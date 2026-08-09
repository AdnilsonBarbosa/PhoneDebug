using System.Text;

namespace PhoneDebug.Core.Diagnostics;

/// <summary>
/// Technical log kept away from the user interface. Users see friendly
/// messages; stack traces and command output land here.
/// Never throws - logging must not be able to break the app.
/// </summary>
public static class Log
{
    private const long MaxBytes = 1024 * 1024;

    private static readonly object Gate = new();
    private static bool _failed;

    public static string FilePath { get; } = Path.Combine(AppInfo.LogDirectory, "phone-debug.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        if (_failed)
            return;

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppInfo.LogDirectory);
                Rotate();

                var text = new StringBuilder()
                    .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .Append("  ")
                    .Append(level.PadRight(5))
                    .Append("  ")
                    .Append(message);

                if (exception is not null)
                    text.AppendLine().Append(exception);

                File.AppendAllText(FilePath, text.AppendLine().ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Disk full, permissions, roaming profile issues - give up quietly.
            _failed = true;
        }
    }

    private static void Rotate()
    {
        var file = new FileInfo(FilePath);
        if (!file.Exists || file.Length < MaxBytes)
            return;

        var previous = FilePath + ".1";
        try
        {
            if (File.Exists(previous))
                File.Delete(previous);
            File.Move(FilePath, previous);
        }
        catch
        {
            // Keep appending to the current file if the rotation fails.
        }
    }
}
