using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class LogsCommand
{
    public static async Task<int> Run(DeviceService devices, CancellationToken ct)
    {
        var device = DevicePicker.Pick(devices);
        if (device is null)
            return ExitCodes.NoDevice;

        Ui.Blank();
        Ui.Line($"Logs from {device.Name}");
        Ui.Hint("Press Ctrl+C to stop.");
        Ui.Blank();

        using var session = devices.StartLogcat(device.Serial);
        if (session is null)
        {
            Ui.Error("adb could not be started.");
            return ExitCodes.Error;
        }

        session.LineReceived += (_, line) =>
        {
            if (line.IsError)
                Console.Error.WriteLine(line.Text);
            else
                Console.WriteLine(line.Text);
        };

        await session.WaitForExitAsync(ct).ConfigureAwait(false);
        return ExitCodes.Ok;
    }
}
