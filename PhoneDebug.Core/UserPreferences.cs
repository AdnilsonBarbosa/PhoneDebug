using System.Text.Json;
using System.Text.Json.Serialization;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core;

/// <summary>
/// The few things worth remembering between runs. Best effort throughout - if
/// the file cannot be read or written, Phone Debug carries on with defaults.
/// </summary>
public static class UserPreferences
{
    private static readonly object Gate = new();
    private static Settings? _cache;

    private static string FilePath => Path.Combine(AppInfo.DataDirectory, "settings.json");

    /// <summary>
    /// True when this phone has already proven it needs emulated keyboard and
    /// mouse input, so mirroring can start that way instead of failing first.
    /// </summary>
    public static bool NeedsEmulatedInput(string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return false;

        lock (Gate)
            return Load().EmulatedInput.Contains(deviceKey, StringComparer.OrdinalIgnoreCase);
    }

    public static void RememberEmulatedInput(string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return;

        lock (Gate)
        {
            var settings = Load();
            if (settings.EmulatedInput.Contains(deviceKey, StringComparer.OrdinalIgnoreCase))
                return;

            settings.EmulatedInput.Add(deviceKey);
            Save(settings);
        }
    }

    /// <summary>Undoes <see cref="RememberEmulatedInput"/> - the way back to normal input.</summary>
    public static void ForgetEmulatedInput(string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey))
            return;

        lock (Gate)
        {
            var settings = Load();
            if (settings.EmulatedInput.RemoveAll(
                    k => string.Equals(k, deviceKey, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                Save(settings);
            }
        }
    }

    private static Settings Load()
    {
        if (_cache is not null)
            return _cache;

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cache = JsonSerializer.Deserialize<Settings>(json);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not read settings: {ex.Message}");
        }

        return _cache ??= new Settings();
    }

    private static void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Warn($"Could not write settings: {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class Settings
    {
        /// <summary>Device models that need UHID input, keyed by model name.</summary>
        [JsonPropertyName("emulatedInput")]
        public List<string> EmulatedInput { get; set; } = [];
    }
}
