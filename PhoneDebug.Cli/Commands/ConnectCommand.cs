using PhoneDebug.Core;
using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.Cli.Commands;

/// <summary>
/// "phone-debug connect": gets a phone attached when there is none, either by
/// explaining the USB steps or by setting up wireless debugging.
/// </summary>
internal static class ConnectCommand
{
    public static async Task<int> Run(PhoneDebugContext core, string[] args, CancellationToken ct)
    {
        var devices = core.Devices!;
        var pairing = core.Pairing!;

        // Skip the menu when the way to connect was given up front.
        switch (Mode(args))
        {
            case "qr":
                return await PairWithQr(pairing, ct).ConfigureAwait(false);

            case "code":
                return await PairWithCode(pairing, ct).ConfigureAwait(false);

            case "usb":
                Ui.Blank();
                PrintUsbSteps();
                return await WaitForDevice(devices, TimeSpan.FromMinutes(3), ct).ConfigureAwait(false);
        }

        // "phone-debug connect 192.168.0.10:5555" just connects.
        if (args.Length > 0)
        {
            var address = string.Join("", args).Trim();
            Ui.Blank();
            Ui.Line($"Connecting to {address}...");

            var direct = pairing.Connect(address);
            Ui.Blank();

            if (!direct.Success)
            {
                Ui.Error(direct.Message);
                Ui.Blank();
                return ExitCodes.Error;
            }

            Ui.Ok(direct.Message);
            Ui.Blank();
            return ExitCodes.Ok;
        }

        var connected = devices.ListDevices().Where(d => d.IsAuthorized).ToList();
        if (connected.Count > 0)
        {
            Ui.Blank();
            foreach (var device in connected)
            {
                devices.Adb.FillDetails(device);
                Ui.Ok($"{device.Name} is already connected");
            }

            Ui.Blank();
            Ui.Hint("Run \"phone-debug\" to open the screen.");
            Ui.Blank();
            return ExitCodes.Ok;
        }

        var choice = AskHowToConnect();
        return choice switch
        {
            1 => await ConnectByUsb(devices, ct).ConfigureAwait(false),
            2 => await PairWithQr(pairing, ct).ConfigureAwait(false),
            3 => await PairWithCode(pairing, ct).ConfigureAwait(false),
            _ => ExitCodes.Ok,
        };
    }

    /// <summary>Recognises "connect qr", "connect --qr", "connect wifi" and friends.</summary>
    private static string? Mode(string[] args)
    {
        if (args.Length == 0)
            return null;

        return args[0].Trim().TrimStart('-').ToLowerInvariant() switch
        {
            "qr" or "wifi" or "wi-fi" => "qr",
            "code" or "pair" => "code",
            "usb" or "cable" => "usb",
            _ => null,
        };
    }

    /// <summary>"phone-debug pair &lt;address&gt; &lt;code&gt;" for people who already know both.</summary>
    public static async Task<int> RunPair(PhoneDebugContext core, string[] args, CancellationToken ct)
    {
        if (args.Length < 2)
        {
            Ui.Blank();
            Ui.Error("Usage: phone-debug pair <ip:port> <6-digit code>");
            Ui.Blank();
            Ui.Hint("Both are shown on the phone under");
            Ui.Hint("Developer options > Wireless debugging > Pair device with pairing code.");
            Ui.Blank();
            Ui.Hint("Or run \"phone-debug connect\" to pair with a QR code instead.");
            Ui.Blank();
            return ExitCodes.Error;
        }

        var pairing = core.Pairing!;

        Ui.Blank();
        Ui.Line("Pairing...");

        var paired = pairing.PairWithCode(args[0], args[1]);
        if (!paired.Success)
        {
            Ui.Blank();
            Ui.Error(paired.Message);
            Ui.Blank();
            return ExitCodes.Error;
        }

        Ui.Ok(paired.Message);

        try
        {
            var connected = await pairing
                .WaitForDeviceAsync(new Progress<string>(m => Ui.Hint($"  {m}")), ct)
                .ConfigureAwait(false);

            return Report(connected);
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Ok;
        }
    }

    private static int AskHowToConnect()
    {
        Ui.Blank();
        Ui.Idle("No Android device connected.");
        Ui.Blank();

        if (Console.IsInputRedirected)
        {
            // No keyboard: print everything instead of asking.
            PrintUsbSteps();
            Ui.Line("Wi-Fi (Android 11 and newer):");
            Ui.Hint("  Run \"phone-debug connect\" in a terminal to pair with a QR code.");
            Ui.Blank();
            return 0;
        }

        Ui.Line("How do you want to connect?");
        Ui.Blank();
        Ui.Line("  [1] USB cable");
        Ui.Line("  [2] Wi-Fi - scan a QR code with the phone   (Android 11+)");
        Ui.Line("  [3] Wi-Fi - type the pairing code           (Android 11+)");
        Ui.Blank();
        Console.Write("Choose (1-3, or Enter to cancel): ");

        var input = Console.ReadLine();
        if (int.TryParse(input, out var choice) && choice >= 1 && choice <= 3)
            return choice;

        Ui.Blank();
        Ui.Hint("Cancelled.");
        Ui.Blank();
        return 0;
    }

