using PhoneDebug.Core.Services;
using Xunit;

namespace PhoneDebug.Tests;

public class AdbOutputParserTests
{
    [Fact]
    public void ParseDevices_reads_serial_and_state()
    {
        const string output = """
                              List of devices attached
                              ABC123        device
                              XYZ789        unauthorized
                              OLD555        offline
                              """;

        var devices = AdbOutputParser.ParseDevices(output);

        Assert.Equal(3, devices.Count);
        Assert.True(devices[0].IsAuthorized);
        Assert.True(devices[1].IsUnauthorized);
        Assert.True(devices[2].IsOffline);
        Assert.Equal("ABC123", devices[0].Serial);
    }

    [Fact]
    public void ParseDevices_keeps_wireless_serials_that_contain_spaces()
    {
        // adb appends " (2)" to a duplicate mDNS registration, so the serial
        // itself has a space in it - only the tab separates it from the state.
        const string output =
            "List of devices attached\n" +
            "192.168.0.200:37417\tdevice\n" +
            "adb-dym7am4luca649yh-op4RB1 (2)._adb-tls-connect._tcp\tdevice";

        var devices = AdbOutputParser.ParseDevices(output);

        Assert.Equal(2, devices.Count);
        Assert.All(devices, d => Assert.True(d.IsAuthorized));
        Assert.Equal("adb-dym7am4luca649yh-op4RB1 (2)._adb-tls-connect._tcp", devices[1].Serial);
    }

    [Fact]
    public void ParseDevices_reads_the_extra_columns_of_devices_dash_l()
    {
        var devices = AdbOutputParser.ParseDevices(
            "List of devices attached\nABC123\tdevice product:emerald model:2312FPCA6G");

        Assert.Single(devices);
        Assert.Equal("ABC123", devices[0].Serial);
        Assert.True(devices[0].IsAuthorized);
    }

    [Fact]
    public void ParseDevices_ignores_daemon_chatter()
    {
        const string output = """
                              * daemon not running; starting now at tcp:5037
                              * daemon started successfully
                              List of devices attached
                              ABC123	device
                              """;

        var devices = AdbOutputParser.ParseDevices(output);

        Assert.Single(devices);
        Assert.Equal("ABC123", devices[0].Serial);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("List of devices attached")]
    public void ParseDevices_returns_empty_when_nothing_is_attached(string? output)
        => Assert.Empty(AdbOutputParser.ParseDevices(output));

    [Fact]
    public void ParseProperties_reads_bracketed_pairs()
    {
        const string output = """
                              [ro.product.model]: [2312FPCA6G]
                              [ro.product.marketname]: [POCO M6 Pro]
                              [ro.build.version.release]: [16]
                              [ro.empty.value]: []
                              """;

        var props = AdbOutputParser.ParseProperties(output);

        Assert.Equal("2312FPCA6G", props["ro.product.model"]);
        Assert.Equal("POCO M6 Pro", props["ro.product.marketname"]);
        Assert.Equal("16", props["ro.build.version.release"]);
        Assert.False(props.ContainsKey("ro.empty.value"));
    }

    [Fact]
    public void ParseProperties_survives_junk()
    {
        var props = AdbOutputParser.ParseProperties("not a property line\n[broken\n[ok]: [yes]");

        Assert.Single(props);
        Assert.Equal("yes", props["ok"]);
    }

    [Theory]
    [InlineData("Failure [INSTALL_FAILED_INVALID_APK: bad]", "not a valid APK")]
    [InlineData("Failure [INSTALL_FAILED_INSUFFICIENT_STORAGE]", "free storage")]
    [InlineData("Failure [INSTALL_FAILED_VERSION_DOWNGRADE]", "Uninstall it first")]
    public void DescribeInstallFailure_explains_known_codes(string adbOutput, string expected)
    {
        var message = AdbOutputParser.DescribeInstallFailure(adbOutput, "");

        Assert.Contains(expected, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DescribeInstallFailure_falls_back_to_the_first_line()
    {
        var message = AdbOutputParser.DescribeInstallFailure("", "adb: device offline\nsecond line");

        Assert.Equal("adb: device offline", message);
    }

    [Fact]
    public void DescribeInstallFailure_ignores_progress_chatter_on_stdout()
    {
        // adb prints progress on stdout and the real reason on stderr.
        var message = AdbOutputParser.DescribeInstallFailure(
            "Performing Incremental Install\nPerforming Streamed Install",
            @"adb.exe: failed to install C:\tmp\app.apk: Failure [INSTALL_PARSE_FAILED_UNEXPECTED_EXCEPTION: Failed to parse base.apk: Corrupt XML binary file]");

        Assert.DoesNotContain("Performing", message);
        Assert.Contains("could not read this APK", message);
    }

    [Fact]
    public void DescribeInstallFailure_keeps_the_detail_of_an_unfamiliar_code()
    {
        var message = AdbOutputParser.DescribeInstallFailure(
            "Performing Streamed Install",
            @"adb: failed to install C:\tmp\app.apk: Failure [INSTALL_FAILED_SOMETHING_NEW: try again]");

        Assert.StartsWith("Failure [INSTALL_FAILED_SOMETHING_NEW", message);
    }

    [Fact]
    public void DescribeInstallFailure_never_returns_empty()
        => Assert.False(string.IsNullOrWhiteSpace(AdbOutputParser.DescribeInstallFailure("", "")));
}
