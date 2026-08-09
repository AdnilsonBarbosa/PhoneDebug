using System.IO.Compression;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

/// <summary>
/// Everything the CLI and the GUI do with a phone. No console output and no
/// user interface here - callers get results and decide how to show them.
/// </summary>
public sealed class DeviceService
{
    private const int InstallTimeoutMs = 10 * 60 * 1000;

    private readonly object _serialGate = new();
    private readonly Dictionary<string, string> _hardwareSerials = new(StringComparer.Ordinal);

    public DeviceService(AdbService adb) => Adb = adb;

    public AdbService Adb { get; }

    public IReadOnlyList<AndroidDevice> ListDevices(bool withDetails = false)
    {
        var devices = Deduplicate(Adb.GetDevices());

        if (withDetails)
        {
            foreach (var device in devices.Where(d => d.IsAuthorized))
                Adb.FillDetails(device);
        }

        return devices;
    }

    /// <summary>
    /// One phone can be attached more than once - over USB and Wi-Fi at the
    /// same time, or with a duplicate mDNS registration after pairing. Without
    /// this, a single phone looks like several devices and Phone Debug starts
    /// asking which one to use.
    /// </summary>
    private List<AndroidDevice> Deduplicate(List<AndroidDevice> devices)
    {
        if (devices.Count(d => d.IsAuthorized) < 2)
            return devices;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AndroidDevice>(devices.Count);

        foreach (var device in devices)
        {
            if (!device.IsAuthorized)
            {
                result.Add(device);
                continue;
            }

            var hardware = HardwareSerial(device.Serial);

            // Unknown hardware serial: keep it rather than hide a real device.
            if (hardware is null || seen.Add(hardware))
                result.Add(device);
            else
                Log.Info($"Hiding {device.Serial}: same phone as another connection ({hardware})");
        }

        return result;
    }

    private string? HardwareSerial(string transportSerial)
    {
        lock (_serialGate)
        {
            if (_hardwareSerials.TryGetValue(transportSerial, out var cached))
                return cached;
        }

        var value = Adb.GetProp(transportSerial, "ro.serialno");
        if (string.IsNullOrWhiteSpace(value))
            return null;

        lock (_serialGate)
        {
            // Wireless transports get a new name on every reconnect; do not let
            // the cache grow without bound.
            if (_hardwareSerials.Count > 32)
                _hardwareSerials.Clear();

            _hardwareSerials[transportSerial] = value;
        }

        return value;
    }

    public IReadOnlyList<AndroidDevice> ListAuthorizedDevices(bool withDetails = false)
        => ListDevices(withDetails).Where(d => d.IsAuthorized).ToList();

    /// <summary>
    /// Returns the requested device, or the only connected one. Returns null
    /// when nothing is usable or when the choice is ambiguous.
    /// </summary>
    public AndroidDevice? Resolve(string? preferredSerial, IReadOnlyList<AndroidDevice>? known = null)
    {
        var authorized = (known ?? ListDevices()).Where(d => d.IsAuthorized).ToList();

        var device = preferredSerial is not null
            ? authorized.FirstOrDefault(d => d.Serial == preferredSerial)
            : authorized.Count == 1
                ? authorized[0]
                : null;

        if (device is not null)
            Adb.FillDetails(device);

        return device;
    }

    public OperationResult Reboot(string serial)
    {
        var result = Adb.Run("-s", serial, "reboot");
        return result.Success
            ? OperationResult.Ok("Reboot requested.")
            : OperationResult.Fail(result.Message);
    }

    public LogcatSession? StartLogcat(string serial, bool clearFirst = false)
        => LogcatSession.Start(Adb, serial, clearFirst);

