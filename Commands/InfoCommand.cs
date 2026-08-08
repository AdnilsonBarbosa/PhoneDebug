using PhoneDebug.Services;

namespace PhoneDebug.Commands;

internal static class InfoCommand
{
    public static int Run(AdbService adb)
    {
        var device = DevicePicker.PickOrThrow(adb);
        if (device is null)
            return 1;

        string? Prop(string key) => adb.GetProp(device.Serial, key);

        Console.WriteLine();
        Console.WriteLine($"  Serial:              {device.Serial}");
        Console.WriteLine($"  Model:               {device.Model ?? "(unknown)"}");
        Console.WriteLine($"  Manufacturer:        {Prop("ro.product.manufacturer") ?? "(unknown)"}");
        Console.WriteLine($"  Android:             {device.AndroidVersion ?? "(unknown)"}");
        Console.WriteLine($"  API level (SDK):     {Prop("ro.build.version.sdk") ?? "(unknown)"}");
        Console.WriteLine($"  Security patch:      {Prop("ro.build.version.security_patch") ?? "(unknown)"}");
        Console.WriteLine($"  CPU ABI:             {Prop("ro.product.cpu.abi") ?? "(unknown)"}");
        return 0;
    }
}