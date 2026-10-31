// Modified companion, 2026-10-29. GPL-3.0; protocol/capture core by dr89mh and contributors.
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using CubeRadar.Core.Capture;
using CubeRadar.Core.Dump;
using CubeRadar.Core.Protocol;
using CubeRadar.Core.Radar;
using CubeRadar.Core.Pets;

namespace CubeRadar.Tr;

internal sealed class CaptureController
{
    private readonly object _gate = new();
    private readonly RadarConfig _config;
    private DumpSession _session = null!;
    private RadarEngine _engine = null!;
    private LiveSniffer? _sniffer;
    private Task? _stopTask;
    private readonly ConcurrentQueue<uint> _newCubes = new();
    private readonly Queue<BossObservation> _bossUpdates=[];
    private BossWorldTracker _bossWorld=new();
    private PetTracker _pets=new();
    private readonly Queue<HeadingCandidate> _headingCandidates = new();
    private string? _lastPacketType;
    private long _lastFrameMs;
    private long _resets;
    private string _resetReason="İlk açılış";
    public volatile string Status = "Dinleme kapalı";
    public bool IsDemo { get; private set; }
    public bool Running => _sniffer is not null;
    public static bool DriverInstalled => LiveSniffer.IsNpcapInstalled();

    public CaptureController()
    {
        var config = Path.Combine(AppContext.BaseDirectory, "config");
        _config = RadarConfig.Load(Path.Combine(config,"radar.json"), Path.Combine(config,"cubes.json"), AppContext.BaseDirectory);
        // The companion has no player/PvP radar. Keep only cube and own-position messages.
        foreach (var key in _config.Opcodes.Where(p => p.Value.ToString().StartsWith("Player", StringComparison.Ordinal)).Select(p=>p.Key).ToList())
            _config.Opcodes.Remove(key);
        Reset("İlk açılış");
    }

    private void Reset(string reason)
    {
        lock (_gate)
        {
            _resets++;_resetReason=reason;
            _session?.Dispose();
            _newCubes.Clear();
            _bossUpdates.Clear();_bossWorld=new();
            _pets=new();
            _headingCandidates.Clear();
            _lastPacketType=null; _lastFrameMs=0;
            _engine = new RadarEngine(_config); // no persistent store
            _engine.CubeAppeared += cube => _newCubes.Enqueue(cube.ObjectId);
            _session = new DumpSession(OpcodeBook.Empty, new DumpOptions(), jsonlPath:null, pcapngPath:null, markerLogPath:null);
            _session.Tracker.GapSkipped+=(flow,_)=>_pets.Invalidate(flow.Id,"Oyun bağlantısında TCP veri kaybı");
            _session.Tracker.OnBundle+=(GameFlow flow,in BundleMeta meta)=>{if(meta.DecodedSize<0)_pets.Invalidate(flow.Id,"Sıkıştırılmış oyun kaydı okunamadı");};
            _session.Tap = (GameFlow flow, in FrameMeta meta, ReadOnlySpan<byte> body) =>
            {
                _engine.OnFrame(flow.TimeMs,meta.Opcode,body);
                _pets.OnFrame(meta.Opcode,body,flow.TimeMs,_engine.MapId,flow.Id,meta.Seq,
                    _engine.IdentitySource is SelfIdentitySource.SelfInfo or SelfIdentitySource.OwnRecords?_engine.SelfId:null);
                BossObservation? bossUpdate=meta.Opcode==FieldBossMessages.Opcode&&FieldBossMessages.TryParse(body,flow.TimeMs,out var bossList)
                    ?new BossListObservation(bossList):_bossWorld.OnFrame(meta.Opcode,body,flow.TimeMs,_engine.MapId);
                if(bossUpdate is not null){_bossUpdates.Enqueue(bossUpdate);while(_bossUpdates.Count>64)_bossUpdates.Dequeue();}
                _lastPacketType=$"0x{meta.Opcode:X4}"; _lastFrameMs=flow.TimeMs;
                if(HeadingProbe.Read(flow.TimeMs,meta.Opcode,body,_engine.SelfId) is { } candidate)
                {
                    _headingCandidates.Enqueue(candidate);
                    while(_headingCandidates.Count>32)_headingCandidates.Dequeue();
                }
            };
        }
    }

    public bool Start()
    {
        if (_stopTask is { IsCompleted:false }) return false;
        if (Running) return true;
        if (!DriverInstalled)
        {
            Status = "Npcap bulunamadı. Resmî Npcap kurulumundan sonra yeniden başlat.";
            return false;
        }
        Reset("Dinleme başlatıldı");
        IsDemo = false;
        var sniffer = new LiveSniffer(new LiveSnifferOptions { OnlyGameConnections = true });
        sniffer.CaptureReset += ()=>Reset("Yakalama adaptörü açıldı veya değişti");
        sniffer.StatusChanged += value =>
        {
            if (value is "AION 2 is not running" or "waiting for the game to connect" || value.StartsWith("capture error:"))
                Reset(value.StartsWith("capture error:")?"Yakalama hatası":value=="AION 2 is not running"?"Oyun kapalı":"Oyun bağlantısı kesildi");
            Status = Translate(value);
        };
        sniffer.PacketArrived += (packet, device) =>
        {
            lock (_gate) _session.OnPacket(packet, device.Name, device.Description);
        };
        _sniffer = sniffer;
        try { sniffer.Start(); }
        catch { _sniffer = null; sniffer.Dispose(); throw; }
        return true;
    }

