using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core.Tools;

/// <summary>
/// Downloads adb and scrcpy from their official sources on first run, so a
/// freshly unpacked Phone Debug works with nothing else installed.
///
/// adb cannot be redistributed under Google's Android SDK licence and scrcpy's
/// official Windows build ships FFmpeg/SDL under their own licences, so the
/// binaries are fetched straight from Google and GitHub on the user's machine
/// instead of being bundled with the release. This pages keeps the licence
/// position simple: Phone Debug only redistributes itself.
/// </summary>
public static class ToolDownloader
{
    // Both URLs stay stable and always point at the current versions.
    private const string AdbZipUrl =
        "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

    private const string ScrcpyReleasesApi =
        "https://api.github.com/repos/Genymobile/scrcpy/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"PhoneDebug/{AppInfo.Version}");
        return http;
    }

    /// <summary>Result of a download pass. The problem is text, never an exception.</summary>
    public sealed record Outcome(bool AdbDownloaded, bool ScrcpyDownloaded)
    {
        public bool AnythingDownloaded => AdbDownloaded || ScrcpyDownloaded;
    }

    /// <summary>
    /// Downloads whichever of adb / scrcpy the locator cannot find. Idempotent:
    /// nothing is downloaded when both are already present.
    /// </summary>
    public static async Task<Outcome> DownloadMissingAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var needAdb = ToolLocator.FindAdb() is null;
        var needScrcpy = ToolLocator.FindScrcpy() is null;

        if (!needAdb && !needScrcpy)
            return new Outcome(false, false);

        var adb = needAdb && await TryDownloadAdbAsync(progress, cancellationToken).ConfigureAwait(false);
        var scrcpy = needScrcpy && await TryDownloadScrcpyAsync(progress, cancellationToken).ConfigureAwait(false);

        return new Outcome(adb, scrcpy);
    }

    private static async Task<bool> TryDownloadAdbAsync(
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report("Downloading adb from Google (Android platform-tools)...");
            var target = Path.Combine(Path.GetTempPath(), $"phone-debug-adb-{Guid.NewGuid():N}.zip");

            await DownloadFileAsync(AdbZipUrl, target, progress, cancellationToken).ConfigureAwait(false);

            var platformTools = Path.Combine(AppInfo.BaseDirectory, "tools", "platform-tools");
            Directory.CreateDirectory(platformTools);
            ExtractClipboard(target, platformTools, stripFirstLevel: true);

            try { File.Delete(target); } catch { /* best effort */ }

            var adb = Path.Combine(platformTools, "adb.exe");
            if (File.Exists(adb))
            {
                Log.Info($"adb downloaded to {adb}");
                return true;
            }

            Log.Error("adb.zip was extracted but adb.exe is missing");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not download adb: {ex.Message}", ex);
            progress?.Report($"adb download failed: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> TryDownloadScrcpyAsync(
        IProgress<string>? progress, CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report("Looking up the newest scrcpy release on GitHub...");
            var scrcpyUrl = await FindLatestScrcpyWin64UrlAsync(cancellationToken).ConfigureAwait(false);
            if (scrcpyUrl is null)
            {
                Log.Error("No scrcpy-win64-*.zip asset in the latest scrcpy release");
                progress?.Report("Could not find a scrcpy download for Windows.");
                return false;
            }

            progress?.Report("Downloading scrcpy from GitHub...");
            var zip = Path.Combine(Path.GetTempPath(), $"phone-debug-scrcpy-{Guid.NewGuid():N}.zip");
            await DownloadFileAsync(scrcpyUrl, zip, progress, cancellationToken).ConfigureAwait(false);

            var scrcpyDir = Path.Combine(AppInfo.BaseDirectory, "tools", "scrcpy");
            Directory.CreateDirectory(scrcpyDir);
            ExtractClipboard(zip, scrcpyDir, stripFirstLevel: true);

            try { File.Delete(zip); } catch { /* best effort */ }

            var scrcpy = Path.Combine(scrcpyDir, "scrcpy.exe");
            if (File.Exists(scrcpy))
            {
                Log.Info($"scrcpy downloaded to {scrcpy}");
                return true;
            }

            Log.Error("scrcpy.zip was extracted but scrcpy.exe is missing");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not download scrcpy: {ex.Message}", ex);
            progress?.Report($"scrcpy download failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Downloads a file, reporting progress in whole percent steps.</summary>
    private static async Task DownloadFileAsync(
        string url, string destination, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        var reported = -1;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = File.Create(destination);

        var buffer = new byte[64 * 1024];
        long readSoFar = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readSoFar += read;

            if (total is > 0)
            {
                var percent = (int)(readSoFar * 100 / total);
                if (percent != reported)
                {
                    reported = percent;
                    progress?.Report($"  {percent}%");
                }
            }
        }
    }

    /// <summary>
    /// Extracts a zip into a destination, stripping the single top-level
    /// folder that these archives wrap everything in. Everything inside is
    /// kept: adb ships DLLs it needs at runtime, and scrcpy cannot run without
    /// the whole set of files that come with it (its pushable server in
    /// particular). These are official files from Google / the scrcpy release,
    /// so no filtering is needed.
    /// </summary>
    internal static void ExtractClipboard(string zipPath, string destination, bool stripFirstLevel)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                continue;

            var segments = entry.FullName.Split('/');
            var kept = stripFirstLevel && segments.Length > 1
                ? segments.Skip(1).ToArray()
                : segments;
            if (kept.Length == 0)
                continue;

            var targetPath = Path.Combine(destination, Path.Combine(kept));
            var fullPath = Path.GetFullPath(targetPath);
            var destinationRoot = Path.GetFullPath(destination);
            if (!fullPath.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Zip entry escapes the tools folder.");

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            entry.ExtractToFile(fullPath, overwrite: true);
        }
    }

    private static async Task<string?> FindLatestScrcpyWin64UrlAsync(CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(ScrcpyReleasesApi, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return FindScrcpyWin64Url(json);
    }

    /// <summary>Parses the GitHub "latest release" payload and finds the win64 zip URL.</summary>
    internal static string? FindScrcpyWin64Url(string releaseJson)
    {
        try
        {
            using var document = JsonDocument.Parse(releaseJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("assets", out var assets))
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement))
                    continue;

                var name = nameElement.GetString();
                if (name is null || !name.StartsWith("scrcpy-win64-", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!asset.TryGetProperty("browser_download_url", out var urlElement))
                    continue;

                var url = urlElement.GetString();
                if (url is null)
                    continue;

                return url;
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read the scrcpy release payload: {ex.Message}");
            return null;
        }
    }
}