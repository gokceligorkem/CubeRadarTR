using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using CubeRadar.Core.Capture;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tr;

/// <summary>Only numeric motion state is consumed from this current-user-only pipe. Keys stay inside the read-only helper.</summary>
internal sealed class MotionBridge
{
    private readonly object _gate=new();
    private CancellationTokenSource? _stop;
    private NamedPipeServerStream? _pipe;
    private Task? _reader;
    private ClientMotionFix? _latest;
    private long _healthAt,_clockAt;
    private bool _ready;
    private string _helperVersion="bilinmiyor";
    private Dictionary<string,long> _counters=[],_motionShapes=[];
    public volatile string Status="Paket yönü kapalı";
    public bool Active=>_stop is {IsCancellationRequested:false};
    public int GamePid {get;private set;}

    public (ClientMotionFix? Motion,bool Ready) Read(long now)
    {
        lock(_gate)return(_latest,_ready&&now-_healthAt<2000&&now-_clockAt<5000);
    }

    public object ReadDiagnostics(long now)
    {
        lock(_gate)
        {
            long? Age(long stamp)=>stamp>0?Math.Max(0,now-stamp):null;
            var healthAge=Age(_healthAt);var clockAge=Age(_clockAt);var motionAge=Age(_latest?.TimeMs??0);
            var ready=_ready&&healthAge is <2000&&clockAge is <5000;
            string reason;
            if(!Active)reason=Status;
            else if(healthAge is null or >=2000)reason="Okuyucu yanıtı bekleniyor";
            else if(!_ready)reason="Paket eşlemesi bekleniyor";
            else if(clockAge is null or >=5000)reason="Saat doğrulaması gecikti";
            else if(motionAge is null)reason="İlk hareket kaydı bekleniyor";
            else reason=motionAge>2500?"Yeni hareket kaydı gelmiyor":"Güncel hareket";
            return new {active=Active,ready,reason,helperVersion=_helperVersion,healthAgeMs=healthAge,clockAgeMs=clockAge,
                motionAgeMs=motionAge,observationAgeMs=Age(_latest?.LastObservationAt??0),positionChangedAgeMs=Age(_latest?.LastPositionChangedAt??0),
                poseStopped=_latest?.Stopped,lastMotionOpcode=_latest?.Opcode,counters=new Dictionary<string,long>(_counters),
                motionShapes=new Dictionary<string,long>(_motionShapes)};
        }
    }

