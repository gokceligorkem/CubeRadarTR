// Derived from cyberbadger6969/aion2-dps-meter @ 414de5c (GPL-3.0), src/AionMeter.Core/Capture/LiveCapture.cs.
// Changes: hands out raw packets (for the pcapng recording) instead of game events; the BPF filter always includes
// the game port in broad mode, or exact owned inbound connections in strict mode; status as plain text.
// Strict BPF refreshes on the same adapter preserve the decoder; TCP gaps still detect flushed/lost data.
using System.Net;
using SharpPcap;
using SharpPcap.LibPcap;

namespace CubeRadar.Core.Capture;

public sealed class LiveSnifferOptions
{
    /// <summary>Npcap device name to force (e.g. a VPN adapter). Null = pick automatically from the game's connections.</summary>
    public string? DeviceName { get; set; }
    /// <summary>Only incoming traffic of exact TCP connections owned by AION2; close when no connection exists.</summary>
    public bool OnlyGameConnections { get; set; }
}

public readonly record struct CaptureHealth(long Received, long Dropped);

/// <summary>
/// Passive capture of the AION 2 connection through Npcap. It looks up the game's own TCP connections in the Windows
/// TCP table, opens only the adapter that carries them (the loopback adapter when a ping accelerator relays the game
/// locally) and filters to the game port plus those servers. Nothing is ever sent or injected.
/// </summary>
public sealed class LiveSniffer : IDisposable
{
    private const int KernelBufferBytes = 16 * 1024 * 1024;

    private readonly LiveSnifferOptions _options;
    private readonly object _gate = new();
    private readonly SortedSet<string> _extraHosts = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private Task? _monitor;
    private LibPcapLiveDevice? _device;
    private string? _filter;
    private long _filterUpdates;

    public LiveSniffer(LiveSnifferOptions options) => _options = options;

    /// <summary>Raised on the capture thread for every packet that passes the filter.</summary>
    public event Action<RawCapture, LibPcapLiveDevice>? PacketArrived;
    public event Action<string>? StatusChanged;
    public event Action? CaptureReset;

    public string Status { get; private set; } = "stopped";
    public string? DeviceDescription { get; private set; }
    public string? Filter => _filter;
    public long FilterUpdates => Interlocked.Read(ref _filterUpdates);

    public static bool IsNpcapInstalled() =>
        File.Exists(Path.Combine(Environment.SystemDirectory, "Npcap", "wpcap.dll")) ||
        File.Exists(Path.Combine(Environment.SystemDirectory, "wpcap.dll"));

    public static IReadOnlyList<(string Name, string Description)> ListDevices() =>
        CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>().Select(d => (d.Name, Describe(d))).ToList();

    /// <summary>Packets Npcap delivered and dropped on the open adapter; null while none is open.</summary>
    public CaptureHealth? Health()
    {
        lock (_gate)
        {
            if (_device is null) return null;
            try
            {
                var s = _device.Statistics;
                return new CaptureHealth(s.ReceivedPackets, s.DroppedPackets + s.InterfaceDroppedPackets);
            }
            catch (PcapException)
            {
                return null;
            }
        }
    }

