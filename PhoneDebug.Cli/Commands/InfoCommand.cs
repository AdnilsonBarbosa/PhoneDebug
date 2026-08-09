using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class InfoCommand
{
    public static int Run(DeviceService devices)
    {
        var device = DevicePicker.Pick(devices);
        if (device is null)
            return ExitCodes.NoDevice;

        Ui.Blank();
        Ui.Ok(device.Name);
        Ui.Blank();
        Row("Model", device.Model);
        Row("Manufacturer", device.Manufacturer);
        Row("Android", device.AndroidVersion);
        Row("API level", device.SdkLevel);
        Row("Security patch", device.SecurityPatch);
        Row("CPU", device.CpuAbi);
        Row("Serial", device.Serial);
        Ui.Blank();

        return ExitCodes.Ok;
    }

    private static void Row(string label, string? value)
        => Ui.Line($"  {label,-16} {value ?? "(unknown)"}");
}
