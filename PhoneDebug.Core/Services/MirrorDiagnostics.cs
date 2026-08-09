using PhoneDebug.Core.Models;

namespace PhoneDebug.Core.Services;

/// <summary>Wording and keys shared by both front ends when input is emulated.</summary>
public static class MirrorPreferences
{
    public const string EmulatedInputNote =
        "Using an emulated keyboard and mouse, because this phone blocks injected input.";

    public const string ReleaseMouseNote =
        "The mouse is captured by the mirror window - press left Alt to give it back to Windows.";

    /// <summary>
    /// Model rather than serial: a phone on Wi-Fi gets a new serial every time
    /// it reconnects, but the model is what decides whether input is blocked.
    /// </summary>
    public static string KeyFor(AndroidDevice device) => device.Model ?? device.Serial;
}

/// <summary>Something the user has to fix on the phone, in plain words.</summary>
public sealed record MirrorProblem(string Key, string Summary, IReadOnlyList<string> Steps);

/// <summary>
/// Reads scrcpy's output and recognises the failures people actually hit, so
/// they get instructions instead of a Java stack trace.
/// </summary>
public static class MirrorDiagnostics
{
    public const string ControlBlocked = "control-blocked";

    public static MirrorProblem? Detect(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        if (line.Contains("INJECT_EVENTS", StringComparison.Ordinal)
            || line.Contains("Injecting input events requires", StringComparison.OrdinalIgnoreCase))
        {
            return new MirrorProblem(
                ControlBlocked,
                "The screen is mirrored, but the phone is refusing touch and keyboard input.",
                [
                    "The phone blocks remote control until you allow it:",
                    "",
                    "  Xiaomi / POCO / Redmi:",
                    "    Settings > Additional settings > Developer options",
                    "    turn on \"USB debugging (Security settings)\"",
                    "    (needs a Mi account signed in and a SIM card, then reboot the phone)",
                    "",
                    "  Other phones: look for \"USB debugging (Security settings)\"",
                    "  or \"Disable permission monitoring\" in Developer options.",
                ]);
        }

        if (line.Contains("Could not find any ADB device", StringComparison.OrdinalIgnoreCase))
        {
            return new MirrorProblem(
                "no-device",
                "The phone was gone before the screen could open.",
                ["Reconnect it and try again."]);
        }

        if (line.Contains("Device unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new MirrorProblem(
                "unauthorized",
                "The phone has not trusted this computer yet.",
                ["Unlock the phone and accept \"Allow USB debugging\"."]);
        }

        if (line.Contains("Could not connect to", StringComparison.OrdinalIgnoreCase)
            && line.Contains("scrcpy-server", StringComparison.OrdinalIgnoreCase))
        {
            return new MirrorProblem(
                "server",
                "scrcpy could not start its helper on the phone.",
                ["Unplug and reconnect the phone, then try again."]);
        }

        return null;
    }
}
