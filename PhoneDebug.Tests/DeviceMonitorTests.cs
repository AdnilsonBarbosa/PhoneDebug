using PhoneDebug.Core.Models;
using PhoneDebug.Core.Services;
using Xunit;

namespace PhoneDebug.Tests;

public class DeviceMonitorTests
{
    private static AndroidDevice Device(string serial, string state = "device")
        => new() { Serial = serial, State = state };

    [Fact]
    public void No_devices_means_no_device()
    {
        var (state, device) = DeviceMonitor.Evaluate([], null, null);

        Assert.Equal(DeviceMonitorState.NoDevice, state);
        Assert.Null(device);
    }

    [Fact]
    public void One_authorized_device_connects()
    {
        var (state, device) = DeviceMonitor.Evaluate([Device("A")], null, null);

        Assert.Equal(DeviceMonitorState.Connected, state);
        Assert.Equal("A", device!.Serial);
    }

    [Fact]
    public void An_unauthorized_device_is_reported_as_such()
    {
        var (state, _) = DeviceMonitor.Evaluate([Device("A", "unauthorized")], null, null);

        Assert.Equal(DeviceMonitorState.Unauthorized, state);
    }

    [Fact]
    public void An_offline_device_is_reported_as_such()
    {
        var (state, _) = DeviceMonitor.Evaluate([Device("A", "offline")], null, null);

        Assert.Equal(DeviceMonitorState.Offline, state);
    }

    [Fact]
    public void Unauthorized_wins_over_offline_because_the_user_can_act_on_it()
    {
        var (state, _) = DeviceMonitor.Evaluate(
            [Device("A", "offline"), Device("B", "unauthorized")], null, null);

        Assert.Equal(DeviceMonitorState.Unauthorized, state);
    }

    [Fact]
    public void A_working_device_wins_over_a_broken_one()
    {
        var (state, device) = DeviceMonitor.Evaluate(
            [Device("A", "unauthorized"), Device("B")], null, null);

        Assert.Equal(DeviceMonitorState.Connected, state);
        Assert.Equal("B", device!.Serial);
    }

    [Fact]
    public void Two_devices_and_no_preference_asks_for_a_choice()
    {
        var (state, device) = DeviceMonitor.Evaluate([Device("A"), Device("B")], null, null);

        Assert.Equal(DeviceMonitorState.MultipleDevices, state);
        Assert.Null(device);
    }

    [Fact]
    public void A_preferred_serial_settles_the_choice()
    {
        var (state, device) = DeviceMonitor.Evaluate([Device("A"), Device("B")], "B", null);

        Assert.Equal(DeviceMonitorState.Connected, state);
        Assert.Equal("B", device!.Serial);
    }

    [Fact]
    public void The_device_already_in_use_is_kept_when_another_appears()
    {
        var (state, device) = DeviceMonitor.Evaluate([Device("A"), Device("B")], null, "A");

        Assert.Equal(DeviceMonitorState.Connected, state);
        Assert.Equal("A", device!.Serial);
    }

    [Fact]
    public void A_preference_for_a_device_that_left_falls_back_to_the_only_one_present()
    {
        var (state, device) = DeviceMonitor.Evaluate([Device("A")], "GONE", null);

        Assert.Equal(DeviceMonitorState.Connected, state);
        Assert.Equal("A", device!.Serial);
    }

    [Fact]
    public void Unplugging_the_device_in_use_returns_to_waiting()
    {
        var (state, _) = DeviceMonitor.Evaluate([], null, "A");

        Assert.Equal(DeviceMonitorState.NoDevice, state);
    }
}
