using System.Diagnostics;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core.Services;

/// <summary>
/// A running "adb logcat". Lines are delivered as events so the CLI can print
/// them and the GUI can append them to its log panel.
/// </summary>
public sealed class LogcatSession : IDisposable
{
    private readonly Process _process;
    private bool _disposed;

    private LogcatSession(Process process, string serial)
    {
        _process = process;
        Serial = serial;

        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => Stopped?.Invoke(this, EventArgs.Empty);
        _process.OutputDataReceived += (_, e) => Emit(e.Data, isError: false);
        _process.ErrorDataReceived += (_, e) => Emit(e.Data, isError: true);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public string Serial { get; }

    public event EventHandler<LogcatLine>? LineReceived;

    public event EventHandler? Stopped;

    public bool IsRunning
    {
        get
        {
            try { return !_disposed && !_process.HasExited; }
            catch { return false; }
        }
    }

    /// <summary>Starts logcat for a device. Returns null when adb could not be started.</summary>
    public static LogcatSession? Start(AdbService adb, string serial, bool clearFirst = false)
    {
        if (clearFirst)
            adb.Run("-s", serial, "logcat", "-c");

        var process = adb.StartProcess("-s", serial, "logcat");
        if (process is null)
            return null;

        Log.Info($"logcat started for {serial}");
        return new LogcatSession(process, serial);
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
                _process.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not stop logcat: {ex.Message}");
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

    private void Emit(string? text, bool isError)
    {
        if (text is null)
            return;

        LineReceived?.Invoke(this, new LogcatLine(text, isError));
    }
}

public readonly record struct LogcatLine(string Text, bool IsError);
