namespace PhoneDebug.Core.Models;

public sealed class AndroidDevice
{
    public required string Serial { get; init; }

    /// <summary>Raw adb state: device, unauthorized, offline, ...</summary>
    public string State { get; init; } = "device";

    public string? Model { get; set; }
    public string? Manufacturer { get; set; }
    public string? Brand { get; set; }

    /// <summary>Commercial name reported by the vendor, e.g. "POCO M6 Pro".</summary>
    public string? MarketName { get; set; }

    public string? AndroidVersion { get; set; }
    public string? SdkLevel { get; set; }
    public string? SecurityPatch { get; set; }
    public string? CpuAbi { get; set; }

    public bool IsAuthorized =>
        string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);

    public bool IsUnauthorized =>
        string.Equals(State, "unauthorized", StringComparison.OrdinalIgnoreCase);

    public bool IsOffline =>
        string.Equals(State, "offline", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Connected over the network rather than a cable - either "ip:port" or an
    /// mDNS name. Video then competes with everything else on the Wi-Fi.
    /// </summary>
    public bool IsWireless =>
        Serial.Contains("_adb-tls-", StringComparison.OrdinalIgnoreCase)
        || (Serial.Contains(':') && Serial.Count(c => c == '.') >= 3);

    /// <summary>Human name, e.g. "Samsung Galaxy S24". Falls back to the serial.</summary>
    public string Name => DeviceNaming.FriendlyName(Manufacturer, Brand, MarketName, Model, Serial);

    /// <summary>Name plus serial, for lists where the serial matters.</summary>
    public string DisplayName => Name == Serial ? Serial : $"{Name} ({Serial})";

    /// <summary>"Android 15", or null when unknown.</summary>
    public string? AndroidLabel =>
        string.IsNullOrWhiteSpace(AndroidVersion) ? null : $"Android {AndroidVersion}";
}
