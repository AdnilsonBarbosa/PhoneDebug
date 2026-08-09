using System.Diagnostics;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core.Tools;

/// <summary>
/// Finds adb.exe and scrcpy.exe. Search order:
///   1. the "tools" folder shipped next to Phone Debug (portable install);
///   2. PHONEDEBUG_ADB / PHONEDEBUG_SCRCPY environment overrides;
///   3. PATH;
///   4. the usual winget / scoop / chocolatey / Android SDK locations.
/// </summary>
public static class ToolLocator
{
    public static string? FindAdb() => Find("adb", AdbCandidates());

    public static string? FindScrcpy() => Find("scrcpy", ScrcpyCandidates());

    /// <summary>Folders searched for bundled binaries, nearest first.</summary>
    public static IEnumerable<string> BundledDirectories()
    {
        var baseDir = AppInfo.BaseDirectory;
        yield return Path.Combine(baseDir, "tools");
        yield return Path.Combine(baseDir, "tools", "platform-tools");
        yield return Path.Combine(baseDir, "tools", "scrcpy");

        var parent = Path.GetDirectoryName(baseDir.TrimEnd(Path.DirectorySeparatorChar));
        if (parent is not null)
        {
            yield return Path.Combine(parent, "tools");
            yield return Path.Combine(parent, "tools", "platform-tools");
            yield return Path.Combine(parent, "tools", "scrcpy");
        }
    }

    internal static string? Find(string name, IEnumerable<string> candidates)
    {
        var fileName = name + ".exe";

        // An explicit override wins outright - including when it is wrong, so
        // that "use this exact binary" never silently falls back to another one.
        var overridden = Environment.GetEnvironmentVariable($"PHONEDEBUG_{name.ToUpperInvariant()}");
        if (!string.IsNullOrWhiteSpace(overridden))
            return File.Exists(overridden) ? overridden : null;

        foreach (var directory in BundledDirectories())
        {
            var bundled = SafeCombine(directory, fileName);
            if (bundled is not null && File.Exists(bundled))
                return bundled;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            // PATH entries are sometimes quoted; Path.Combine rejects those characters.
            var candidate = SafeCombine(directory.Trim().Trim('"'), fileName);
            if (candidate is not null && File.Exists(candidate))
                return candidate;
        }

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Unreadable path - keep looking.
            }
        }

        return null;
    }

    /// <summary>Runs "&lt;tool&gt; --version" and returns the first line, or null when it does not answer.</summary>
    public static string? ProbeVersion(string exePath, int timeoutMs = 15_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--version");

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return null;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                Log.Warn($"{exePath} --version timed out");
                return null;
            }

            var text = stdout.Result;
            if (string.IsNullOrWhiteSpace(text))
                text = stderr.Result;

            var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to probe {exePath}", ex);
            return null;
        }
    }

    private static string? SafeCombine(string directory, string fileName)
    {
        try
        {
            return Path.Combine(directory, fileName);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> AdbCandidates()
    {
        foreach (var root in new[]
                 {
                     Environment.GetEnvironmentVariable("ANDROID_HOME"),
                     Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(root))
                yield return Path.Combine(root, "platform-tools", "adb.exe");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
        yield return Path.Combine(AppInfo.DataDirectory, "platform-tools", "adb.exe");
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "adb.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "adb.exe");
        yield return @"C:\ProgramData\chocolatey\bin\adb.exe";

        foreach (var found in SearchWinGetPackages("adb.exe"))
            yield return found;
    }

    private static IEnumerable<string> ScrcpyCandidates()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims", "scrcpy.exe");
        yield return @"C:\ProgramData\chocolatey\bin\scrcpy.exe";
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "scrcpy.exe");
        yield return Path.Combine(AppInfo.DataDirectory, "scrcpy", "scrcpy.exe");

        foreach (var found in SearchWinGetPackages("scrcpy.exe"))
            yield return found;
    }

    /// <summary>winget portable installs land in Packages\&lt;package&gt;\&lt;version&gt;\&lt;exe&gt;.</summary>
    private static IEnumerable<string> SearchWinGetPackages(string fileName)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");

        List<string> results = [];
        try
        {
            if (!Directory.Exists(root))
                return results;

            foreach (var package in Directory.EnumerateDirectories(root))
            {
                var direct = Path.Combine(package, fileName);
                if (File.Exists(direct))
                    results.Add(direct);

                foreach (var version in Directory.EnumerateDirectories(package))
                {
                    var nested = Path.Combine(version, fileName);
                    if (File.Exists(nested))
                        results.Add(nested);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not scan winget packages: {ex.Message}");
        }

        return results;
    }
}
