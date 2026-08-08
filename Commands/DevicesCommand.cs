using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class DevicesCommand
{
    public static int Run(AdbService adb)
    {
        var devices = adb.GetDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("No devices connected.");
            return 0;
        }

        Console.WriteLine("Android devices:");
        foreach (var device in devices)
        {
            if (device.IsAuthorized)
            {
                adb.FillDetails(device);
                Console.WriteLine($"  {device.Serial,-14} {device.State,-12} {device.Model ?? "(unknown)",-24} Android {device.AndroidVersion ?? "?"}");
            }
            else
            {
                Console.WriteLine($"  {device.Serial,-14} {device.State}");
            }
        }

        return 0;
    }
}