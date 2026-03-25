using System;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothRefreshRequestedEventArgs : EventArgs
{
    public required string Reason { get; init; }
}
