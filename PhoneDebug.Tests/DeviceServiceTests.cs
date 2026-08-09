using System.IO.Compression;
using PhoneDebug.Core;
using PhoneDebug.Core.Services;
using Xunit;

namespace PhoneDebug.Tests;

public class DeviceServiceTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "phone-debug tests", Guid.NewGuid().ToString("N"));

    public DeviceServiceTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ValidateApk_rejects_a_missing_file()
    {
        var result = DeviceService.ValidateApk(Path.Combine(_workspace, "nope.apk"));

        Assert.False(result.Success);
        Assert.Contains("File not found", result.Message);
    }

    [Fact]
    public void ValidateApk_rejects_a_folder()
    {
        var result = DeviceService.ValidateApk(_workspace);

        Assert.False(result.Success);
        Assert.Contains("folder", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateApk_rejects_a_file_that_is_not_a_package()
    {
        var path = Path.Combine(_workspace, "broken app.apk");
        File.WriteAllText(path, "this is not an apk");

        var result = DeviceService.ValidateApk(path);

        Assert.False(result.Success);
        Assert.Contains("not a valid APK", result.Message);
    }

    [Fact]
    public void ValidateApk_rejects_a_zip_without_a_manifest()
    {
        var path = Path.Combine(_workspace, "empty.apk");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            archive.CreateEntry("readme.txt");

        var result = DeviceService.ValidateApk(path);

        Assert.False(result.Success);
        Assert.Contains("AndroidManifest", result.Message);
    }

    [Fact]
    public void ValidateApk_accepts_a_package_and_returns_a_full_path()
    {
        var path = Path.Combine(_workspace, "demo app.apk");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            archive.CreateEntry("AndroidManifest.xml");

        var result = DeviceService.ValidateApk(path);

        Assert.True(result.Success);
        Assert.Equal(Path.GetFullPath(path), result.FullPath);
    }

    [Fact]
    public void ValidateApk_rejects_an_empty_path()
        => Assert.False(DeviceService.ValidateApk("").Success);

    [Fact]
    public void Screenshots_default_to_the_pictures_folder()
    {
        var path = DeviceService.ResolveScreenshotPath(null);

        Assert.Equal(AppInfo.ScreenshotDirectory, Path.GetDirectoryName(path));
        Assert.EndsWith(".png", path);
    }

    [Fact]
    public void An_existing_folder_receives_a_generated_name()
    {
        var path = DeviceService.ResolveScreenshotPath(_workspace);

        Assert.Equal(_workspace, Path.GetDirectoryName(path));
        Assert.StartsWith("screenshot-", Path.GetFileName(path));
    }

    [Fact]
    public void An_explicit_file_name_is_kept()
    {
        var target = Path.Combine(_workspace, "my shot.png");

        Assert.Equal(target, DeviceService.ResolveScreenshotPath(target));
    }
}
