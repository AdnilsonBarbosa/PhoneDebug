using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

public enum DeviceMonitorState
{
    Starting,
    NoDevice,
    Unauthorized,
    Offline,
    MultipleDevices,
    Connected,
    AdbError,
}

public sealed class DeviceMonitorEventArgs : EventArgs
{
    public required DeviceMonitorState State { get; init; }
    public AndroidDevice? Device { get; init; }
    public IReadOnlyList<AndroidDevice> Devices { get; init; } = [];
    public string? Detail { get; init; }
}

/// <summary>
/// Watches "adb devices" and raises an event whenever the situation changes:
/// a phone appears, is waiting for authorization, goes offline or is unplugged.
/// Events are raised on a background thread - user interfaces must marshal.
/// </summary>
public sealed class DeviceMonitor : IDisposable
{
    private static readonly TimeSpan WaitSlice = TimeSpan.FromMilliseconds(100);

    private readonly DeviceService _devices;
    private readonly TimeSpan _interval;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private volatile bool _refreshRequested;
    private bool _disposed;

    public DeviceMonitor(DeviceService devices, TimeSpan? interval = null)
    {
        _devices = devices;
        _interval = interval ?? TimeSpan.FromSeconds(2);
    }

    public event EventHandler<DeviceMonitorEventArgs>? Changed;

    public DeviceMonitorState State { get; private set; } = DeviceMonitorState.Starting;

    public AndroidDevice? Current { get; private set; }

    /// <summary>Pins the monitor to one device when several are connected.</summary>
    public string? PreferredSerial { get; set; }

    public void Start()
    {
        if (_loop is not null)
            return;

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            if (_loop is not null)
                await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // expected
        }

        _loop = null;
        _cts.Dispose();
        _cts = null;
    }

    /// <summary>Polls immediately instead of waiting for the next tick.</summary>
    public void RefreshNow() => _refreshRequested = true;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                Log.Error("Device poll failed", ex);
                Publish(DeviceMonitorState.AdbError, null, [], ex.Message);
            }

            if (!await WaitForNextPollAsync(cancellationToken).ConfigureAwait(false))
                break;
        }
    }

    /// <summary>Sleeps until the next tick, waking early when RefreshNow is called.</summary>
    private async Task<bool> WaitForNextPollAsync(CancellationToken cancellationToken)
    {
        var waited = TimeSpan.Zero;
        _refreshRequested = false;

        while (waited < _interval)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (_refreshRequested)
            {
                _refreshRequested = false;
                return true;
            }

            try
            {
                await Task.Delay(WaitSlice, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            waited += WaitSlice;
        }

        return true;
    }

    private void Poll()
    {
        var devices = _devices.ListDevices();
        var (state, chosen) = Evaluate(devices, PreferredSerial, Current?.Serial);

        if (state == DeviceMonitorState.Connected && chosen is not null)
        {
            // Only re-read properties when the device actually changed.
            if (Current is null || Current.Serial != chosen.Serial || State != DeviceMonitorState.Connected)
            {
                _devices.Adb.FillDetails(chosen);
                Publish(DeviceMonitorState.Connected, chosen, devices);
            }

            return;
        }

        Publish(state, null, devices);
    }

    /// <summary>
    /// Decides what the current list of devices means. Pure, so every branch
    /// (unauthorized, offline, several devices) can be tested without a phone.
    /// </summary>
    public static (DeviceMonitorState State, AndroidDevice? Device) Evaluate(
        IReadOnlyList<AndroidDevice> devices,
        string? preferredSerial,
        string? currentSerial)
    {
        var authorized = devices.Where(d => d.IsAuthorized).ToList();

        if (authorized.Count > 0)
        {
            var chosen = Choose(authorized, preferredSerial, currentSerial);
            return chosen is null
                ? (DeviceMonitorState.MultipleDevices, null)
                : (DeviceMonitorState.Connected, chosen);
        }

        if (devices.Any(d => d.IsUnauthorized))
            return (DeviceMonitorState.Unauthorized, null);

        if (devices.Any(d => d.IsOffline))
            return (DeviceMonitorState.Offline, null);

        return (DeviceMonitorState.NoDevice, null);
    }

    private static AndroidDevice? Choose(
        List<AndroidDevice> authorized,
        string? preferredSerial,
        string? currentSerial)
    {
        if (preferredSerial is not null)
        {
            var pinned = authorized.FirstOrDefault(d => d.Serial == preferredSerial);
            if (pinned is not null)
                return pinned;
        }

        // Stay on the device already in use.
        if (currentSerial is not null)
        {
            var same = authorized.FirstOrDefault(d => d.Serial == currentSerial);
            if (same is not null)
                return same;
        }

        return authorized.Count == 1 ? authorized[0] : null;
    }

    private void Publish(
        DeviceMonitorState state,
        AndroidDevice? device,
        IReadOnlyList<AndroidDevice> devices,
        string? detail = null)
    {
        var sameDevice = device?.Serial == Current?.Serial;
        if (state == State && sameDevice && state != DeviceMonitorState.AdbError)
            return;

        State = state;
        Current = device;

        Log.Info($"Monitor -> {state} {device?.Serial ?? ""} {detail ?? ""}".TrimEnd());

        Changed?.Invoke(this, new DeviceMonitorEventArgs
        {
            State = state,
            Device = device,
            Devices = devices,
            Detail = detail,
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try { _cts?.Cancel(); } catch { /* shutting down */ }
        _cts?.Dispose();
    }
}
