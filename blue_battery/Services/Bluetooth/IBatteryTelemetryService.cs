using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBattery.Services.Bluetooth;

public interface IBatteryTelemetryService : IDisposable
{
    event EventHandler<BluetoothRefreshRequestedEventArgs>? RefreshRequested;

    Task UpdateTrackedDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default);
}
