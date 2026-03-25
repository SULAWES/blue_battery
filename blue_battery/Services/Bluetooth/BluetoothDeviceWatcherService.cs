using System;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothDeviceWatcherService : IDisposable
{
    private DeviceWatcher? _watcher;
    private bool _isStopping;

    public event EventHandler<BluetoothRefreshRequestedEventArgs>? RefreshRequested;

    public bool IsRunning => _watcher?.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted;

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        StopWatcher();
        _isStopping = false;

        string selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
        _watcher = DeviceInformation.CreateWatcher(selector);
        _watcher.Added += OnAdded;
        _watcher.Updated += OnUpdated;
        _watcher.Removed += OnRemoved;
        _watcher.Start();
    }

    public void Stop()
    {
        _isStopping = true;
        StopWatcher();
    }

    public void Dispose()
    {
        Stop();
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Added -= OnAdded;
        _watcher.Updated -= OnUpdated;
        _watcher.Removed -= OnRemoved;

        if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            _watcher.Stop();
        }

        _watcher = null;
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation args)
    {
        RaiseDevicesChanged("检测到蓝牙设备接入。");
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        RaiseDevicesChanged("检测到蓝牙设备状态变化。");
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        RaiseDevicesChanged("检测到蓝牙设备断开。");
    }

    private void RaiseDevicesChanged(string reason)
    {
        if (_isStopping)
        {
            return;
        }

        RefreshRequested?.Invoke(this, new BluetoothRefreshRequestedEventArgs
        {
            Reason = reason,
        });
    }
}
