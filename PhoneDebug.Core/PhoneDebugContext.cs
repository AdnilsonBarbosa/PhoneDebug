using PhoneDebug.Core.Diagnostics;
using PhoneDebug.Core.Services;
using PhoneDebug.Core.Tools;

namespace PhoneDebug.Core;

/// <summary>
/// One call that gets Phone Debug ready: locate the tools and build the
/// services on top of them. Both the CLI and the GUI start here, which is
/// what keeps them behaving identically.
/// </summary>
public sealed class PhoneDebugContext
{
    private PhoneDebugContext(ToolEnvironment tools, AdbService? adb, ScrcpyService? scrcpy)
    {
        Tools = tools;
        Adb = adb;
        Scrcpy = scrcpy;
        Devices = adb is null ? null : new DeviceService(adb);
        Pairing = adb is null ? null : new WirelessPairing(adb);
    }

    public ToolEnvironment Tools { get; }

    /// <summary>Null when adb is missing.</summary>
    public AdbService? Adb { get; }

    /// <summary>Null when adb is missing.</summary>
    public DeviceService? Devices { get; }

    /// <summary>Null when scrcpy is missing.</summary>
    public ScrcpyService? Scrcpy { get; }

    /// <summary>Wireless debugging setup. Null when adb is missing.</summary>
    public WirelessPairing? Pairing { get; }

    public bool CanUseDevices => Devices is not null;

    public bool CanMirror => Devices is not null && Scrcpy is not null;

    public static PhoneDebugContext Create(bool probeVersions = true)
    {
        Log.Info($"{AppInfo.Title} starting ({AppInfo.BaseDirectory})");

        var tools = ToolEnvironment.Detect(probeVersions);

        var adb = tools.Adb.Found ? new AdbService(tools.Adb.Path!) : null;
        var scrcpy = tools.Scrcpy.Found ? new ScrcpyService(tools.Scrcpy.Path!) : null;

        return new PhoneDebugContext(tools, adb, scrcpy);
    }

    public DeviceMonitor CreateMonitor(TimeSpan? interval = null)
    {
        if (Devices is null)
            throw new InvalidOperationException("ADB is not available.");

        return new DeviceMonitor(Devices, interval);
    }
}