    public void Start()
    {
        if (_monitor is not null) return;
        _cts = new CancellationTokenSource();
        SetStatus("starting");
        _monitor = Task.Run(() => MonitorLoop(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        var monitor = _monitor;
        if (monitor is not null)
        {
            try { await monitor.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _monitor = null;
        CloseDevice();
        SetStatus("stopped");
    }

    public void Stop() => StopAsync().GetAwaiter().GetResult();

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    private async Task MonitorLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try { Refresh(ct); }
                catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException)
                { SetStatus("Npcap is not installed (npcap.com)"); }
                catch (PcapException ex)
                { SetStatus("capture error: " + ex.Message); CloseDevice(); }
                await Task.Delay(2_000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        { SetStatus("capture error: " + error.Message); }
        finally { CloseDevice(); }
    }

    internal static string? StrictInboundFilter(IReadOnlyList<GameConnection> connections)
    {
        if (connections.Count == 0) return null;
        // Npcap purges queued packets on a filter change. Auxiliary socket churn must not
        // change a continuing world capture. Keep the existing exact-socket proxy fallback.
        var world=connections.Where(c=>c.RemotePort==StreamTracker.GamePort).ToArray();
        IReadOnlyList<GameConnection> selected=world.Length>0?world:connections;
        var clauses = selected.Distinct().Select(c =>
            $"(src host {c.RemoteIp} and src port {c.RemotePort} and dst host {c.LocalIp} and dst port {c.LocalPort})")
            .Order(StringComparer.Ordinal);
        return "tcp and (" + string.Join(" or ", clauses) + ")";
    }

    private void Refresh(CancellationToken ct)
    {
        var devices = CaptureDeviceList.Instance.OfType<LibPcapLiveDevice>().ToList();
        var pids = GameProcessLocator.FindGameProcessIds();
        var connections = pids.Length == 0 ? [] : GameProcessLocator.FindConnections(pids);
        foreach (var c in connections.Where(c => !_options.OnlyGameConnections && c.RemotePort != StreamTracker.GamePort))
            _extraHosts.Add($"(host {c.RemoteIp} and port {c.RemotePort})");
        var filter = _extraHosts.Count == 0
            ? $"tcp port {StreamTracker.GamePort}"
            : $"tcp and (port {StreamTracker.GamePort} or " + string.Join(" or ", _extraHosts) + ")";
        if (_options.OnlyGameConnections)
        {
            var scoped = StrictInboundFilter(connections);
            if (scoped is null)
            {
                CloseDevice();
                SetStatus(pids.Length == 0 ? "AION 2 is not running" : "waiting for the game to connect");
                return;
            }
            filter = scoped;
        }
        ct.ThrowIfCancellationRequested();

        if (_options.DeviceName is { Length: > 0 } forced)
        {
            var dev = devices.FirstOrDefault(d => d.Name == forced);
            if (dev is null)
            {
                SetStatus($"adapter not found: {forced}");
                return;
            }
            EnsureDevice(dev, filter, ct);
            SetStatus($"capturing on {Describe(dev)}");
            return;
        }

        if (pids.Length == 0)
        {
            SetStatus(_device is null ? "AION 2 is not running" : $"AION 2 not running · still capturing on {DeviceDescription}");
            return;
        }
        if (connections.Count == 0)
        {
            SetStatus(_device is null ? "waiting for the game to connect" : $"no game connection · still capturing on {DeviceDescription}");
            return;
        }

        LibPcapLiveDevice? device = null;
        foreach (var c in connections.OrderByDescending(c => c.RemotePort == StreamTracker.GamePort))
        {
            device = IPAddress.IsLoopback(c.RemoteIp) || IPAddress.IsLoopback(c.LocalIp)
                ? devices.FirstOrDefault(IsLoopback)
                : devices.FirstOrDefault(d => d.Addresses.Any(a => c.LocalIp.Equals(a.Addr?.ipAddress)));
            if (device is not null) break;
        }
        if (device is null)
        {
            SetStatus("no Npcap adapter carries the game connection (VPN? use --device)");
            return;
        }
        EnsureDevice(device, filter, ct);
        SetStatus($"capturing on {Describe(device)}");
    }

    private void EnsureDevice(LibPcapLiveDevice device, string filter, CancellationToken ct)
    {
        lock (_gate)
        {
            ct.ThrowIfCancellationRequested();
            // A connection-list refresh on the same adapter must not erase decoder/collection state.
            // Applying BPF may flush queued packets; the existing TCP gap checks invalidate real loss.
            if (_device is not null && _device.Name != device.Name)
                CloseDeviceLocked();
            if (_device is null)
            {
                device.OnPacketArrival += OnPacketArrival;
                try
                {
                    device.Open(new DeviceConfiguration { Mode = DeviceModes.None, ReadTimeout = 100, BufferSize = KernelBufferBytes });
                    ct.ThrowIfCancellationRequested();
                    device.Filter = filter;
                    Interlocked.Increment(ref _filterUpdates);
                    _filter = filter;
                    _device = device;
                    DeviceDescription = Describe(device);
                    CaptureReset?.Invoke();
                    ct.ThrowIfCancellationRequested();
                    device.StartCapture();
                }
                catch
                {
                    device.OnPacketArrival -= OnPacketArrival;
                    try { device.Close(); } catch (PcapException) { }
                    _device = null;
                    _filter = null;
                    throw;
                }
            }
            else if (_filter != filter)
            {
                _device.Filter = filter;
                Interlocked.Increment(ref _filterUpdates);
                _filter = filter;
            }
        }
    }

    private void CloseDevice()
    {
        lock (_gate) CloseDeviceLocked();
    }

    private void CloseDeviceLocked()
    {
        if (_device is null) return;
        try
        {
            _device.StopCapture();
        }
        catch (PcapException)
        {
        }
        _device.OnPacketArrival -= OnPacketArrival;
        _device.Close();
        _device = null;
        _filter = null;
    }

    private void OnPacketArrival(object sender, PacketCapture e)
    {
        try
        {
            PacketArrived?.Invoke(e.GetPacket(), (LibPcapLiveDevice)sender);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A malformed packet must never take the capture thread down.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void SetStatus(string message)
    {
        if (message == Status) return;
        Status = message;
        StatusChanged?.Invoke(message);
    }

    public static bool IsLoopback(LibPcapLiveDevice d) =>
        d.Name.Contains("Loopback", StringComparison.OrdinalIgnoreCase) ||
        (d.Description?.Contains("loopback", StringComparison.OrdinalIgnoreCase) ?? false);

    public static string Describe(LibPcapLiveDevice d) =>
        IsLoopback(d) ? "Loopback" : string.IsNullOrWhiteSpace(d.Description) ? d.Name : d.Description!;
}
