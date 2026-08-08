using System.Diagnostics;

namespace PhoneDebug.Services;

public sealed class ScrcpyService
{
    private readonly string _scrcpyPath;

    public ScrcpyService(string scrcpyPath) => _scrcpyPath = scrcpyPath;

    public Process? Start(string? deviceSerial)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _scrcpyPath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrWhiteSpace(deviceSerial))
        {
            psi.ArgumentList.Add("--serial");
            psi.ArgumentList.Add(deviceSerial);
        }

        try
        {
            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start scrcpy: {ex.Message}");
            return null;
        }
    }
}