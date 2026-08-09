using System.Reflection;

namespace PhoneDebug.Core;

/// <summary>
/// Product identity and the well-known folders Phone Debug writes to.
/// The version comes from the assembly, which is stamped from
/// Directory.Build.props - the single place where it is declared.
/// </summary>
public static class AppInfo
{
    public const string Name = "Phone Debug";

    public static string Version { get; } = ReadVersion();

    /// <summary>e.g. "Phone Debug 0.1.0"</summary>
    public static string Title => $"{Name} {Version}";

    /// <summary>Folder holding the running executable (and the bundled tools\ folder).</summary>
    public static string BaseDirectory => AppContext.BaseDirectory;

    /// <summary>%LOCALAPPDATA%\PhoneDebug</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PhoneDebug");

    /// <summary>%LOCALAPPDATA%\PhoneDebug\logs</summary>
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>Default place for screenshots: Pictures\Phone Debug.</summary>
    public static string ScreenshotDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Phone Debug");

    private static string ReadVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip any "+<build metadata>" suffix.
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        var version = typeof(AppInfo).Assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
