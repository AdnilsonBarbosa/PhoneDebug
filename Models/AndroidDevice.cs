namespace PhoneDebug.Models;

public sealed class AndroidDevice
{
    public required string Serial { get; init; }
    public string State { get; init; } = "device";

    public string? Model { get; set; }
    public string? AndroidVersion { get; set; }

    public bool IsAuthorized =>
        string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);

    public bool IsUnauthorized =>
        string.Equals(State, "unauthorized", StringComparison.OrdinalIgnoreCase);

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
}