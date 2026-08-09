using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

/// <summary>
/// Pure parsing of adb text output, kept separate so it can be tested
/// without a phone attached.
/// </summary>
public static class AdbOutputParser
{
    /// <summary>
    /// Parses the output of "adb devices". Lines are "serial&lt;tab&gt;state";
    /// a wireless serial can itself contain spaces, as in
    /// "adb-XXXX (2)._adb-tls-connect._tcp", so the tab is what separates them.
    /// </summary>
    public static List<AndroidDevice> ParseDevices(string? output)
    {
        var devices = new List<AndroidDevice>();
        if (string.IsNullOrWhiteSpace(output))
            return devices;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith('*')) continue;                       // daemon chatter
            if (line.StartsWith("adb server", StringComparison.OrdinalIgnoreCase)) continue;

            string serial;
            string state;

            var tab = line.IndexOf('\t');
            if (tab > 0)
            {
                serial = line[..tab].Trim();
                state = First(line[(tab + 1)..]);
            }
            else
            {
                // Older or padded output: the state is the last field.
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                state = parts[^1];
                serial = string.Join(' ', parts[..^1]);
            }

            if (serial.Length == 0 || state.Length == 0)
                continue;

            devices.Add(new AndroidDevice
            {
                Serial = serial,
                State = state.ToLowerInvariant(),
            });
        }

        return devices;
    }

    private static string First(string text)
    {
        var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : "";
    }

    /// <summary>Parses "adb shell getprop" output, whose lines look like [key]: [value].</summary>
    public static Dictionary<string, string> ParseProperties(string? output)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
            return properties;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 7 || line[0] != '[')
                continue;

            var keyEnd = line.IndexOf("]:", StringComparison.Ordinal);
            if (keyEnd <= 1)
                continue;

            var key = line[1..keyEnd];

            var valueStart = line.IndexOf('[', keyEnd);
            var valueEnd = line.LastIndexOf(']');
            if (valueStart < 0 || valueEnd <= valueStart)
                continue;

            var value = line[(valueStart + 1)..valueEnd].Trim();
            if (value.Length > 0)
                properties[key] = value;
        }

        return properties;
    }

    /// <summary>
    /// Turns an adb install failure into something a person can act on.
    /// adb reports progress on stdout and the real reason on stderr, so stderr
    /// is read first and progress chatter is thrown away.
    /// </summary>
    public static string DescribeInstallFailure(string output, string error)
    {
        var lines = Meaningful(error).Concat(Meaningful(output)).ToList();
        if (lines.Count == 0)
            return "The installation failed and adb gave no reason.";

        var code = FindFailureCode(string.Join('\n', lines));
        if (code is null)
            return lines[0];

        var explanation = Explain(code);
        if (explanation is not null)
            return $"{explanation} ({code})";

        // An unfamiliar code: show the line that carries it, not the progress line.
        var detail = lines.FirstOrDefault(l => l.Contains(code, StringComparison.Ordinal));
        return detail is null ? $"The installation failed ({code})." : Tidy(detail);
    }

    private static string? Explain(string code) => code switch
    {
        "INSTALL_FAILED_ALREADY_EXISTS" => "The app is already installed.",
        "INSTALL_FAILED_INVALID_APK" => "The file is not a valid APK.",
        "INSTALL_FAILED_OLDER_SDK" or "INSTALL_FAILED_DEPRECATED_SDK_VERSION" =>
            "The app needs a different Android version than this device has.",
        "INSTALL_FAILED_INSUFFICIENT_STORAGE" => "The device does not have enough free storage.",
        "INSTALL_FAILED_UPDATE_INCOMPATIBLE" or "INSTALL_FAILED_VERSION_DOWNGRADE" =>
            "A different version of this app is already installed. Uninstall it first.",
        "INSTALL_FAILED_TEST_ONLY" => "The APK is marked test-only and cannot be installed this way.",
        "INSTALL_FAILED_USER_RESTRICTED" => "The device refused the install. Allow installs via USB on the phone.",
        "INSTALL_FAILED_NO_MATCHING_ABIS" => "The app does not support this device's processor.",
        "INSTALL_PARSE_FAILED_NO_CERTIFICATES" => "The APK is not signed.",
        "INSTALL_PARSE_FAILED_NOT_APK" => "The file is not an APK.",
        _ when code.StartsWith("INSTALL_PARSE_FAILED", StringComparison.Ordinal) =>
            "Android could not read this APK - it may be corrupt or built incorrectly.",
        _ => null,
    };

    /// <summary>Real content only: adb's progress lines say nothing about the failure.</summary>
    private static IEnumerable<string> Meaningful(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        return text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0
                        && !l.StartsWith("Performing ", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("Waiting for", StringComparison.OrdinalIgnoreCase)
                        && !l.StartsWith("Success", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Drops the "adb: failed to install &lt;path&gt;:" preamble.</summary>
    private static string Tidy(string line)
    {
        foreach (var prefix in new[] { "adb.exe: ", "adb: " })
        {
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                line = line[prefix.Length..];
        }

        const string marker = "failed to install ";
        if (line.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            var colon = line.IndexOf(": ", marker.Length, StringComparison.Ordinal);
            if (colon > 0)
                line = line[(colon + 2)..];
        }

        return line.Trim();
    }

    private static string? FindFailureCode(string text)
    {
        var index = text.IndexOf("INSTALL_", StringComparison.Ordinal);
        if (index < 0)
            return null;

        var end = index;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_'))
            end++;

        return text[index..end];
    }
}
