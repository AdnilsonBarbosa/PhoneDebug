using PhoneDebug.Core.Models;
using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

/// <summary>
/// Chooses the device a one-shot command should act on, and explains clearly
/// when there is nothing to act on.
/// </summary>
internal static class DevicePicker
{
    public static AndroidDevice? Pick(DeviceService devices)
    {
        var all = devices.ListDevices();
        var authorized = all.Where(d => d.IsAuthorized).ToList();

        if (authorized.Count == 0)
        {
            ExplainNoDevice(all);
            return null;
        }

        if (authorized.Count == 1)
        {
            devices.Adb.FillDetails(authorized[0]);
            return authorized[0];
        }

        return Prompt(devices, authorized);
    }

    public static void ExplainNoDevice(IReadOnlyList<AndroidDevice> all)
    {
        Ui.Blank();

        if (all.Any(d => d.IsUnauthorized))
        {
            Ui.Line("Android device detected.");
            Ui.Blank();
            Ui.Line("Unlock your phone and accept:");
            Ui.Line("\"Allow USB debugging\"");
            Ui.Blank();
            return;
        }

        if (all.Any(d => d.IsOffline))
        {
            Ui.Warn("The device is offline.");
            Ui.Blank();
            Ui.Hint("Unplug and reconnect the cable, or turn USB debugging off and on again.");
            Ui.Blank();
            return;
        }

        Ui.Idle("No Android device connected.");
        Ui.Blank();
        Ui.Hint("Run \"phone-debug connect\" to be walked through it,");
        Ui.Hint("over USB or over Wi-Fi with a QR code.");
        Ui.Blank();
    }

    private static AndroidDevice? Prompt(DeviceService devices, List<AndroidDevice> authorized)
    {
        foreach (var device in authorized)
            devices.Adb.FillDetails(device);

        // No keyboard available (piped input): fall back to the first device.
        if (Console.IsInputRedirected)
        {
            Ui.Warn($"Several devices connected; using {authorized[0].DisplayName}.");
            return authorized[0];
        }

        while (true)
        {
            Ui.Blank();
            Ui.Line("Several Android devices are connected:");
            for (var i = 0; i < authorized.Count; i++)
                Ui.Line($"  [{i + 1}] {authorized[i].DisplayName}");

            Ui.Blank();
            Console.Write($"Choose a device (1-{authorized.Count}, or Enter to cancel): ");

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
                return null;

            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= authorized.Count)
                return authorized[choice - 1];

            Ui.Warn("That is not one of the options.");
        }
    }
}
