using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class InstallCommand
{
    public static async Task<int> Run(DeviceService devices, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Ui.Blank();
            Ui.Error("No APK given.");
            Ui.Hint("  phone-debug install <app.apk>");
            Ui.Blank();
            return ExitCodes.Error;
        }

        // Everything after the command is the path, so unquoted paths with
        // spaces still work: phone-debug install C:\my apps\demo.apk
        var apk = string.Join(' ', args);

        var validation = DeviceService.ValidateApk(apk);
        if (!validation.Success)
        {
            Ui.Blank();
            Ui.Error(validation.Message);
            Ui.Blank();
            return ExitCodes.Error;
        }

        var device = DevicePicker.Pick(devices);
        if (device is null)
            return ExitCodes.NoDevice;

        Ui.Blank();
        Ui.Line($"Installing {Path.GetFileName(validation.FullPath!)} on {device.Name}...");

        var result = await Task.Run(() => devices.InstallApk(device.Serial, apk), ct).ConfigureAwait(false);

        Ui.Blank();
        if (!result.Success)
        {
            Ui.Error(result.Message);
            Ui.Blank();
            return ExitCodes.Error;
        }

        Ui.Ok(result.Message);
        Ui.Blank();
        return ExitCodes.Ok;
    }
}
