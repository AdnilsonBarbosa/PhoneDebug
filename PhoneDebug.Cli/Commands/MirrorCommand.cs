using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class MirrorCommand
{
    public static async Task<int> Run(PhoneDebugContext core, CliOptions options, CancellationToken ct)
    {
        var device = DevicePicker.Pick(core.Devices!);
        if (device is null)
            return ExitCodes.NoDevice;

        Ui.Blank();
        Ui.Ok($"{device.Name} connected");
        if (device.AndroidLabel is not null)
            Ui.Line($"  {device.AndroidLabel}");

        Ui.Blank();
        Ui.Line("Starting mirror...");

        var key = MirrorPreferences.KeyFor(device);

        // Asking for a mode explicitly also sets it as this phone's default.
        if (options.Input == MirrorInput.Standard)
            UserPreferences.ForgetEmulatedInput(key);
        else if (options.Input == MirrorInput.Emulated)
            UserPreferences.RememberEmulatedInput(key);

        var input = options.Input
                    ?? (UserPreferences.NeedsEmulatedInput(key) ? MirrorInput.Emulated : MirrorInput.Standard);

        if (device.IsWireless && !options.Light)
        {
            Ui.Hint("Connected over Wi-Fi. If the picture stutters, use a USB cable");
            Ui.Hint("or add \"--light\" for a smaller, smoother stream.");
        }

        // Two goes at most: the second only happens when the phone turns out to
        // refuse injected input and scrcpy can emulate a real keyboard instead.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var session = core.Scrcpy!.StartMirror(device, input, options.Light);
            if (session is null)
            {
                Ui.Error("The screen could not be opened.");
                Ui.Hint($"Details: {Log.FilePath}");
                return ExitCodes.Error;
            }

            if (input == MirrorInput.Emulated)
            {
                Ui.Hint(MirrorPreferences.EmulatedInputNote);
                Ui.Hint(MirrorPreferences.ReleaseMouseNote);
            }

            var switching = false;

            session.ProblemDetected += (_, problem) =>
            {
                if (problem.Key != MirrorDiagnostics.ControlBlocked
                    || input != MirrorInput.Standard
                    || options.Input == MirrorInput.Standard
                    || !core.Scrcpy.SupportsEmulatedInput)
                {
                    Ui.Problem(problem);
                    return;
                }

                switching = true;
                Ui.Blank();
                Ui.Warn($"! {problem.Summary}");
                Ui.Hint("Switching to an emulated keyboard and mouse...");
                UserPreferences.RememberEmulatedInput(key);

                // Not from scrcpy's own output thread - that would block it.
                Task.Run(session.Stop);
            };

            await session.WaitForExitAsync(ct).ConfigureAwait(false);

            if (switching)
            {
                input = MirrorInput.Emulated;
                continue;
            }

            if (session.ExitCode != 0 && !ct.IsCancellationRequested)
            {
                Ui.Blank();
                Ui.Error("The screen closed unexpectedly.");
                var output = session.RecentOutput;
                if (output.Length > 0)
                    Ui.Hint(output);
                return ExitCodes.Error;
            }

            return ExitCodes.Ok;
        }

        return ExitCodes.Ok;
    }
}
