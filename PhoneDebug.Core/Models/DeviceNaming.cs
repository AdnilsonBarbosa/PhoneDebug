namespace PhoneDebug.Core.Models;

/// <summary>
/// Builds the name a person recognises ("Samsung Galaxy S24") out of the
/// several, inconsistent properties Android vendors expose.
/// </summary>
public static class DeviceNaming
{
    public static string FriendlyName(
        string? manufacturer,
        string? brand,
        string? marketName,
        string? model,
        string fallback)
    {
        var maker = Normalize(Pick(manufacturer, brand));
        var name = Clean(marketName) ?? Clean(model);

        if (name is null)
            return maker ?? fallback;

        if (maker is null || StartsWithWord(name, maker) || StartsWithWord(name, Clean(brand)))
            return name;

        return $"{maker} {name}";
    }

    /// <summary>
    /// Lowercase vendor strings ("samsung") get capitalised; acronyms and
    /// deliberate capitalisation ("HUAWEI", "POCO", "OnePlus") are left alone.
    /// </summary>
    private static string? Normalize(string? value)
    {
        if (value is null)
            return null;

        return value.Any(char.IsUpper)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static bool StartsWithWord(string value, string? word)
    {
        if (string.IsNullOrEmpty(word))
            return false;

        if (!value.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            return false;

        return value.Length == word.Length || !char.IsLetterOrDigit(value[word.Length]);
    }

    private static string? Pick(string? first, string? second) => Clean(first) ?? Clean(second);

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length == 0 || trimmed == "unknown" ? null : trimmed;
    }
}
