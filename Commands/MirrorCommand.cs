using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class MirrorCommand
{
    public static async Task<int> Run(AdbService adb, ScrcpyService scrcpy, CancellationToken ct)
    {
        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        Console.WriteLine($"Opening screen: {device.DisplayName}");

        var process = scrcpy.Start(device.Serial);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start scrcpy.");
            return 1;
        }

        using (process)
        {
            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C: let scrcpy close with the process kill.
                try { process.Kill(); } catch { /* already exited */ }
            }
        }

        return 0;
    }
}