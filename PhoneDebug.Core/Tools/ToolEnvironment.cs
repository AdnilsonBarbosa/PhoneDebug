using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core.Tools;

public sealed record ToolStatus(string Name, string? Path, string? Version, string? Problem)
{
    public bool Found => Path is not null && Problem is null;

    /// <summary>Short line for the user, e.g. "ADB found" or "ADB not found".</summary>
    public string Summary => Found ? $"{Name} found" : $"{Name} not found";
}

/// <summary>
/// Answers "can Phone Debug actually work on this machine?" and, when it
/// cannot, what the user should do about it.
/// </summary>
public sealed class ToolEnvironment
{
    private ToolEnvironment(ToolStatus adb, ToolStatus scrcpy)
    {
        Adb = adb;
        Scrcpy = scrcpy;
    }

    public ToolStatus Adb { get; }
    public ToolStatus Scrcpy { get; }

    /// <summary>Everything except mirroring needs adb only.</summary>
    public bool CanUseDevices => Adb.Found;

    /// <summary>Mirroring needs both.</summary>
    public bool CanMirror => Adb.Found && Scrcpy.Found;

    public static ToolEnvironment Detect(bool probeVersions = true)
    {
        var adb = Inspect("ADB", ToolLocator.FindAdb(), probeVersions);
        var scrcpy = Inspect("scrcpy", ToolLocator.FindScrcpy(), probeVersions);

        Log.Info($"adb={adb.Path ?? "(missing)"} version={adb.Version ?? "?"} problem={adb.Problem ?? "none"}");
        Log.Info($"scrcpy={scrcpy.Path ?? "(missing)"} version={scrcpy.Version ?? "?"} problem={scrcpy.Problem ?? "none"}");

        return new ToolEnvironment(adb, scrcpy);
    }

    private static ToolStatus Inspect(string name, string? path, bool probe)
    {
        if (path is null)
            return new ToolStatus(name, null, null, $"{name} is not installed, or is not on the PATH.");

        if (!probe)
            return new ToolStatus(name, path, null, null);

        var version = ToolLocator.ProbeVersion(path);
        return version is null
            ? new ToolStatus(name, path, null, $"{name} was found but did not respond.")
            : new ToolStatus(name, path, version, null);
    }

    /// <summary>Install instructions shown when a tool is missing.</summary>
    public static IReadOnlyList<string> HowToInstall(string toolName) =>
        toolName.Equals("scrcpy", StringComparison.OrdinalIgnoreCase)
            ? new[]
            {
                "Install it with one of:",
                "  winget install Genymobile.scrcpy",
                "  scoop install scrcpy",
                "  choco install scrcpy",
                "",
                "Or copy scrcpy.exe into the \"tools\" folder next to Phone Debug.",
            }
            : new[]
            {
                "Install it with:",
                "  winget install Google.PlatformTools",
                "",
                "Or download the Android platform-tools and add the folder to your PATH:",
                "  https://developer.android.com/tools/releases/platform-tools",
                "",
                "Or copy adb.exe into the \"tools\" folder next to Phone Debug.",
            };
}
