using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli;

internal enum CliCommand
{
    Watch,
    Mirror,
    Devices,
    Info,
    Install,
    Logs,
    Screenshot,
    Reboot,
    Connect,
    Pair,
    Help,
    Version,
    Unknown,
}

/// <summary>Switches that can appear anywhere on the line.</summary>
internal sealed record CliOptions
{
    /// <summary>Set only when the user asked for a specific input mode.</summary>
    public MirrorInput? Input { get; init; }

    /// <summary>Smaller and slower picture, for a phone on Wi-Fi.</summary>
    public bool Light { get; init; }
}

internal sealed record ParsedCommand(
    CliCommand Command,
    string[] Arguments,
    string? Raw = null,
    CliOptions? Options = null)
{
    public CliOptions Settings => Options ?? new CliOptions();
}

internal static class CommandLine
{
    /// <summary>Maps the raw arguments onto a command. No side effects, so it can be tested.</summary>
    public static ParsedCommand Parse(string[] args)
    {
        MirrorInput? input = null;
        var light = false;
        var positional = new List<string>();

        foreach (var argument in args)
        {
            if (string.IsNullOrWhiteSpace(argument))
                continue;

            var trimmed = argument.Trim();
            switch (trimmed.ToLowerInvariant())
            {
                case "--standard" or "--sdk":
                    input = MirrorInput.Standard;
                    continue;

                case "--emulated" or "--uhid":
                    input = MirrorInput.Emulated;
                    continue;

                case "--light" or "--low":
                    light = true;
                    continue;
            }

            positional.Add(trimmed);
        }

        var options = new CliOptions { Input = input, Light = light };

        if (positional.Count == 0)
            return new ParsedCommand(CliCommand.Watch, [], null, options);

        var first = positional[0];
        var rest = positional.Skip(1).ToArray();

        var command = first.ToLowerInvariant() switch
        {
            "watch" => CliCommand.Watch,
            "mirror" or "screen" => CliCommand.Mirror,
            "devices" or "list" => CliCommand.Devices,
            "info" => CliCommand.Info,
            "install" => CliCommand.Install,
            "logs" or "logcat" => CliCommand.Logs,
            "screenshot" or "capture" => CliCommand.Screenshot,
            "reboot" or "restart" => CliCommand.Reboot,
            "connect" => CliCommand.Connect,
            "pair" => CliCommand.Pair,
            "help" or "--help" or "-h" or "-?" or "/?" => CliCommand.Help,
            "version" or "--version" or "-v" => CliCommand.Version,
            _ => CliCommand.Unknown,
        };

        return new ParsedCommand(command, rest, first, options);
    }
}
