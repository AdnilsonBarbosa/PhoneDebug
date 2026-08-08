using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class RebootCommand
{
    public static int Run(AdbService adb)
    {
        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        Console.WriteLine($"Rebooting {device.DisplayName}...");
        var result = adb.RunCapture("-s", device.Serial, "reboot");

        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error.Length > 0 ? result.Error : result.Output);
            return 1;
        }

        return 0;
    }
}