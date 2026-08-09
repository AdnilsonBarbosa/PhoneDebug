using System.Diagnostics;
using System.Text;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

public readonly record struct AdbResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;

    /// <summary>Whatever adb had to say, preferring stderr.</summary>
    public string Message =>
        !string.IsNullOrWhiteSpace(Error) ? Error :
        !string.IsNullOrWhiteSpace(Output) ? Output :
        "adb reported no output.";

    public static AdbResult Failure(string error) => new(-1, "", error);
}

/// <summary>
/// Thin, reliable wrapper around adb.exe. Everything else in Phone Debug
/// goes through this class - it is the only place that spawns adb.
/// </summary>
public sealed class AdbService
{
    private const int DefaultTimeoutMs = 60_000;

    public AdbService(string adbPath) => AdbPath = adbPath;

    public string AdbPath { get; }

    /// <summary>Starts adb with text pipes (UTF-8). The caller owns the process.</summary>
    public Process? StartProcess(params string[] args) => Start(binary: false, args);

    /// <summary>Starts adb without touching the output encoding - required for binary data.</summary>
    public Process? StartBinaryProcess(params string[] args) => Start(binary: true, args);

    private Process? Start(bool binary, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = AdbPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!binary)
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
        }

        // ArgumentList quotes each argument, so paths with spaces are safe.
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error($"Could not start adb ({AdbPath}) {string.Join(' ', args)}", ex);
            return null;
        }
    }

    public AdbResult Run(params string[] args) => Run(DefaultTimeoutMs, args);

    /// <summary>Runs adb to completion. Both pipes are drained while it runs, so long output cannot deadlock.</summary>
    public AdbResult Run(int timeoutMs, params string[] args)
    {
        using var process = StartProcess(args);
        if (process is null)
            return AdbResult.Failure("adb could not be started.");

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            Log.Warn($"adb {string.Join(' ', args)} timed out after {timeoutMs} ms");
            return AdbResult.Failure("adb did not respond in time.");
        }

        // Lets the async readers finish flushing.
        process.WaitForExit();

        var result = new AdbResult(process.ExitCode, stdout.Result.Trim(), stderr.Result.Trim());
        if (!result.Success)
            Log.Warn($"adb {string.Join(' ', args)} -> exit {result.ExitCode}: {result.Message}");

        return result;
    }

    /// <summary>Text output of a successful command, or null.</summary>
    public string? RunText(params string[] args)
    {
        var result = Run(args);
        return result.Success && result.Output.Length > 0 ? result.Output : null;
    }

    public List<AndroidDevice> GetDevices() => AdbOutputParser.ParseDevices(RunText("devices"));

    /// <summary>Reads every property in a single call instead of one adb per value.</summary>
    public Dictionary<string, string> GetProperties(string serial)
        => AdbOutputParser.ParseProperties(RunText("-s", serial, "shell", "getprop"));

    public string? GetProp(string serial, string key)
    {
        var value = RunText("-s", serial, "shell", "getprop", key);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Fills model, vendor and Android version. Safe to call on a device that just vanished.</summary>
    public void FillDetails(AndroidDevice device)
    {
        if (!device.IsAuthorized)
            return;

        var props = GetProperties(device.Serial);
        if (props.Count == 0)
            return;

        device.Model = Value(props, "ro.product.model");
        device.Manufacturer = Value(props, "ro.product.manufacturer");
        device.Brand = Value(props, "ro.product.brand");
        device.MarketName = Value(props,
            "ro.product.marketname",
            "ro.product.vendor.marketname",
            "ro.product.odm.marketname",
            "ro.config.marketing_name");
        device.AndroidVersion = Value(props, "ro.build.version.release");
        device.SdkLevel = Value(props, "ro.build.version.sdk");
        device.SecurityPatch = Value(props, "ro.build.version.security_patch");
        device.CpuAbi = Value(props, "ro.product.cpu.abi");
    }

    private static string? Value(Dictionary<string, string> props, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