    // ------------------------------------------------------------------ USB

    private static async Task<int> ConnectByUsb(DeviceService devices, CancellationToken ct)
    {
        Ui.Blank();
        PrintUsbSteps();

        return await WaitForDevice(devices, TimeSpan.FromMinutes(3), ct).ConfigureAwait(false);
    }

    private static void PrintUsbSteps()
    {
        Ui.Line("On the phone:");
        Ui.Hint("  1. Settings > About phone");
        Ui.Hint("     tap \"Build number\" seven times to unlock Developer options");
        Ui.Hint("  2. Settings > System > Developer options");
        Ui.Hint("     turn on \"USB debugging\"");
        Ui.Hint("  3. Plug the phone into this computer with a USB cable");
        Ui.Hint("  4. Unlock the phone and accept \"Allow USB debugging\"");
        Ui.Blank();
        Ui.Hint("Use a cable that carries data - some charging cables do not.");
        Ui.Blank();
    }

    // ------------------------------------------------------------------ QR

    private static async Task<int> PairWithQr(WirelessPairing pairing, CancellationToken ct)
    {
        var request = WirelessPairing.CreateRequest();

        // The code is tall, so it gets a screen of its own and is drawn
        // straight away - nothing to press first, nothing to scroll past.
        Ui.ClearScreen();
        Ui.Line("On the phone:  Developer options > Wireless debugging");
        Ui.Line("               > \"Pair device with QR code\", then scan this:");
        Ui.Blank();

        Ui.QrCode(QrCode.Encode(request.QrPayload));

        Ui.Blank();
        Ui.Hint("Both on the same Wi-Fi. Waiting for the phone to scan... (Ctrl+C to stop)");
        Ui.Flush();

        var progress = new Progress<string>(message => Ui.Hint($"  {message}"));

        try
        {
            var result = await pairing
                .PairWithQrAsync(request, progress, TimeSpan.FromMinutes(3), ct)
                .ConfigureAwait(false);

            return Report(result);
        }
        catch (OperationCanceledException)
        {
            Ui.Blank();
            Ui.Hint("Cancelled.");
            return ExitCodes.Ok;
        }
    }

    // ------------------------------------------------------------------ code

    private static async Task<int> PairWithCode(WirelessPairing pairing, CancellationToken ct)
    {
        Ui.Blank();
        Ui.Line("On the phone:");
        Ui.Hint("  Settings > System > Developer options > Wireless debugging");
        Ui.Hint("  turn it on, then tap \"Pair device with pairing code\"");
        Ui.Blank();
        Ui.Line("The phone shows an address and a six-digit code.");
        Ui.Blank();

        Console.Write("IP address & port (e.g. 192.168.0.10:37123): ");
        var address = Console.ReadLine() ?? "";

        Console.Write("Pairing code (6 digits): ");
        var code = Console.ReadLine() ?? "";

        Ui.Blank();
        Ui.Line("Pairing...");

        var paired = pairing.PairWithCode(address, code);
        if (!paired.Success)
        {
            Ui.Blank();
            Ui.Error(paired.Message);
            Ui.Blank();
            return ExitCodes.Error;
        }

        Ui.Ok(paired.Message);

        var progress = new Progress<string>(message => Ui.Hint($"  {message}"));

        try
        {
            var connected = await pairing.WaitForDeviceAsync(progress, ct).ConfigureAwait(false);
            return Report(connected);
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Ok;
        }
    }

    // ------------------------------------------------------------------ shared

    private static async Task<int> WaitForDevice(DeviceService devices, TimeSpan timeout, CancellationToken ct)
    {
        Ui.Idle("Waiting for the phone... (Ctrl+C to stop)");

        var deadline = DateTime.UtcNow + timeout;
        var warnedUnauthorized = false;

        while (DateTime.UtcNow < deadline)
        {
            if (ct.IsCancellationRequested)
                return ExitCodes.Ok;

            var all = devices.ListDevices();
            var device = all.FirstOrDefault(d => d.IsAuthorized);

            if (device is not null)
            {
                devices.Adb.FillDetails(device);
                Ui.Blank();
                Ui.Ok($"{device.Name} connected");
                Ui.Blank();
                Ui.Hint("Run \"phone-debug\" to open the screen.");
                Ui.Blank();
                return ExitCodes.Ok;
            }

            if (!warnedUnauthorized && all.Any(d => d.IsUnauthorized))
            {
                warnedUnauthorized = true;
                Ui.Blank();
                Ui.Line("Unlock the phone and accept \"Allow USB debugging\".");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ExitCodes.Ok;
            }
        }

        Ui.Blank();
        Ui.Warn("No phone showed up.");
        Ui.Blank();
        return ExitCodes.NoDevice;
    }

    private static int Report(OperationResult result)
    {
        Ui.Blank();

        if (!result.Success)
        {
            Ui.Error(result.Message);
            Ui.Blank();
            Ui.Hint("Try again with \"phone-debug connect\".");
            Ui.Blank();
            return ExitCodes.Error;
        }

        Ui.Ok(result.Message);
        Ui.Blank();
        Ui.Hint("Run \"phone-debug\" to open the screen.");
        Ui.Blank();
        return ExitCodes.Ok;
    }
}
