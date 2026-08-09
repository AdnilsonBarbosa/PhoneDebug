using System.IO.Compression;
using PhoneDebug.Core.Tools;
using Xunit;

namespace PhoneDebug.Tests;

public class ToolDownloaderTests : IDisposable
{
    private readonly string _workspace =
        Path.Combine(Path.GetTempPath(), "phone-debug tests", Guid.NewGuid().ToString("N"));

    public ToolDownloaderTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        try { Directory.Delete(_workspace, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Extracting_strips_the_archive_top_folder()
    {
        var zip = MakeZip("platform-tools/adb.exe", "platform-tools/AdbWinApi.dll");
        var destination = Path.Combine(_workspace, "out");

        ToolDownloader.ExtractClipboard(zip, destination, stripFirstLevel: true);

        Assert.True(File.Exists(Path.Combine(destination, "adb.exe")));
        Assert.True(File.Exists(Path.Combine(destination, "AdbWinApi.dll")));
    }

    [Fact]
    public void Nested_files_keep_their_structure_after_stripping()
    {
        var zip = MakeZip("scrcpy-win64-v3.2/sub/folder.txt");
        var destination = Path.Combine(_workspace, "out");

        ToolDownloader.ExtractClipboard(zip, destination, stripFirstLevel: true);

        Assert.True(File.Exists(Path.Combine(destination, "sub", "folder.txt")));
    }

    [Fact]
    public void Without_stripping_the_top_folder_is_kept()
    {
        var zip = MakeZip("scrcpy-win64-v3.2/scrcpy.exe");
        var destination = Path.Combine(_workspace, "out");

        ToolDownloader.ExtractClipboard(zip, destination, stripFirstLevel: false);

        Assert.True(File.Exists(Path.Combine(destination, "scrcpy-win64-v3.2", "scrcpy.exe")));
    }

    [Fact]
    public void A_path_traversal_entry_is_rejected()
    {
        var zip = MakeZip("../evil.exe");
        var destination = Path.Combine(_workspace, "out");

        Assert.Throws<InvalidOperationException>(
            () => ToolDownloader.ExtractClipboard(zip, destination, stripFirstLevel: false));
    }

    [Fact]
    public void The_win64_asset_is_found_in_the_release_payload()
    {
        const string payload = """
            {
              "assets": [
                { "name": "scrcpy-win32-v3.2.zip", "browser_download_url": "https://x/scrcpy-win32-v3.2.zip" },
                { "name": "scrcpy-win64-v3.2.zip", "browser_download_url": "https://x/scrcpy-win64-v3.2.zip" },
                { "name": "scrcpy-server", "browser_download_url": "https://x/scrcpy-server" }
              ]
            }
            """;

        Assert.Equal("https://x/scrcpy-win64-v3.2.zip", ToolDownloader.FindScrcpyWin64Url(payload));
    }

    [Fact]
    public void A_release_without_a_win64_asset_yields_nothing()
    {
        const string payload = """
            {
              "assets": [
                { "name": "scrcpy-win32-v3.2.zip", "browser_download_url": "https://x/scrcpy-win32-v3.2.zip" }
              ]
            }
            """;

        Assert.Null(ToolDownloader.FindScrcpyWin64Url(payload));
    }

    [Fact]
    public void Malformed_json_yields_nothing_instead_of_throwing()
    {
        Assert.Null(ToolDownloader.FindScrcpyWin64Url("not json"));
    }

    private string MakeZip(params string[] entries)
    {
        var zipPath = Path.Combine(_workspace, $"{Guid.NewGuid():N}.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                var item = archive.CreateEntry(entry);
                using var writer = new StreamWriter(item.Open());
                writer.Write("hello");
            }
        }

        return zipPath;
    }
}