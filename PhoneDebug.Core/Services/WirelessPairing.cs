using System.Security.Cryptography;
using PhoneDebug.Core.Diagnostics;

namespace PhoneDebug.Core.Services;

/// <summary>One entry of "adb mdns services".</summary>
public sealed record MdnsService(string Name, string ServiceType, string Address)
{
    public bool IsPairing => ServiceType.Contains("_adb-tls-pairing", StringComparison.OrdinalIgnoreCase);

    public bool IsConnect => ServiceType.Contains("_adb-tls-connect", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// What the phone has to scan. The payload is the format Android's
/// "Pair device with QR code" screen expects.
/// </summary>
public sealed record PairingRequest(string ServiceName, string Password)
{
    public string QrPayload => $"WIFI:T:ADB;S:{ServiceName};P:{Password};;";
}

/// <summary>
/// Wireless debugging: pair a phone over Wi-Fi, by QR code or by typing the
/// six-digit code the phone shows.
/// </summary>
public sealed class WirelessPairing
{
    // Ambiguous characters left out - the password ends up inside a QR code
    // but also has to be readable if anyone types it.
    private const string PasswordAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly AdbService _adb;

    public WirelessPairing(AdbService adb) => _adb = adb;

    public static PairingRequest CreateRequest()
        => new($"PhoneDebug-{RandomText(6)}", RandomText(10));

    /// <summary>Services adb can currently see on the network.</summary>
    public IReadOnlyList<MdnsService> ListServices()
        => ParseServices(_adb.Run(15_000, "mdns", "services").Output);

    /// <remarks>
    /// Lines look like "name\t_adb-tls-connect._tcp\t192.168.0.200:37417", but
    /// the name can contain spaces - adb appends " (2)" when a phone registers
    /// twice, which is exactly the fresh entry worth connecting to. The service
    /// type is therefore located first and the fields read around it.
    /// </remarks>
    internal static List<MdnsService> ParseServices(string? output)
    {
        var services = new List<MdnsService>();
        if (string.IsNullOrWhiteSpace(output))
            return services;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("List of", StringComparison.OrdinalIgnoreCase)) continue;

            var fields = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var type = Array.FindIndex(fields, f => f.StartsWith("_adb", StringComparison.OrdinalIgnoreCase));
            if (type < 1 || type + 1 >= fields.Length)
                continue;

            var name = string.Join(' ', fields[..type]);
            services.Add(new MdnsService(name, fields[type], fields[type + 1]));
        }

        return services;
    }

    /// <summary>Pairs using the address and six-digit code shown on the phone.</summary>
    public OperationResult PairWithCode(string address, string code)
    {
        address = address.Trim();
        code = code.Trim();

        if (address.Length == 0)
            return OperationResult.Fail("Enter the \"IP address & Port\" shown on the phone.");

        if (code.Length == 0)
            return OperationResult.Fail("Enter the six-digit pairing code shown on the phone.");

        Log.Info($"Pairing with {address}");
        var result = _adb.Run(60_000, "pair", address, code);

        // adb pair prints "Failed: ..." and can still exit 0.
        var failed = !result.Success
                     || result.Output.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                     || result.Error.Contains("Failed", StringComparison.OrdinalIgnoreCase);

        if (failed)
            return OperationResult.Fail(DescribePairFailure(result));

        return OperationResult.Ok($"Paired with {address}.");
    }

    public OperationResult Connect(string address)
    {
        address = address.Trim();
        if (address.Length == 0)
            return OperationResult.Fail("No address to connect to.");

        var result = _adb.Run(30_000, "connect", address);
        var text = result.Output + result.Error;

        if (!result.Success || text.Contains("failed", StringComparison.OrdinalIgnoreCase)
                            || text.Contains("cannot connect", StringComparison.OrdinalIgnoreCase))
        {
            return OperationResult.Fail(text.Trim().Length > 0 ? text.Trim() : "Could not connect.");
        }

        return OperationResult.Ok($"Connected to {address}.");
    }

    /// <summary>
    /// Waits for the phone to scan the QR code, pairs with it and waits for it
    /// to show up as a device. Progress messages are meant for the user.
    /// </summary>
    public async Task<OperationResult> PairWithQrAsync(
        PairingRequest request,
        IProgress<string>? progress = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(3));
        var lastError = "The phone did not scan the code in time.";

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pairing = ListServices().Where(s => s.IsPairing).ToList();
            var match = pairing.FirstOrDefault(s => s.Name == request.ServiceName) ?? pairing.FirstOrDefault();

            if (match is not null)
            {
                progress?.Report("Phone found, pairing...");

                var paired = PairWithCode(match.Address, request.Password);
                if (paired.Success)
                {
                    progress?.Report("Paired. Waiting for the device...");
                    return await WaitForDeviceAsync(progress, cancellationToken).ConfigureAwait(false);
                }

                // Could be another pairing session on the network; keep looking.
                lastError = paired.Message;
                Log.Warn($"Pairing attempt with {match.Address} failed: {paired.Message}");
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return OperationResult.Fail(lastError);
    }

    /// <summary>
    /// After pairing, adb usually connects on its own through mDNS. This gives
    /// it a moment and connects explicitly if it does not.
    /// </summary>
    /// <remarks>
    /// A phone re-advertises on a new port after pairing, and the old entry can
    /// linger in mDNS for a while. Every advertised endpoint is therefore
    /// retried on each pass - trying each address only once picks the stale one
    /// and gives up just before the real one appears.
    /// </remarks>
    public async Task<OperationResult> WaitForDeviceAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        var announced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var devices = _adb.GetDevices();
            if (devices.Any(d => d.IsAuthorized))
                return OperationResult.Ok("Device connected.");

            if (devices.Any(d => d.IsUnauthorized))
                return OperationResult.Ok("Device connected. Accept \"Allow USB debugging\" on the phone.");

            foreach (var service in ListServices().Where(s => s.IsConnect))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Only mention each address once, however often it is retried.
                if (announced.Add(service.Address))
                    progress?.Report($"Connecting to {service.Address}...");

                var attempt = Connect(service.Address);
                if (attempt.Success)
                    break;

                lastError = attempt.Message;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        Log.Warn($"Paired but never connected. Last error: {lastError ?? "(none)"}");

        return OperationResult.Fail(
            "Paired, but the phone did not connect. Check that both are on the same Wi-Fi network, "
            + "then run \"phone-debug connect\" again.");
    }

    private static string DescribePairFailure(AdbResult result)
    {
        var text = (result.Error + "\n" + result.Output).Trim();

        if (text.Contains("Failed to authenticate", StringComparison.OrdinalIgnoreCase))
            return "The pairing code was not accepted. It changes every time - reopen the pairing screen and try again.";

        if (text.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
            || text.Contains("cannot connect", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not reach the phone. Make sure the PC and the phone are on the same Wi-Fi network.";
        }

        // Happens when something else grabbed the pairing session first, which
        // usually means two Phone Debug windows are pairing at the same time.
        if (text.Contains("protocol fault", StringComparison.OrdinalIgnoreCase))
        {
            return "The pairing did not go through. Close any other Phone Debug window, "
                   + "reopen \"Pair device with QR code\" on the phone and try again.";
        }

        var line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return line ?? "Pairing failed.";
    }

    private static string RandomText(int length)
    {
        var characters = new char[length];
        for (var i = 0; i < length; i++)
            characters[i] = PasswordAlphabet[RandomNumberGenerator.GetInt32(PasswordAlphabet.Length)];

        return new string(characters);
    }
}
