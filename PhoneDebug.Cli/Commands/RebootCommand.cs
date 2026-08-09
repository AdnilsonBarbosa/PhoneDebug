using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class RebootCommand
{
    public static int Run(DeviceService devices)
    {
        var device = DevicePicker.Pick(devices);
        if (device is null)
            return ExitCodes.NoDevice;

        Ui.Blank();
        Ui.Line($"Rebooting {device.Name}...");

        var result = devices.Reboot(device.Serial);
        if (!result.Success)
        {
            Ui.Error(result.Message);
            return ExitCodes.Error;
        }

        Ui.Ok("Reboot requested.");
        Ui.Blank();
        return ExitCodes.Ok;
    }
}
