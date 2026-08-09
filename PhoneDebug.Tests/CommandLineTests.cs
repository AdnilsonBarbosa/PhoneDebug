using PhoneDebug.Cli;
using PhoneDebug.Core.Services;
using Xunit;

namespace PhoneDebug.Tests;

public class CommandLineTests
{
    [Fact]
    public void No_arguments_means_watch()
    {
        var parsed = CommandLine.Parse([]);

        Assert.Equal(CliCommand.Watch, parsed.Command);
        Assert.Empty(parsed.Arguments);
    }

    // The enum is internal, so the expectation travels as its name.
    [Theory]
    [InlineData("mirror", "Mirror")]
    [InlineData("screen", "Mirror")]
    [InlineData("devices", "Devices")]
    [InlineData("info", "Info")]
    [InlineData("install", "Install")]
    [InlineData("logs", "Logs")]
    [InlineData("logcat", "Logs")]
    [InlineData("screenshot", "Screenshot")]
    [InlineData("reboot", "Reboot")]
    [InlineData("MIRROR", "Mirror")]
    public void Recognises_commands(string argument, string expected)
        => Assert.Equal(expected, CommandLine.Parse([argument]).Command.ToString());

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    [InlineData("-?")]
    public void Recognises_help(string argument)
        => Assert.Equal(CliCommand.Help, CommandLine.Parse([argument]).Command);

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("version")]
    public void Recognises_version(string argument)
        => Assert.Equal(CliCommand.Version, CommandLine.Parse([argument]).Command);

    [Fact]
    public void Unknown_commands_keep_the_original_text()
    {
        var parsed = CommandLine.Parse(["frobnicate"]);

        Assert.Equal(CliCommand.Unknown, parsed.Command);
        Assert.Equal("frobnicate", parsed.Raw);
    }

    [Fact]
    public void Keeps_the_remaining_arguments()
    {
        var parsed = CommandLine.Parse(["install", @"C:\apps\demo.apk"]);

        Assert.Equal(CliCommand.Install, parsed.Command);
        Assert.Equal([@"C:\apps\demo.apk"], parsed.Arguments);
    }

    [Fact]
    public void Keeps_every_part_of_an_unquoted_path_with_spaces()
    {
        var parsed = CommandLine.Parse(["install", @"C:\my", "apps", @"demo.apk"]);

        Assert.Equal(3, parsed.Arguments.Length);
        Assert.Equal(@"C:\my apps demo.apk", string.Join(' ', parsed.Arguments));
    }

    [Fact]
    public void Ignores_blank_arguments()
        => Assert.Equal(CliCommand.Devices, CommandLine.Parse(["  ", "devices"]).Command);

    [Fact]
    public void Switches_are_taken_out_of_the_command()
    {
        var parsed = CommandLine.Parse(["--light", "mirror"]);

        Assert.Equal(CliCommand.Mirror, parsed.Command);
        Assert.True(parsed.Settings.Light);
        Assert.Empty(parsed.Arguments);
    }

    [Fact]
    public void Switches_work_with_no_command_at_all()
    {
        var parsed = CommandLine.Parse(["--light"]);

        Assert.Equal(CliCommand.Watch, parsed.Command);
        Assert.True(parsed.Settings.Light);
    }

    [Fact]
    public void Input_mode_can_be_forced_either_way()
    {
        Assert.Equal(MirrorInput.Emulated, CommandLine.Parse(["mirror", "--uhid"]).Settings.Input);
        Assert.Equal(MirrorInput.Standard, CommandLine.Parse(["mirror", "--standard"]).Settings.Input);
        Assert.Null(CommandLine.Parse(["mirror"]).Settings.Input);
    }

    [Fact]
    public void Switches_do_not_eat_the_apk_path()
    {
        var parsed = CommandLine.Parse(["install", @"C:\apps\demo.apk", "--light"]);

        Assert.Equal(CliCommand.Install, parsed.Command);
        Assert.Equal([@"C:\apps\demo.apk"], parsed.Arguments);
        Assert.True(parsed.Settings.Light);
    }
}
