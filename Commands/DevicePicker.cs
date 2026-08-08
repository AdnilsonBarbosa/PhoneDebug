using PhoneDebug.Models;
using PhoneDebug.Services;

namespace PhoneDebug.Commands;

/// <summary>
/// Selects the single connected device, or asks the user when several
/// authorized devices are present.
/// </summary>
internal static class DevicePicker
{
    public static AndroidDevice? PickOrNull(AdbService adb)
    {
        var authorized = adb.GetDevices().Where(d => d.IsAuthorized).ToList();

        if (authorized.Count == 0)
            return null;

        if (authorized.Count == 1)
        {
            adb.FillDetails(authorized[0]);
            return authorized[0];
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Multiple Android devices detected:");
            for (var i = 0; i < authorized.Count; i++)
                Console.WriteLine($"  [{i + 1}] {authorized[i].DisplayName}");
            Console.WriteLine();
            Console.Write($"Select a device (1-{authorized.Count}): ");

            var input = Console.ReadLine();
            if (!int.TryParse(input, out var choice) || choice < 1 || choice > authorized.Count)
            {
                Console.WriteLine("Invalid choice, try again.");
                continue;
            }

            var device = authorized[choice - 1];
            adb.FillDetails(device);
            return device;
        }
    }

    public static AndroidDevice? PickOrWarn(AdbService adb)
    {
        var device = PickOrNull(adb);
        if (device is null)
            Console.Error.WriteLine("No authorized Android device connected.");
        return device;
    }

    public static AndroidDevice? PickOrThrow(AdbService adb)
        => PickOrWarn(adb);
}