    public async Task StartAsync()
    {
        if(Active)return;
        var helper=Path.Combine(AppContext.BaseDirectory,"Motion","CubeRadarMotion.exe");
        if(!File.Exists(helper)){Status="Motion klasöründeki okuyucu bulunamadı";return;}
        var pids=GameProcessLocator.FindGameProcessIds().Where(pid=>GameProcessLocator.FindConnections([pid]).Any(c=>c.RemotePort==13328)).ToArray();
        if(pids.Length!=1){Status="Tek bir etkin AION2 dünya bağlantısı gerekli";return;}
        var source=new CancellationTokenSource();_stop=source;GamePid=pids[0];
        lock(_gate){_healthAt=_clockAt=0;_latest=null;_ready=false;_helperVersion="bilinmiyor";_counters=[];_motionShapes=[];}
        var name="CubeRadarMotion-"+Guid.NewGuid().ToString("N");
        // Explicit same-account ACL supports the approved elevated helper while excluding other users/network logons.
        using var identity=WindowsIdentity.GetCurrent();
        var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.SetOwner(identity.User!);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(identity.User!,PipeAccessRights.FullControl,AccessControlType.Allow));
        var pipe=NamedPipeServerStreamAcl.Create(name,PipeDirection.In,1,PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous|PipeOptions.FirstPipeInstance,8192,8192,security);
        _pipe=pipe;Status="Windows yönetici onayı bekleniyor";
        _reader=ReadAsync(pipe,source);
        try
        {
            await Task.Run(()=>
            {
                var info=new ProcessStartInfo(helper) { UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden };
                info.ArgumentList.Add("--pid");info.ArgumentList.Add(pids[0].ToString());
                info.ArgumentList.Add("--pipe");info.ArgumentList.Add(name);
                info.ArgumentList.Add("--seconds");info.ArgumentList.Add("43200");
                using var process=Process.Start(info);
            });
        }
        catch(Exception error)when(error is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Status="Okuyucu başlatılamadı: "+error.Message;await StopAsync(preserveStatus:true); }
    }

    private async Task ReadAsync(NamedPipeServerStream pipe,CancellationTokenSource source)
    {
        try
        {
            await pipe.WaitForConnectionAsync(source.Token);
            using var reader=new StreamReader(pipe);
            while(!source.IsCancellationRequested)
            {
                var line=await reader.ReadLineAsync(source.Token);
                if(line is null)break;
                if(line.Length>4096)continue;
                try
                {
                    using var json=JsonDocument.Parse(line);
                    var value=json.RootElement;
                    var kind=value.GetProperty("kind").GetString();
                    var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if(kind=="motion")
                    {
                        var motion=new ClientMotionFix(value.GetProperty("x").GetDouble(),value.GetProperty("y").GetDouble(),value.GetProperty("z").GetDouble(),
                            value.GetProperty("faceYaw").GetDouble(),value.GetProperty("moveYaw").ValueKind==JsonValueKind.Null?null:value.GetProperty("moveYaw").GetDouble(),
                            value.GetProperty("t").GetInt64(),value.GetProperty("op").GetString()??"") {
                                Stopped=value.TryGetProperty("stopped",out var stopped)&&stopped.ValueKind==JsonValueKind.True,
                                PoseGeneration=value.TryGetProperty("poseGeneration",out var generation)?generation.GetInt64():0,
                                PoseSerial=value.TryGetProperty("poseSerial",out var serial)?serial.GetInt64():0,
                                PositionChangedAtMs=value.TryGetProperty("positionChangedAtMs",out var changed)?changed.GetInt64():0};
                        if(motion.Opcode is not ("0037" or "0137" or "0337" or "0A37" or "0B37" or "1837")||Math.Abs(now-motion.TimeMs)>5000)continue;
                        if(motion.Stopped&&motion.Opcode is not ("0037" or "0137")){lock(_gate)_latest=null;continue;}
                        lock(_gate)_latest=motion;
                    }
                    else if(kind=="health")
                    {
                        lock(_gate)
                        {
                            _ready=value.GetProperty("streamReady").GetBoolean();
                            _healthAt=now;_clockAt=value.GetProperty("lastClockMs").GetInt64();
                            if(value.TryGetProperty("poseProtocol",out var poseProtocol)&&poseProtocol.ValueKind==JsonValueKind.Number&&poseProtocol.GetInt32()==1)
                            {
                                if(value.TryGetProperty("pose",out var pose)&&pose.ValueKind==JsonValueKind.Object&&
                                    pose.TryGetProperty("observationAtMs",out var observation)&&observation.ValueKind==JsonValueKind.Number&&
                                    observation.TryGetInt64(out var poseAt)&&poseAt<=Math.Max(_latest?.TimeMs??0,_clockAt))
                                    _latest=ClientPoseObservation.Apply(_latest,pose,now);
                                else _latest=null;
                            }
                            if(value.TryGetProperty("helperVersion",out var version)&&version.ValueKind==JsonValueKind.String&&
                                version.GetString() is {} tag&&System.Text.RegularExpressions.Regex.IsMatch(tag,@"^\d{1,3}\.\d{1,3}\.\d{1,3}$"))_helperVersion=tag;
                            foreach(var key in new[]{"frames","clocks","motions","gaps","outOfOrder","tcpGaps","pendingBytes","pendingSegments"})
                                if(value.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.Number&&field.TryGetInt64(out var count)&&count>=0)_counters[key]=count;
                            if(value.TryGetProperty("motionShapes",out var shapes)&&shapes.ValueKind==JsonValueKind.Object)
                            {
                                _motionShapes=[];
                                foreach(var shape in shapes.EnumerateObject().Take(24))
                                    if(System.Text.RegularExpressions.Regex.IsMatch(shape.Name,@"^[0-9A-F]{4}:\d{1,5}$")&&
                                        shape.Value.ValueKind==JsonValueKind.Number&&shape.Value.TryGetInt64(out var count)&&count>=0)_motionShapes[shape.Name]=count;
                            }
                            Status=_ready?"C2S paket yönü okunuyor":"C2S paket eşlemesi yenileniyor";
                        }
                    }
                    else if(kind is "status" or "error")
                    {
                        var message=value.GetProperty("text").GetString()??"";
                        Status=message.Length>180?message[..180]:message;
                    }
                }
                catch(Exception error)when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
                { lock(_gate){_latest=null;_ready=false;}Status="Okuyucudan geçersiz durum kaydı"; }
            }
            if(!source.IsCancellationRequested)Status="Paket yönü okuyucusu durdu";
        }
        catch(Exception error)when(error is OperationCanceledException or IOException or ObjectDisposedException)
        { if(!source.IsCancellationRequested)Status="Paket yönü bağlantısı kesildi"; }
        finally { lock(_gate){_ready=false;_latest=null;}GamePid=0;source.Cancel();pipe.Dispose(); }
    }

    public async Task StopAsync(bool preserveStatus=false)
    {
        _stop?.Cancel();_pipe?.Dispose();
        if(_reader is not null)await _reader;
        _reader=null;_pipe=null;_stop?.Dispose();_stop=null;GamePid=0;
        lock(_gate){_ready=false;_latest=null;}
        if(!preserveStatus)Status="Paket yönü kapalı";
    }
}
