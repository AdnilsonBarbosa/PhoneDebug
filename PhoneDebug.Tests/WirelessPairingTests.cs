using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;
using Xunit;

namespace PhoneDebug.Tests;

public class WirelessPairingTests
{
    [Fact]
    public void Parses_the_output_of_adb_mdns_services()
    {
        const string output = """
                              List of discovered mdns services
                              adb-dym7am4luca649yh-op4RB1	_adb-tls-connect._tcp	192.168.0.200:34547
                              PhoneDebug-ABC123	_adb-tls-pairing._tcp	192.168.0.200:37101
                              """;

        var services = WirelessPairing.ParseServices(output);

        Assert.Equal(2, services.Count);
        Assert.True(services[0].IsConnect);
        Assert.False(services[0].IsPairing);
        Assert.True(services[1].IsPairing);
        Assert.Equal("PhoneDebug-ABC123", services[1].Name);
        Assert.Equal("192.168.0.200:37101", services[1].Address);
    }

    [Fact]
    public void Keeps_the_duplicate_registration_a_phone_makes_after_pairing()
    {
        // adb appends " (2)" to the instance name; that entry is the live one,
        // and dropping it leaves only a stale endpoint that refuses connections.
        const string output = """
                              List of discovered mdns services
                              adb-dym7am4luca649yh-op4RB1 (2)	_adb-tls-connect._tcp	192.168.0.200:37417
                              adb-dym7am4luca649yh-op4RB1	_adb-tls-connect._tcp	192.168.0.200:34547
                              """;

        var services = WirelessPairing.ParseServices(output);

        Assert.Equal(2, services.Count);
        Assert.Equal("adb-dym7am4luca649yh-op4RB1 (2)", services[0].Name);
        Assert.Equal("192.168.0.200:37417", services[0].Address);
        Assert.True(services[0].IsConnect);
        Assert.Equal("192.168.0.200:34547", services[1].Address);
    }

    [Fact]
    public void Reads_lines_that_are_padded_with_spaces_instead_of_tabs()
    {
        var services = WirelessPairing.ParseServices(
            "List of discovered mdns services\nmy phone   _adb-tls-pairing._tcp   10.0.0.4:37101");

        Assert.Single(services);
        Assert.Equal("my phone", services[0].Name);
        Assert.Equal("10.0.0.4:37101", services[0].Address);
        Assert.True(services[0].IsPairing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("List of discovered mdns services")]
    [InlineData("garbage line without fields")]
    [InlineData("_adb-tls-connect._tcp\t10.0.0.1:5555")]
    public void Parsing_survives_empty_or_odd_output(string? output)
        => Assert.Empty(WirelessPairing.ParseServices(output));

    [Fact]
    public void Ignores_services_that_are_not_adb()
    {
        var services = WirelessPairing.ParseServices("printer\t_ipp._tcp\t192.168.0.5:631");

        Assert.Empty(services);
    }

    [Fact]
    public void The_qr_payload_uses_the_format_android_expects()
    {
        var request = new PairingRequest("PhoneDebug-ABC123", "SECRET1234");

        Assert.Equal("WIFI:T:ADB;S:PhoneDebug-ABC123;P:SECRET1234;;", request.QrPayload);
    }

    [Fact]
    public void Each_request_gets_its_own_name_and_password()
    {
        var first = WirelessPairing.CreateRequest();
        var second = WirelessPairing.CreateRequest();

        Assert.StartsWith("PhoneDebug-", first.ServiceName);
        Assert.NotEqual(first.ServiceName, second.ServiceName);
        Assert.NotEqual(first.Password, second.Password);
        Assert.True(first.Password.Length >= 8);

        // Semicolons and colons would break the QR payload.
        Assert.DoesNotContain(';', first.Password);
        Assert.DoesNotContain(':', first.Password);
    }
}

public class QrCodeTests
{
    [Fact]
    public void Encodes_a_pairing_payload_into_a_square_grid()
    {
        var qr = QrCode.Encode("WIFI:T:ADB;S:PhoneDebug-ABC123;P:SECRET1234;;");

        Assert.True(qr.Size >= 25, $"unexpectedly small: {qr.Size}");
        Assert.True(qr.Size <= 60, $"unexpectedly large: {qr.Size}");
    }

    [Fact]
    public void Has_the_finder_patterns_a_camera_looks_for()
    {
        var qr = QrCode.Encode("WIFI:T:ADB;S:PhoneDebug-ABC123;P:SECRET1234;;");

        // QRCoder surrounds the code with a 4-module quiet zone.
        const int start = 4;

        AssertFinder(qr, start, start);                          // top left
        AssertFinder(qr, qr.Size - start - 7, start);            // top right
        AssertFinder(qr, start, qr.Size - start - 7);            // bottom left
    }

    [Fact]
    public void The_quiet_zone_is_empty()
    {
        var qr = QrCode.Encode("WIFI:T:ADB;S:X;P:Y;;");

        for (var i = 0; i < qr.Size; i++)
        {
            Assert.False(qr.IsDark(i, 0));
            Assert.False(qr.IsDark(0, i));
        }
    }

    [Fact]
    public void Reading_outside_the_grid_is_light()
    {
        var qr = QrCode.Encode("test");

        Assert.False(qr.IsDark(-1, 0));
        Assert.False(qr.IsDark(0, qr.Size));
    }

    /// <summary>A finder is a 7x7 dark ring, a light ring, then a 3x3 dark core.</summary>
    private static void AssertFinder(QrCode qr, int x, int y)
    {
        for (var i = 0; i < 7; i++)
        {
            Assert.True(qr.IsDark(x + i, y), $"top edge at {x + i},{y}");
            Assert.True(qr.IsDark(x + i, y + 6), $"bottom edge at {x + i},{y + 6}");
            Assert.True(qr.IsDark(x, y + i), $"left edge at {x},{y + i}");
            Assert.True(qr.IsDark(x + 6, y + i), $"right edge at {x + 6},{y + i}");
        }

        Assert.False(qr.IsDark(x + 1, y + 1));
        Assert.False(qr.IsDark(x + 5, y + 5));
        Assert.True(qr.IsDark(x + 3, y + 3));
    }
}

public class MirrorDiagnosticsTests
{
    [Fact]
    public void Recognises_a_phone_that_refuses_remote_control()
    {
        const string line =
            "[server] ERROR: Injecting input events requires the caller to have the INJECT_EVENTS permission.";

        var problem = MirrorDiagnostics.Detect(line);

        Assert.NotNull(problem);
        Assert.Equal(MirrorDiagnostics.ControlBlocked, problem!.Key);
        Assert.Contains("USB debugging (Security settings)", string.Join(' ', problem.Steps));
    }

    [Fact]
    public void Recognises_an_untrusted_phone()
    {
        var problem = MirrorDiagnostics.Detect("ERROR: Device unauthorized.");

        Assert.NotNull(problem);
        Assert.Equal("unauthorized", problem!.Key);
    }

    [Theory]
    [InlineData("INFO: Renderer: direct3d11")]
    [InlineData("INFO: Texture: 1080x2400")]
    [InlineData("")]
    [InlineData(null)]
    public void Ordinary_output_is_not_a_problem(string? line)
        => Assert.Null(MirrorDiagnostics.Detect(line!));

    [Fact]
    public void The_stay_awake_warning_is_not_shown_to_the_user()
    {
        // Harmless on phones without WRITE_SECURE_SETTINGS - it belongs in the log only.
        var problem = MirrorDiagnostics.Detect(
            "[server] ERROR: Could not change \"stay_on_while_plugged_in\"");

        Assert.Null(problem);
    }
}
