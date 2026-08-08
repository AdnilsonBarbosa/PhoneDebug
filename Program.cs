using System.Text;
using PhoneDebug.Commands;
using PhoneDebug.Services;

namespace PhoneDebug;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Phone Debug Terminal";

        WriteBanner();
        var command = args.Length > 0 ? args[0] : "watch";

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var adbPath = ResolveAdb();
        if (adbPath is null)
            return 1;

        var adb = new AdbService(adbPath);

        switch (command)
        {
            case "watch" when args.Length == 0:
                return await Watch(adb, cts.Token);

            case "mirror":
                return await Mirror(adb, cts.Token);

            case "devices":
                return DevicesCommand.Run(adb);

            case "logs":
                return await LogsCommand.Run(adb, cts.Token);

            case "info":
                return InfoCommand.Run(adb);

            case "screenshot":
                return await ScreenshotCommand.Run(adb, cts.Token);

            case "install":
                return await InstallCommand.Run(adb, args.Skip(1).ToArray(), cts.Token);

            case "reboot":
                return RebootCommand.Run(adb);

            case "help" or "-h" or "--help":
                return ShowHelp();

            default:
                Console.WriteLine($"Unknown command '{command}'.");
                ShowHelp();
                return 1;
        }
    }

    private static async Task<int> Watch(AdbService adb, CancellationToken ct)
    {
        var scrcpy = ResolveScrcpy();
        if (scrcpy is null)
            return 1;

        var watcher = new DeviceWatcher(adb, scrcpy);
        await watcher.RunAsync(ct);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("Bye.");
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> Mirror(AdbService adb, CancellationToken ct)
    {
        var scrcpy = ResolveScrcpy();
        if (scrcpy is null)
            return 1;

        return await MirrorCommand.Run(adb, scrcpy, ct);
    }

    private static string? ResolveAdb()
    {
        Console.WriteLine("Checking ADB...");

        var adbPath = FindExe("adb", AdbCandidates());
        if (adbPath is null)
        {
            WriteError("ADB not found.");
            Console.WriteLine("Install Android platform-tools and add adb.exe to your PATH:");
            Console.WriteLine("  https://dl.google.com/android/repository/platform-tools-latest-windows.zip");
            return null;
        }

        if (!RunOk(adbPath, "--version"))
        {
            WriteError("ADB is not responding.");
            return null;
        }

        Console.WriteLine("ADB: OK");
        Console.WriteLine();
        return adbPath;
    }

    private static ScrcpyService? ResolveScrcpy()
    {
        Console.WriteLine("Checking scrcpy...");

        var scrcpyPath = FindExe("scrcpy", ScrcpyCandidates());
        if (scrcpyPath is null)
        {
            WriteError("scrcpy not found.");
            Console.WriteLine();
            Console.WriteLine("Install it with one of:");
            Console.WriteLine("  winget install Genymobile.scrcpy");
            Console.WriteLine("  scoop install scrcpy");
            Console.WriteLine("  choco install scrcpy --yes");
            return null;
        }

        if (!RunOk(scrcpyPath, "--version"))
        {
            WriteError("scrcpy is not responding.");
            return null;
        }

        Console.WriteLine("scrcpy: OK");
        Console.WriteLine();
        return new ScrcpyService(scrcpyPath);
    }

    private static int ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  phone-debug               watch for a device and open the screen");
        Console.WriteLine("  phone-debug devices       list connected devices");
        Console.WriteLine("  phone-debug mirror        open the screen directly");
        Console.WriteLine("  phone-debug logs          stream adb logcat");
        Console.WriteLine("  phone-debug info          show device details");
        Console.WriteLine("  phone-debug screenshot    save a capture to screenshots\\");
        Console.WriteLine("  phone-debug install <apk> install an APK (-r)");
        Console.WriteLine("  phone-debug reboot        reboot the device");
        Console.WriteLine();
        return 0;
    }

    private static bool RunOk(string exe, params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        try
        {
            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return false;

            process.WaitForExit(10_000);
            if (!process.HasExited)
            {
                try { process.Kill(); } catch { /* already exited */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExe(string name, IEnumerable<string?> extraCandidates)
    {
        var fileName = name + ".exe";

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            string full;
            try { full = Path.Combine(dir.Trim(), fileName); }
            catch { continue; }

            if (File.Exists(full))
                return full;
        }

        foreach (var candidate in extraCandidates)
        {
            if (candidate is null)
                continue;
            try
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }

    private static IEnumerable<string> AdbCandidates()
    {
        foreach (var env in new[]
                 {
                     Environment.GetEnvironmentVariable("ANDROID_HOME"),
                     Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(env))
                yield return Path.Combine(env, "platform-tools", "adb.exe");
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Android", "Sdk", "platform-tools", "adb.exe");
    }

    private static IEnumerable<string> ScrcpyCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        yield return Path.Combine(home, "scoop", "shims", "scrcpy.exe");
        yield return Path.Combine("C:\\ProgramData\\chocolatey\\bin\\scrcpy.exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Links", "scrcpy.exe");

        // Winget portable installs land in Packages\<Package>\<version>\scrcpy.exe.
        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(packagesRoot))
        {
            foreach (var dir in Directory.EnumerateDirectories(packagesRoot))
            {
                var exe = Path.Combine(dir, "scrcpy.exe");
                if (File.Exists(exe))
                    yield return exe;

                foreach (var inner in Directory.EnumerateDirectories(dir))
                {
                    var innerExe = Path.Combine(inner, "scrcpy.exe");
                    if (File.Exists(innerExe))
                        yield return innerExe;
                }
            }
        }
    }

    private static void WriteBanner()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("PHONE DEBUG");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void WriteError(string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}