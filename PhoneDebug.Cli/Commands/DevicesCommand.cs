using PhoneDebug.Core.Services;

namespace PhoneDebug.Cli.Commands;

internal static class DevicesCommand
{
    public static int Run(DeviceService devices)
    {
        var all = devices.ListDevices(withDetails: true);

        if (all.Count == 0)
        {
            DevicePicker.ExplainNoDevice(all);
            return ExitCodes.NoDevice;
        }

        Ui.Blank();
        foreach (var device in all)
        {
            if (device.IsAuthorized)
            {
                Ui.Ok(device.Name);
                Ui.Hint($"  {device.AndroidLabel ?? "Android ?"}   {device.Serial}");
            }
            else
            {
                Ui.Idle($"{device.Serial}  ({device.State})");
            }
        }

        Ui.Blank();
        return ExitCodes.Ok;
    }
}