    public Task StopAsync()
    {
        lock (_gate)
            return _stopTask is { IsCompleted:false } ? _stopTask : _stopTask=StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        var sniffer = _sniffer;
        if (sniffer is not null)
        {
            await sniffer.StopAsync().ConfigureAwait(false);
            sniffer.Dispose();
        }
        _sniffer = null;
        IsDemo = false;
        Reset("Dinleme durduruldu");
        Status = "Dinleme kapalı";
    }

    public RadarSnapshot Snapshot() { lock (_gate) return _engine.Snapshot(); }
    public PetSnapshot ReadPets(long now) {lock(_gate)return _pets.Read(now);}
    public IReadOnlyList<PetMob> ReadRadarMobs() {lock(_gate)return _pets.ReadRadarMobs();}
    public PetCombatDiagnostics PetCombatDiagnostics {get{lock(_gate)return _pets.CombatDiagnostics();}}
    public object PetSyncDiagnostics
    {
        get
        {
            // Read Npcap before taking the controller lock: adapter changes acquire them in this order.
            var health=_sniffer?.Health();
            lock(_gate)
            {
                var pets=_pets.Read(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                return new {captureResets=_resets,lastResetReason=_resetReason,pets.Epoch,pets.Synced,pets.Status,
                    trackedBalances=pets.Progress.Count,verifiedBalances=pets.Progress.Values.Count(p=>p.Verified),pets.CombatPartial,
                    npcap=health,filterUpdates=_sniffer?.FilterUpdates??0,tcpGaps=_session.Tracker.Gaps,
                    badBundles=_session.Tracker.GameFlows.Sum(f=>f.Decoder.BadBundles),decoderResyncs=_session.Tracker.GameFlows.Sum(f=>f.Decoder.Resyncs)};
            }
        }
    }
    public CaptureView Read()
    {
        lock(_gate) return new(_engine.Snapshot(),Diagnostics,_lastPacketType,_lastFrameMs,_headingCandidates.ToArray());
    }
    public (long Packets,long Frames,int Streams,long Heartbeats) Diagnostics
    {
        get { lock (_gate) return _session.Read(s=>(s.Packets,s.Census.Frames,s.Tracker.GameFlows.Count,s.Census.Heartbeats)); }
    }
    public bool TakeAlert(out uint cube) => _newCubes.TryDequeue(out cube);
    public BossObservation[] TakeBossUpdates()
    {
        lock(_gate){var items=_bossUpdates.ToArray();_bossUpdates.Clear();return items;}
    }
    public bool IsVerified(CubeView cube) => cube.Learned=="key-item" ||
        (_config.CubeTemplates.TryGetValue(cube.Template,out var template) && template.Status=="verified");

    public void LoadDemo()
    {
        if (Running) throw new InvalidOperationException("Önce canlı dinlemeyi durdur.");
        Reset("Örnek veri açıldı");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("cube-demo.json")!;
        using var document = JsonDocument.Parse(stream);
        var frames = document.RootElement.GetProperty("frames").EnumerateArray().TakeWhile(f=>f.GetProperty("op").GetString()!="3836").ToList();
        var end = frames[^1].GetProperty("t").GetInt64();
        var offset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-end;
        var self = document.RootElement.GetProperty("self_id").GetUInt32();
        var id = new List<byte>();
        do { var b=(byte)(self&127); self>>=7; id.Add((byte)(b|(self>0?128:0))); } while(self>0);
        lock (_gate)
        {
            _engine.OnFrame(frames[0].GetProperty("t").GetInt64()+offset, 0x3336, id.ToArray());
            foreach(var frame in frames)
                _engine.OnFrame(frame.GetProperty("t").GetInt64()+offset,
                    Convert.ToUInt16(frame.GetProperty("op").GetString(),16), Convert.FromHexString(frame.GetProperty("hex").GetString()!));
            _newCubes.Clear();
        }
        IsDemo = true;
        Status = "ÖRNEK VERİ · canlı oyun bağlantısı değil";
    }

    private static string Translate(string status) => status switch
    {
        "starting" => "Oyun bağlantısı aranıyor…",
        "stopped" => "Dinleme kapalı",
        "AION 2 is not running" => "AION2 açık değil · oyun bekleniyor",
        "waiting for the game to connect" => "Tanınan oyun bağlantısı bekleniyor",
        _ when status.StartsWith("capturing on ") => "Dinleniyor · " + status[13..],
        _ when status.StartsWith("capture error: ") => "Dinleme hatası: " + status[15..],
        _ when status.Contains("Npcap is not installed") => "Npcap bulunamadı. Npcap kurulumunu kontrol et.",
        _ => status
    };
}

internal sealed record CaptureView(RadarSnapshot Snapshot,
    (long Packets,long Frames,int Streams,long Heartbeats) Traffic,string? LastPacketType,long LastFrameMs,
    IReadOnlyList<HeadingCandidate> HeadingCandidates);
