using System.Diagnostics;
using System.Text;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

public enum MirrorInput
{
    /// <summary>scrcpy injects events through Android - fails on locked-down phones.</summary>
    Standard,

    /// <summary>scrcpy pretends to be a plugged-in USB keyboard and mouse (UHID).</summary>
    Emulated,
}

/// <summary>
/// Launches scrcpy. Command line options changed between scrcpy versions
/// (for example --turn-screen-on was dropped in 4.x), so the supported
/// options are read from "scrcpy --help" once and only known flags are used.
/// </summary>
public sealed class ScrcpyService
{
    private readonly Lazy<HashSet<string>> _options;

    public ScrcpyService(string scrcpyPath)
    {
        ScrcpyPath = scrcpyPath;
        _options = new Lazy<HashSet<string>>(ReadSupportedOptions, isThreadSafe: true);
    }

    public string ScrcpyPath { get; }

    public bool Supports(string option) => _options.Value.Contains(option);

    /// <summary>True when this scrcpy can emulate a physical keyboard and mouse.</summary>
    public bool SupportsEmulatedInput => Supports("--mouse") && Supports("--keyboard");

    /// <summary>Opens the phone screen. Returns null when scrcpy could not be started at all.</summary>
    public MirrorSession? StartMirror(
        AndroidDevice device,
        MirrorInput input = MirrorInput.Standard,
        bool lowBandwidth = false)
        => StartMirror(device.Serial, device.Name, input, lowBandwidth);

    public MirrorSession? StartMirror(
        string? serial,
        string? windowTitle = null,
        MirrorInput input = MirrorInput.Standard,
        bool lowBandwidth = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ScrcpyPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (!string.IsNullOrWhiteSpace(serial))
        {
            psi.ArgumentList.Add("--serial");
            psi.ArgumentList.Add(serial);
        }

        // Keep the phone awake while it is being controlled.
        if (Supports("--stay-awake"))
            psi.ArgumentList.Add("--stay-awake");

        // Some phones (Xiaomi and friends) refuse injected input. Emulating a
        // real USB keyboard and mouse goes around that restriction.
        if (input == MirrorInput.Emulated && SupportsEmulatedInput)
        {
            psi.ArgumentList.Add("--mouse=uhid");
            psi.ArgumentList.Add("--keyboard=uhid");
        }

        // Smaller, slower, cheaper - what a phone on Wi-Fi needs to stay smooth.
        if (lowBandwidth)
        {
            if (Supports("--max-size"))
                psi.ArgumentList.Add("--max-size=1280");

            if (Supports("--max-fps"))
                psi.ArgumentList.Add("--max-fps=30");

            if (Supports("--video-bit-rate"))
                psi.ArgumentList.Add("--video-bit-rate=6M");
            else if (Supports("--bit-rate"))
                psi.ArgumentList.Add("--bit-rate=6M");
        }

        if (Supports("--window-title"))
        {
            psi.ArgumentList.Add("--window-title");
            psi.ArgumentList.Add(string.IsNullOrWhiteSpace(windowTitle)
                ? AppInfo.Name
                : $"{AppInfo.Name} - {windowTitle}");
        }

        try
        {
            var process = Process.Start(psi);
            if (process is null)
            {
                Log.Error("Process.Start returned null for scrcpy");
                return null;
            }

            Log.Info($"scrcpy started (pid {process.Id}) for {serial ?? "(default device)"}");
            return new MirrorSession(process, serial);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to start scrcpy ({ScrcpyPath})", ex);
            return null;
        }
    }

    private HashSet<string> ReadSupportedOptions()
    {
        var supported = new HashSet<string>(StringComparer.Ordinal);

        var psi = new ProcessStartInfo
        {
            FileName = ScrcpyPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--help");

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
                return supported;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(10_000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                return supported;
            }

            process.WaitForExit();

            // scrcpy prints its help to stdout on some builds and stderr on others.
            foreach (var text in new[] { stdout.Result, stderr.Result })
                CollectOptions(text, supported);
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read scrcpy options: {ex.Message}");
        }

        return supported;
    }

    private static void CollectOptions(string help, HashSet<string> into)
    {
        if (string.IsNullOrEmpty(help))
            return;

        var index = 0;
        while ((index = help.IndexOf("--", index, StringComparison.Ordinal)) >= 0)
        {
            var end = index + 2;
            while (end < help.Length && (char.IsLetterOrDigit(help[end]) || help[end] == '-'))
                end++;

            if (end > index + 2)
                into.Add(help[index..end]);

            index = end;
        }
    }
}

/// <summary>A running scrcpy window.</summary>
public sealed class MirrorSession : IDisposable
{
    private readonly Process _process;
    private readonly Queue<string> _recentOutput = new();
    private readonly HashSet<string> _reportedProblems = [];
    private bool _disposed;

    internal MirrorSession(Process process, string? serial)
    {
        _process = process;
        Serial = serial;

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            Log.Info($"scrcpy exited with code {ExitCodeSafe()}");
            Exited?.Invoke(this, EventArgs.Empty);
        };

        // scrcpy logs continuously; draining the pipes keeps it from blocking.
        _process.OutputDataReceived += (_, e) => Capture(e.Data);
        _process.ErrorDataReceived += (_, e) => Capture(e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public string? Serial { get; }

    public event EventHandler? Exited;

    /// <summary>
    /// Raised once per distinct problem found in scrcpy's output - most often
    /// a phone that mirrors but refuses to be controlled.
    /// </summary>
    public event EventHandler<MirrorProblem>? ProblemDetected;

    public bool IsRunning
    {
        get
        {
            try { return !_disposed && !_process.HasExited; }
            catch { return false; }
        }
    }

    public int ExitCode => ExitCodeSafe();

    /// <summary>Last lines scrcpy printed - used to explain a failed launch.</summary>
    public string RecentOutput
    {
        get
        {
            lock (_recentOutput)
                return string.Join(Environment.NewLine, _recentOutput);
        }
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Stop();
        }
    }

    public void Stop()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not stop scrcpy: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        _process.Dispose();
    }

    private void Capture(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        Log.Info($"scrcpy: {line}");

        lock (_recentOutput)
        {
            _recentOutput.Enqueue(line);
            while (_recentOutput.Count > 20)
                _recentOutput.Dequeue();
        }

        var problem = MirrorDiagnostics.Detect(line);
        if (problem is null)
            return;

        bool isNew;
        lock (_reportedProblems)
            isNew = _reportedProblems.Add(problem.Key);

        if (isNew)
            ProblemDetected?.Invoke(this, problem);
    }

    private int ExitCodeSafe()
    {
        try { return _process.HasExited ? _process.ExitCode : 0; }
        catch { return -1; }
    }
}
