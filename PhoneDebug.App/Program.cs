using System.Windows.Forms;
using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    /// <summary>The user gets a plain message; the stack trace goes to the log file.</summary>
    private static void Report(Exception? exception)
    {
        Log.Error("Unhandled error in the Windows app", exception);

        MessageBox.Show(
            $"{AppInfo.Name} hit an unexpected problem.\n\n" +
            $"Details were written to:\n{Log.FilePath}",
            AppInfo.Name,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }
}
