using PhoneDebug.Core;
using PhoneDebug.Core.Tools;
using Xunit;

namespace PhoneDebug.Tests;

[Collection("environment")]
public class ToolLocatorTests : IDisposable
{
    private const string Variable = "PHONEDEBUG_ADB";

    private readonly string? _original = Environment.GetEnvironmentVariable(Variable);
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "phone-debug tests", Guid.NewGuid().ToString("N"));

    public ToolLocatorTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, _original);
        try { Directory.Delete(_workspace, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void An_override_that_exists_is_used()
    {
        var fake = Path.Combine(_workspace, "adb.exe");
        File.WriteAllText(fake, "");
        Environment.SetEnvironmentVariable(Variable, fake);

        Assert.Equal(fake, ToolLocator.FindAdb());
    }

    [Fact]
    public void An_override_that_is_wrong_does_not_silently_fall_back()
    {
        Environment.SetEnvironmentVariable(Variable, Path.Combine(_workspace, "missing.exe"));

        Assert.Null(ToolLocator.FindAdb());
    }

    [Fact]
    public void The_bundled_tools_folder_is_searched_first()
    {
        var directories = ToolLocator.BundledDirectories().ToList();

        Assert.Contains(Path.Combine(AppInfo.BaseDirectory, "tools"), directories);
    }
}

public class AppInfoTests
{
    [Fact]
    public void Version_comes_from_the_assembly()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);
        Assert.Equal($"Phone Debug {AppInfo.Version}", AppInfo.Title);
    }

    [Fact]
    public void Data_folders_live_under_local_app_data()
    {
        Assert.Contains("PhoneDebug", AppInfo.DataDirectory);
        Assert.StartsWith(AppInfo.DataDirectory, AppInfo.LogDirectory);
    }
}
