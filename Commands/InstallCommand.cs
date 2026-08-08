using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class InstallCommand
{
    public static async Task<int> Run(AdbService adb, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: phone-debug install <app.apk>");
            return 1;
        }

        var apk = args[0];
        if (!File.Exists(apk))
        {
            Console.Error.WriteLine($"File not found: {apk}");
            return 1;
        }

        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        Console.WriteLine($"Installing {Path.GetFileName(apk)} on {device.DisplayName}...");

        var full = Path.GetFullPath(apk);
        var result = await Task.Run(
            () => adb.RunCapture("-s", device.Serial, "install", "-r", full),
            ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            Console.Error.WriteLine(result.Error.Length > 0 ? result.Error : result.Output);
            return 1;
        }

        Console.WriteLine(result.Output);
        Console.WriteLine("Installed.");
        return 0;
    }
}