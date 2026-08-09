using PhoneDebug.Core.Models;
using Xunit;

namespace PhoneDebug.Tests;

public class DeviceNamingTests
{
    [Fact]
    public void Adds_the_manufacturer_when_the_market_name_omits_it()
    {
        var name = DeviceNaming.FriendlyName("samsung", "samsung", "Galaxy S24", "SM-S921B", "SERIAL");

        Assert.Equal("Samsung Galaxy S24", name);
    }

    [Fact]
    public void Keeps_the_market_name_when_it_already_names_the_brand()
    {
        var name = DeviceNaming.FriendlyName("Xiaomi", "POCO", "POCO M6 Pro", "2312FPCA6G", "SERIAL");

        Assert.Equal("POCO M6 Pro", name);
    }

    [Fact]
    public void Falls_back_to_the_model_when_there_is_no_market_name()
    {
        var name = DeviceNaming.FriendlyName("Google", null, null, "Pixel 8", "SERIAL");

        Assert.Equal("Google Pixel 8", name);
    }

    [Fact]
    public void Does_not_repeat_a_manufacturer_already_in_the_model()
    {
        var name = DeviceNaming.FriendlyName("HUAWEI", "HUAWEI", null, "HUAWEI P30", "SERIAL");

        Assert.Equal("HUAWEI P30", name);
    }

    [Fact]
    public void Falls_back_to_the_serial_when_nothing_is_known()
    {
        var name = DeviceNaming.FriendlyName(null, null, null, null, "R58N12ABCDE");

        Assert.Equal("R58N12ABCDE", name);
    }

    [Fact]
    public void Treats_unknown_and_blank_values_as_missing()
    {
        var name = DeviceNaming.FriendlyName("unknown", "  ", "", null, "SERIAL");

        Assert.Equal("SERIAL", name);
    }

    [Fact]
    public void Device_exposes_a_display_name_and_android_label()
    {
        var device = new AndroidDevice
        {
            Serial = "ABC123",
            State = "device",
            Manufacturer = "samsung",
            MarketName = "Galaxy S24",
            AndroidVersion = "15",
        };

        Assert.Equal("Samsung Galaxy S24", device.Name);
        Assert.Equal("Samsung Galaxy S24 (ABC123)", device.DisplayName);
        Assert.Equal("Android 15", device.AndroidLabel);
        Assert.True(device.IsAuthorized);
    }

    [Fact]
    public void Display_name_is_just_the_serial_when_the_device_is_unknown()
    {
        var device = new AndroidDevice { Serial = "ABC123", State = "unauthorized" };

        Assert.Equal("ABC123", device.DisplayName);
        Assert.Null(device.AndroidLabel);
        Assert.True(device.IsUnauthorized);
        Assert.False(device.IsAuthorized);
    }
}
