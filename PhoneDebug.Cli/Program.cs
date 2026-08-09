using PhoneDebug.Cli.Commands;
using PhoneDebug.Core;
using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.Cli;

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int Error = 1;
    public const int MissingTool = 2;
    public const int NoDevice = 3;
}

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Ui.Configure();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var exitCode = ExitCodes.Error;
        try
        {
            exitCode = await Dispatch(CommandLine.Parse(args), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            exitCode = ExitCodes.Ok;
        }
        catch (Exception ex)
        {
            Ui.Fatal("Phone Debug hit an unexpected problem.", ex);
        }
        finally
        {
            ConsoleHost.PauseIfOwnConsole();
        }

        return exitCode;
    }

    private static async Task<int> Dispatch(ParsedCommand parsed, CancellationToken ct)
    {
        switch (parsed.Command)
        {
            case CliCommand.Help:
                HelpCommand.Show();
                return ExitCodes.Ok;

            case CliCommand.Version:
                Console.WriteLine(AppInfo.Title);
                return ExitCodes.Ok;

            case CliCommand.Unknown:
                Ui.Header();
                Ui.Error($"Unknown command: {parsed.Raw}");
                Ui.Blank();
                HelpCommand.ShowUsage();
                return ExitCodes.Error;
        }

        // Everything below needs adb, and mirroring also needs scrcpy.
        var needsMirroring = parsed.Command is CliCommand.Watch or CliCommand.Mirror;
        var showChecks = needsMirroring;

        if (showChecks)
            Ui.Header();

        var core = PhoneDebugContext.Create();

        if (!Report(core.Tools.Adb, showChecks))
            return ExitCodes.MissingTool;

        if (needsMirroring && !Report(core.Tools.Scrcpy, showChecks))
            return ExitCodes.MissingTool;

        var devices = core.Devices!;

        return parsed.Command switch
        {
            CliCommand.Watch => await WatchCommand.Run(core, parsed.Settings, ct).ConfigureAwait(false),
            CliCommand.Mirror => await MirrorCommand.Run(core, parsed.Settings, ct).ConfigureAwait(false),
            CliCommand.Devices => DevicesCommand.Run(devices),
            CliCommand.Info => InfoCommand.Run(devices),
            CliCommand.Install => await InstallCommand.Run(devices, parsed.Arguments, ct).ConfigureAwait(false),
            CliCommand.Logs => await LogsCommand.Run(devices, ct).ConfigureAwait(false),
            CliCommand.Screenshot => await ScreenshotCommand.Run(devices, parsed.Arguments, ct).ConfigureAwait(false),
            CliCommand.Reboot => RebootCommand.Run(devices),
            CliCommand.Connect => await ConnectCommand.Run(core, parsed.Arguments, ct).ConfigureAwait(false),
            CliCommand.Pair => await ConnectCommand.RunPair(core, parsed.Arguments, ct).ConfigureAwait(false),
            _ => ExitCodes.Error,
        };
    }

    /// <summary>Prints the "✓ ADB found" line, or explains how to install what is missing.</summary>
    private static bool Report(ToolStatus tool, bool showWhenFound)
    {
        if (tool.Found)
        {
            if (showWhenFound)
                Ui.Ok(tool.Summary);
            return true;
        }

        if (!showWhenFound)
            Ui.Header();

        Ui.Missing(tool.Summary);
        Ui.Blank();

        if (tool.Problem is not null)
            Ui.Line(tool.Problem);

        Ui.Blank();
        foreach (var line in ToolEnvironment.HowToInstall(tool.Name))
            Ui.Hint(line);

        Ui.Blank();
        Ui.Hint("Then open a new terminal and run phone-debug again.");
        Ui.Blank();

        Log.Warn($"Missing tool: {tool.Name}");
        return false;
    }
}
