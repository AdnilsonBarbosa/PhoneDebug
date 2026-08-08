using System.Diagnostics;
using PhoneDebug.Models;

namespace PhoneDebug.Services;

/// <summary>
/// Polls "adb devices" (lightly) and reacts to Android devices
/// connecting, disconnecting or being unauthorized.
/// </summary>
public sealed class DeviceWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private enum Phase
    {
        NoDevice,
        Unauthorized,
        Connected,
        AdbError,
    }

    private readonly AdbService _adb;
    private readonly ScrcpyService _scrcpy;

    private string? _activeSerial;
    private Process? _scrcpyProcess;

    public DeviceWatcher(AdbService adb, ScrcpyService scrcpy)
    {
        _adb = adb;
        _scrcpy = scrcpy;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("Waiting for Android device...");
        Console.WriteLine("Press Ctrl+C to exit.");

        var phase = Phase.NoDevice;

        while (!cancellationToken.IsCancellationRequested)
        {
            List<AndroidDevice> devices;
            try
            {
                devices = _adb.GetDevices();
            }
            catch
            {
                if (phase != Phase.AdbError)
                {
                    Console.WriteLine();
                    Console.WriteLine("ADB read failed. Retrying...");
                    phase = Phase.AdbError;
                }
                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            var authorized = devices.Where(d => d.IsAuthorized).ToList();
            var unauthorized = devices.Where(d => d.IsUnauthorized).ToList();

            if (authorized.Count > 0)
            {
                var serial = ChooseSerial(devices, _activeSerial);
                if (serial is not null && serial != _activeSerial)
                {
                    StopScrcpy();

                    _activeSerial = serial;
                    var device = devices.First(d => d.Serial == serial);
                    _adb.FillDetails(device);

                    PrintConnected(device);
                    StartScrcpy(device);
                    phase = Phase.Connected;
                }
                else if (phase == Phase.AdbError)
                {
                    phase = Phase.Connected;
                }

                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            if (unauthorized.Count > 0)
            {
                if (phase != Phase.Unauthorized)
                {
                    if (phase == Phase.Connected)
                        PrintDisconnected();

                    StopScrcpy();
                    _activeSerial = null;

                    Console.WriteLine();
                    Console.WriteLine("Device detected but not authorized.");
                    Console.WriteLine("Unlock your phone and accept the USB debugging authorization.");

                    phase = Phase.Unauthorized;
                }

                await Task.Delay(PollInterval, cancellationToken);
                continue;
            }

            // No devices.
            if (phase != Phase.NoDevice)
            {
                if (phase == Phase.Connected || phase == Phase.Unauthorized)
                {
                    StopScrcpy();
                    _activeSerial = null;
                    PrintDisconnected();
                    Console.WriteLine("Waiting for Android device...");
                }

                phase = Phase.NoDevice;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        StopScrcpy();
    }

    /// <summary>
    /// Keeps the current serial while it is still authorized; otherwise
    /// picks the single connected device or asks the user to choose.
    /// </summary>
    private static string? ChooseSerial(List<AndroidDevice> allDevices, string? activeSerial)
    {
        var authorized = allDevices.Where(d => d.IsAuthorized).ToList();

        if (activeSerial is not null && authorized.Any(d => d.Serial == activeSerial))
            return activeSerial;

        if (authorized.Count == 1)
            return authorized[0].Serial;

        if (authorized.Count == 0)
            return null;

        return PromptSelection(authorized);
    }

    private static string? PromptSelection(List<AndroidDevice> devices)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Multiple Android devices detected:");
            for (var i = 0; i < devices.Count; i++)
                Console.WriteLine($"  [{i + 1}] {devices[i].DisplayName}");
            Console.WriteLine();
            Console.Write($"Select a device (1-{devices.Count}): ");

            var input = Console.ReadLine();
            if (!int.TryParse(input, out var choice) || choice < 1 || choice > devices.Count)
            {
                Console.WriteLine("Invalid choice, try again.");
                continue;
            }

            return devices[choice - 1].Serial;
        }
    }

    private void StartScrcpy(AndroidDevice device)
    {
        _scrcpyProcess = _scrcpy.Start(device.Serial);
        if (_scrcpyProcess is null)
            Console.Error.WriteLine("Failed to open the phone screen.");
    }

    private void StopScrcpy()
    {
        if (_scrcpyProcess is null)
            return;

        if (!_scrcpyProcess.HasExited)
        {
            try
            {
                _scrcpyProcess.Kill();
                _scrcpyProcess.WaitForExit(2000);
            }
            catch
            {
                // already gone
            }
        }

        _scrcpyProcess.Dispose();
        _scrcpyProcess = null;
    }

    private void PrintConnected(AndroidDevice device)
    {
        Console.WriteLine();
        Console.WriteLine("Device connected");
        Console.WriteLine($"  Model:   {device.Model ?? "(unknown)"}");
        Console.WriteLine($"  Android: {device.AndroidVersion ?? "(unknown)"}");
        Console.WriteLine($"  Serial:  {device.Serial}");
        Console.WriteLine();
        Console.WriteLine("Opening screen...");
    }

    private static void PrintDisconnected()
    {
        Console.WriteLine();
        Console.WriteLine("Device disconnected.");
        Console.WriteLine();
    }
}