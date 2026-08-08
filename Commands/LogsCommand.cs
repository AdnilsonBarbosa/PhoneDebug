using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class LogsCommand
{
    public static async Task<int> Run(AdbService adb, CancellationToken ct)
    {
        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        Console.WriteLine($"Logcat: {device.Serial}");
        Console.WriteLine("Ctrl+C to stop.");

        using var process = adb.StartProcess("-s", device.Serial, "logcat");
        if (process is null)
        {
            Console.Error.WriteLine("Failed to start adb.");
            return 1;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) Console.Error.WriteLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
            catch
            {
                // already exited
            }
        }

        return 0;
    }
}