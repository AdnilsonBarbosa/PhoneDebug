using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;
using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

/// <summary>
/// "phone-debug" with no arguments: wait for a phone, announce it and open
/// the screen automatically, then keep reacting to plug and unplug.
/// </summary>
internal sealed class WatchCommand
{
    private readonly PhoneDebugContext _core;
    private readonly DeviceMonitor _monitor;
    private readonly CliOptions _options;
    private MirrorSession? _session;
    private AndroidDevice? _device;
    private MirrorInput _input = MirrorInput.Standard;
    private bool _hintShown;

    private WatchCommand(PhoneDebugContext core, DeviceMonitor monitor, CliOptions options)
    {
        _core = core;
        _monitor = monitor;
        _options = options;
    }

    public static async Task<int> Run(PhoneDebugContext core, CliOptions options, CancellationToken ct)
    {
        using var monitor = core.CreateMonitor();
        var watch = new WatchCommand(core, monitor, options);

        monitor.Changed += watch.OnChanged;

        Ui.Blank();
        Ui.Hint("Press Ctrl+C to exit.");

        try
        {
            await monitor.RunAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            watch.StopMirror();
        }

        Ui.Blank();
        Ui.Hint("Bye.");
        return ExitCodes.Ok;
    }

    private void OnChanged(object? sender, DeviceMonitorEventArgs e)
    {
        switch (e.State)
        {
            case DeviceMonitorState.Connected when e.Device is not null:
                Connected(e.Device);
                break;

            case DeviceMonitorState.Unauthorized:
                StopMirror();
                Ui.Blank();
                Ui.Line("Android device detected.");
                Ui.Blank();
                Ui.Line("Unlock your phone and accept:");
                Ui.Line("\"Allow USB debugging\"");
                Ui.Blank();
                Ui.Hint("Waiting for authorization...");
                break;

            case DeviceMonitorState.Offline:
                StopMirror();
                Ui.Blank();
                Ui.Warn("The device is offline.");
                Ui.Hint("Unplug and reconnect the cable. Waiting...");
                break;

            case DeviceMonitorState.MultipleDevices:
                ChooseDevice(e.Devices);
                break;

            case DeviceMonitorState.AdbError:
                Ui.Blank();
                Ui.Warn("ADB is not responding. Retrying...");
                break;

            case DeviceMonitorState.NoDevice:
                StopMirror();
                Ui.Blank();
                Ui.Idle("Waiting for Android device...");

                if (!_hintShown)
                {
                    _hintShown = true;
                    Ui.Hint("Nothing connected? Run \"phone-debug connect\" for USB or Wi-Fi (QR code) setup.");
                }

                break;
        }
    }

    private void Connected(AndroidDevice device)
    {
        Ui.Blank();
        Ui.Ok($"{device.Name} connected");
        if (device.AndroidLabel is not null)
            Ui.Line($"  {device.AndroidLabel}");

        Ui.Blank();
        Ui.Line("Starting mirror...");

        var key = MirrorPreferences.KeyFor(device);

        // Asking for a mode explicitly also sets it as this phone's default.
        if (_options.Input == MirrorInput.Standard)
            UserPreferences.ForgetEmulatedInput(key);
        else if (_options.Input == MirrorInput.Emulated)
            UserPreferences.RememberEmulatedInput(key);

        // A phone already known to block injected input starts emulated, so it
        // does not have to fail once more first.
        StartMirror(device, _options.Input
                            ?? (UserPreferences.NeedsEmulatedInput(key)
                                ? MirrorInput.Emulated
                                : MirrorInput.Standard));

        if (device.IsWireless && !_options.Light)
        {
            Ui.Hint("Connected over Wi-Fi. If the picture stutters, use a USB cable");
            Ui.Hint("or run \"phone-debug --light\" for a smaller, smoother stream.");
        }
    }

    private void StartMirror(AndroidDevice device, MirrorInput input)
    {
        StopMirror();

        _device = device;
        _input = input;

        _session = _core.Scrcpy!.StartMirror(device, input, _options.Light);
        if (_session is null)
        {
            Ui.Error("The screen could not be opened.");
            Ui.Hint($"Details: {Log.FilePath}");
            return;
        }

        _session.Exited += OnMirrorExited;
        _session.ProblemDetected += OnProblem;

        if (input == MirrorInput.Emulated)
        {
            Ui.Hint(MirrorPreferences.EmulatedInputNote);
            Ui.Hint(MirrorPreferences.ReleaseMouseNote);
        }
    }

    private void OnProblem(object? sender, MirrorProblem problem)
    {
        var device = _device;

        // Asking for standard input explicitly means "do not switch on me".
        var canSwitch = problem.Key == MirrorDiagnostics.ControlBlocked
                        && _input == MirrorInput.Standard
                        && _options.Input != MirrorInput.Standard
                        && device is not null
                        && _core.Scrcpy!.SupportsEmulatedInput;

        if (!canSwitch)
        {
            Ui.Problem(problem);
            return;
        }

        Ui.Blank();
        Ui.Warn($"! {problem.Summary}");
        Ui.Hint("Switching to an emulated keyboard and mouse...");

        UserPreferences.RememberEmulatedInput(MirrorPreferences.KeyFor(device!));

        // Restarting from inside scrcpy's own output handler would block it.
        Task.Run(() => StartMirror(device!, MirrorInput.Emulated));
    }

    private void OnMirrorExited(object? sender, EventArgs e)
    {
        if (sender is not MirrorSession session)
            return;

        session.Exited -= OnMirrorExited;

        // Exit code 0 means the user closed the window; anything else is a failure.
        if (session.ExitCode != 0)
        {
            Ui.Blank();
            Ui.Error("The screen closed unexpectedly.");
            var output = session.RecentOutput;
            if (output.Length > 0)
                Ui.Hint(output);
        }
        else
        {
            Ui.Blank();
            Ui.Hint("Screen closed. Run \"phone-debug mirror\" to open it again.");
        }
    }

    private void ChooseDevice(IReadOnlyList<AndroidDevice> devices)
    {
        var authorized = devices.Where(d => d.IsAuthorized).ToList();
        foreach (var device in authorized)
            _core.Devices!.Adb.FillDetails(device);

        // No keyboard available (piped input): say what was picked and carry on.
        if (Console.IsInputRedirected)
        {
            Ui.Blank();
            Ui.Warn($"Several devices connected; using {authorized[0].DisplayName}.");
            _monitor.PreferredSerial = authorized[0].Serial;
            _monitor.RefreshNow();
            return;
        }

        Ui.Blank();
        Ui.Line("Several Android devices are connected:");
        for (var i = 0; i < authorized.Count; i++)
            Ui.Line($"  [{i + 1}] {authorized[i].DisplayName}");

        Ui.Blank();
        Console.Write($"Choose a device (1-{authorized.Count}): ");

        var input = Console.ReadLine();
        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= authorized.Count)
        {
            _monitor.PreferredSerial = authorized[choice - 1].Serial;
            _monitor.RefreshNow();
            return;
        }

        Ui.Warn("Nothing selected; waiting.");
    }

    private void StopMirror()
    {
        if (_session is null)
            return;

        _session.Exited -= OnMirrorExited;
        _session.ProblemDetected -= OnProblem;
        _session.Dispose();
        _session = null;
    }
}
