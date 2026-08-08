using System.Diagnostics;
using System.Text;
using PhoneDebug.Models;

namespace PhoneDebug.Services;

public sealed class AdbService
{
    private readonly string _adbPath;

    public AdbService(string adbPath) => _adbPath = adbPath;

    public Process? StartProcess(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _adbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Runs an adb command to completion and returns trimmed stdout (or null on failure).
    /// </summary>
    public string? Run(params string[] args)
    {
        var (exitCode, output, _) = RunCapture(args);
        return exitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
    }

    /// <summary>
    /// Runs an adb command to completion and returns exit code plus stdout/stderr.
    /// </summary>
    public (int ExitCode, string Output, string Error) RunCapture(params string[] args)
    {
        using var process = StartProcess(args);
        if (process is null)
            return (-1, "", "failed to start adb");

        process.WaitForExit(300_000);
        if (!process.HasExited)
        {
            try { process.Kill(); } catch { /* already exited */ }
            return (-2, "", "timed out");
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        return (process.ExitCode, output, error);
    }

    public List<AndroidDevice> GetDevices()
    {
        var devices = new List<AndroidDevice>();
        var output = Run("devices");

        if (output is null)
            return devices;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith('*')) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            devices.Add(new AndroidDevice
            {
                Serial = parts[0],
                State = parts[1].ToLowerInvariant(),
            });
        }

        return devices;
    }

    public void FillDetails(AndroidDevice device)
    {
        device.Model = Clean(Run("-s", device.Serial, "shell", "getprop", "ro.product.model"));
        device.AndroidVersion = Clean(Run("-s", device.Serial, "shell", "getprop", "ro.build.version.release"));
    }

    public string? GetProp(string serial, string key)
        => Clean(Run("-s", serial, "shell", "getprop", key));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}