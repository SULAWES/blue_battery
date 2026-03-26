using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothDeviceDiscoveryService : IBluetoothDeviceDiscoveryService
{
    private readonly BluetoothBatteryDeviceService _snapshotService;
    private readonly BluetoothDeviceWatcherService _watcherService;
    private bool _disposed;

    public BluetoothDeviceDiscoveryService()
        : this(new BluetoothBatteryDeviceService(), new BluetoothDeviceWatcherService())
    {
    }

    internal BluetoothDeviceDiscoveryService(
        BluetoothBatteryDeviceService snapshotService,
        BluetoothDeviceWatcherService watcherService)
    {
        _snapshotService = snapshotService;
        _watcherService = watcherService;
        _watcherService.RefreshRequested += OnWatcherRefreshRequested;
    }

    public event EventHandler<BluetoothRefreshRequestedEventArgs>? RefreshRequested;

    public bool IsMonitoring => _watcherService.IsRunning;

    public Task<BluetoothRefreshResult> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _snapshotService.GetConnectedDevicesAsync(cancellationToken);
    }

    public void StartMonitoring()
    {
        ThrowIfDisposed();
        _watcherService.Start();
    }

    public void StopMonitoring()
    {
        if (_disposed)
        {
            return;
        }

        _watcherService.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcherService.RefreshRequested -= OnWatcherRefreshRequested;
        _watcherService.Dispose();
    }

    private void OnWatcherRefreshRequested(object? sender, BluetoothRefreshRequestedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        RefreshRequested?.Invoke(this, args);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
