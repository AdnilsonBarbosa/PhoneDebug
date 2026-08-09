using System.Runtime.InteropServices;

namespace PhoneDebug.Cli;

internal static class ConsoleHost
{
    /// <summary>
    /// When phone-debug is double-clicked, Windows gives it a console of its
    /// own and closes it the moment the process ends - the user would never
    /// read the message. In that case only, wait for a key.
    /// </summary>
    public static void PauseIfOwnConsole()
    {
        if (!OwnsConsole())
            return;

        Ui.Blank();
        Ui.Hint("Press any key to close...");

        try
        {
            Console.ReadKey(intercept: true);
        }
        catch (InvalidOperationException)
        {
            // No keyboard attached (redirected input): nothing to wait for.
        }
    }

    private static bool OwnsConsole()
    {
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected || Console.IsOutputRedirected)
            return false;

        try
        {
            var processes = new uint[8];
            var count = GetConsoleProcessList(processes, (uint)processes.Length);

            // Exactly one process attached means the console was created for us.
            return count == 1;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetConsoleProcessList(uint[] processList, uint processCount);
}