    /// <summary>Checks the file before adb is involved, so mistakes are reported instantly.</summary>
    public static ApkValidation ValidateApk(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ApkValidation.Invalid("No APK file was given.");

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"Invalid APK path '{path}': {ex.Message}");
            return ApkValidation.Invalid($"That is not a valid file path: {path}");
        }

        if (Directory.Exists(full))
            return ApkValidation.Invalid($"That is a folder, not an APK: {full}");

        if (!File.Exists(full))
            return ApkValidation.Invalid($"File not found: {full}");

        try
        {
            using var archive = ZipFile.OpenRead(full);
            if (archive.GetEntry("AndroidManifest.xml") is null)
                return ApkValidation.Invalid($"This file is not a valid APK (no AndroidManifest.xml): {Path.GetFileName(full)}");
        }
        catch (InvalidDataException)
        {
            return ApkValidation.Invalid($"This file is not a valid APK: {Path.GetFileName(full)}");
        }
        catch (Exception ex)
        {
            Log.Error($"Could not read APK {full}", ex);
            return ApkValidation.Invalid($"The APK could not be read: {ex.Message}");
        }

        return ApkValidation.Valid(full);
    }

    /// <summary>Installs (or reinstalls) an APK. Blocking - call it from a background thread.</summary>
    public OperationResult InstallApk(string serial, string apkPath)
    {
        var validation = ValidateApk(apkPath);
        if (!validation.Success || validation.FullPath is null)
            return validation;

        var full = validation.FullPath;
        Log.Info($"Installing {full} on {serial}");

        var result = Adb.Run(InstallTimeoutMs, "-s", serial, "install", "-r", full);

        // adb install prints "Failure [...]" and still exits 0 on some versions.
        var failed = !result.Success
            || result.Output.Contains("Failure", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("Failure", StringComparison.OrdinalIgnoreCase);

        if (failed)
            return OperationResult.Fail(AdbOutputParser.DescribeInstallFailure(result.Output, result.Error));

        return OperationResult.Ok($"{Path.GetFileName(full)} installed.");
    }

    /// <summary>
    /// Captures the screen. When <paramref name="targetPath"/> is null the file
    /// goes to Pictures\Phone Debug; a folder path is also accepted.
    /// </summary>
    public async Task<ScreenshotResult> CaptureScreenshotAsync(
        string serial,
        string? targetPath,
        CancellationToken cancellationToken = default)
    {
        string path;
        try
        {
            path = ResolveScreenshotPath(targetPath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot prepare screenshot path '{targetPath}'", ex);
            return new ScreenshotResult(false, $"The screenshot folder could not be used: {ex.Message}", null, 0);
        }

        using var process = Adb.StartBinaryProcess("-s", serial, "exec-out", "screencap", "-p");
        if (process is null)
            return new ScreenshotResult(false, "adb could not be started.", null, 0);

        using var buffer = new MemoryStream();
        try
        {
            await process.StandardOutput.BaseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
            return new ScreenshotResult(false, "Screenshot cancelled.", null, 0);
        }
        catch (Exception ex)
        {
            Log.Error("Screenshot failed", ex);
            return new ScreenshotResult(false, $"The screenshot failed: {ex.Message}", null, 0);
        }

        var bytes = buffer.ToArray();
        if (!LooksLikePng(bytes))
        {
            var error = process.StandardError.ReadToEnd().Trim();
            Log.Warn($"screencap produced {bytes.Length} bytes, error: {error}");
            return new ScreenshotResult(false,
                error.Length > 0 ? error : "The device did not return an image.", null, 0);
        }

        try
        {
            await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"Cannot write screenshot to {path}", ex);
            return new ScreenshotResult(false, $"The screenshot could not be saved: {ex.Message}", null, 0);
        }

        return new ScreenshotResult(true, $"Saved to {path}", path, bytes.Length);
    }

    internal static string ResolveScreenshotPath(string? targetPath)
    {
        var fileName = $"screenshot-{DateTime.Now:yyyy-MM-dd-HHmmss}.png";

        if (string.IsNullOrWhiteSpace(targetPath))
            return Path.Combine(AppInfo.ScreenshotDirectory, fileName);

        var full = Path.GetFullPath(targetPath);

        // A folder, or something that looks like one, receives a generated name.
        var looksLikeFolder = Directory.Exists(full)
            || targetPath.EndsWith(Path.DirectorySeparatorChar)
            || targetPath.EndsWith(Path.AltDirectorySeparatorChar)
            || string.IsNullOrEmpty(Path.GetExtension(full));

        return looksLikeFolder ? Path.Combine(full, fileName) : full;
    }

    private static bool LooksLikePng(byte[] data) =>
        data.Length > 100
        && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
}
