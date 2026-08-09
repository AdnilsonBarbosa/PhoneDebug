using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class ScreenshotCommand
{
    public static async Task<int> Run(DeviceService devices, string[] args, CancellationToken ct)
    {
        var device = DevicePicker.Pick(devices);
        if (device is null)
            return ExitCodes.NoDevice;

        // An optional destination: a file, or a folder to drop the capture in.
        var target = args.Length > 0 ? string.Join(' ', args) : null;

        Ui.Blank();
        Ui.Line($"Capturing {device.Name}...");

        var result = await devices.CaptureScreenshotAsync(device.Serial, target, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            Ui.Error(result.Message);
            Ui.Blank();
            return ExitCodes.Error;
        }

        Ui.Ok(result.Message);
        Ui.Hint($"  {result.Bytes:N0} bytes");
        Ui.Blank();
        return ExitCodes.Ok;
    }
}
