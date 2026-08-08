using System.Diagnostics;
using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class ScreenshotCommand
{
    public static async Task<int> Run(AdbService adb, CancellationToken ct)
    {
        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        var directory = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(directory);

        var fileName = $"screenshot-{DateTime.Now:yyyy-MM-dd-HHmmss}.png";
        var path = Path.Combine(directory, fileName);

        Console.WriteLine($"Capturing {device.DisplayName} -> {path}");

        using var process = adb.StartProcess("-s", device.Serial, "exec-out", "screencap", "-p");
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start adb.");
            return 1;
        }

        long size;
        try
        {
            using var file = File.Create(path);
            await process.StandardOutput.BaseStream.CopyToAsync(file, ct);
            size = file.Length;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { /* already exited */ }
            TryDelete(path);
            Console.WriteLine("Capture cancelled.");
            return 1;
        }

        await ConfigureExit(process, ct);

        if (process.ExitCode != 0 || size < 100)
        {
            Console.Error.WriteLine("Failed to capture screenshot (adb error).");
            TryDelete(path);
            return 1;
        }

        Console.WriteLine($"Saved {size:N0} bytes to {path}");
        return 0;
    }

    private static async Task ConfigureExit(Process process, CancellationToken ct)
    {
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { /* already exited */ }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}