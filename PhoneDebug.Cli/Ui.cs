using System.Text;
using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.Cli;

/// <summary>
/// All console writing lives here so the output stays consistent, and so a
/// terminal that cannot render Unicode still gets readable text.
/// </summary>
internal static class Ui
{
    private static bool _unicode = true;

    public static string Check => _unicode ? "✓" : "+";      // v
    public static string Circle => _unicode ? "○" : "o";     // (
    public static string Dot => _unicode ? "●" : "*";        // *
    public static string Cross => _unicode ? "×" : "x";      // x

    public static void Configure()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = AppInfo.Name;
        }
        catch (Exception ex)
        {
            // Redirected output or a console host without UTF-8: fall back to ASCII.
            _unicode = false;
            Log.Warn($"Console setup fell back to ASCII: {ex.Message}");
        }
    }

    public static void Header()
    {
        Blank();
        Write(AppInfo.Title, ConsoleColor.Cyan);
        Blank();
    }

    public static void Blank() => Console.WriteLine();

    public static void Line(string text = "") => Console.WriteLine(text);

    public static void Ok(string text) => Write($"{Check} {text}", ConsoleColor.Green);

    public static void Missing(string text) => Write($"{Cross} {text}", ConsoleColor.Red);

    public static void Idle(string text) => Write($"{Circle} {text}", ConsoleColor.DarkGray);

    public static void Hint(string text) => Write(text, ConsoleColor.DarkGray);

    public static void Warn(string text) => Write(text, ConsoleColor.Yellow);

    public static void Error(string text) => Write(text, ConsoleColor.Red, error: true);

    /// <summary>Last-resort message: never shows a stack trace, always points at the log.</summary>
    public static void Fatal(string text, Exception? exception = null)
    {
        Log.Error(text, exception);

        Blank();
        Error($"{Cross} {text}");
        Hint($"Details: {Log.FilePath}");
        Blank();
    }

    /// <summary>
    /// Draws a QR code with half-block characters, two module rows per line.
    /// Colours are forced to black on white: phone cameras will not read an
    /// inverted code, whatever theme the terminal happens to use.
    /// </summary>
    public static void QrCode(QrCode qr)
    {
        if (!_unicode)
        {
            Warn("This terminal cannot draw a QR code.");
            Hint("Run \"phone-debug connect code\" instead.");
            return;
        }

        // Half the rows, because each line holds two module rows.
        var neededRows = (qr.Size + 1) / 2;
        if (!FitsOnScreen(qr.Size, neededRows))
        {
            Warn("This window is too small to show the whole QR code.");
            Hint($"Make it at least {qr.Size} columns wide and {neededRows + 6} lines tall,");
            Hint("or run \"phone-debug connect code\" and type the pairing code instead.");
            Blank();
        }

        var previousBackground = Console.BackgroundColor;
        var previousForeground = Console.ForegroundColor;

        try
        {
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;

            var line = new StringBuilder(qr.Size + 1);

            for (var y = 0; y < qr.Size; y += 2)
            {
                line.Clear();
                for (var x = 0; x < qr.Size; x++)
                {
                    var top = qr.IsDark(x, y);
                    var bottom = qr.IsDark(x, y + 1);

                    line.Append((top, bottom) switch
                    {
                        (true, true) => '█',    // full block
                        (true, false) => '▀',   // upper half
                        (false, true) => '▄',   // lower half
                        _ => ' ',
                    });
                }

                Console.WriteLine(line.ToString());
            }
        }
        finally
        {
            Console.BackgroundColor = previousBackground;
            Console.ForegroundColor = previousForeground;
            Console.ResetColor();
        }
    }

    private static bool FitsOnScreen(int columns, int rows)
    {
        try
        {
            // Leave room for the lines printed around the code.
            return Console.WindowWidth >= columns && Console.WindowHeight >= rows + 6;
        }
        catch (IOException)
        {
            return true;    // no real console attached; assume it is fine
        }
    }

    /// <summary>Pushes everything written so far to the terminal.</summary>
    public static void Flush()
    {
        try
        {
            Console.Out.Flush();
        }
        catch (IOException)
        {
            // Nothing listening on the other end.
        }
    }

    /// <summary>Wipes the screen so a tall QR code is not cut off by scrolling.</summary>
    public static void ClearScreen()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Clear();
        }
        catch (IOException)
        {
            // Some hosts do not support clearing; carry on.
        }
    }

    /// <summary>Shows a problem found on the phone, with what to do about it.</summary>
    public static void Problem(MirrorProblem problem)
    {
        Blank();
        Warn($"! {problem.Summary}");
        Blank();
        foreach (var step in problem.Steps)
            Hint(step);
        Blank();
    }

    private static void Write(string text, ConsoleColor color, bool error = false)
    {
        var writer = error ? Console.Error : Console.Out;
        try
        {
            Console.ForegroundColor = color;
            writer.WriteLine(text);
        }
        finally
        {
            Console.ResetColor();
        }
    }
}
