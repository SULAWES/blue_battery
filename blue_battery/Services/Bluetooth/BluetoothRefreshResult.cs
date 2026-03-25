using System.Collections.Generic;
using BlueBattery.Models;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothRefreshResult
{
    public required IReadOnlyList<DeviceBatteryInfo> Devices { get; init; }

    public required int ConnectedLeDeviceCount { get; init; }
}
