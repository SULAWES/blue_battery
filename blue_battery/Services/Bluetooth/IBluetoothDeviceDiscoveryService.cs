using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBattery.Services.Bluetooth;

public interface IBluetoothDeviceDiscoveryService : IDisposable
{
    event EventHandler<BluetoothRefreshRequestedEventArgs>? RefreshRequested;

    bool IsMonitoring { get; }

    Task<BluetoothRefreshResult> GetConnectedDevicesAsync(CancellationToken cancellationToken = default);

    void StartMonitoring();

    void StopMonitoring();
}
