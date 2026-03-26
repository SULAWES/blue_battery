using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothBatteryTelemetryService : IBatteryTelemetryService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Dictionary<string, SubscriptionContext> _subscriptions = new(StringComparer.Ordinal);
    private Timer? _pollTimer;
    private bool _disposed;

    public event EventHandler<BluetoothRefreshRequestedEventArgs>? RefreshRequested;

    public async Task UpdateTrackedDevicesAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        string[] targetIds = deviceIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            HashSet<string> targetSet = targetIds.ToHashSet(StringComparer.Ordinal);

            string[] removedIds = _subscriptions.Keys
                .Where(existingId => !targetSet.Contains(existingId))
                .ToArray();

            foreach (string removedId in removedIds)
            {
                _subscriptions[removedId].Dispose();
                _subscriptions.Remove(removedId);
            }

            foreach (string targetId in targetIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_subscriptions.ContainsKey(targetId))
                {
                    continue;
                }

                _subscriptions[targetId] = await CreateSubscriptionContextAsync(targetId);
            }

            UpdatePollingTimer();
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer?.Dispose();
        _pollTimer = null;

        foreach (SubscriptionContext subscription in _subscriptions.Values)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
        _syncLock.Dispose();
    }

    private async Task<SubscriptionContext> CreateSubscriptionContextAsync(string deviceId)
    {
        try
        {
            BluetoothLEDevice? bluetoothDevice = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (bluetoothDevice is null)
            {
                return SubscriptionContext.CreateFallbackOnly(deviceId);
            }

            GattDeviceServicesResult servicesResult = await bluetoothDevice.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery,
                BluetoothCacheMode.Uncached);

            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                bluetoothDevice.Dispose();
                return SubscriptionContext.CreateFallbackOnly(deviceId);
            }

            GattDeviceService service = servicesResult.Services[0];
            GattCharacteristicsResult characteristicsResult = await service.GetCharacteristicsForUuidAsync(
                GattCharacteristicUuids.BatteryLevel,
                BluetoothCacheMode.Uncached);

            if (characteristicsResult.Status != GattCommunicationStatus.Success || characteristicsResult.Characteristics.Count == 0)
            {
                service.Dispose();
                bluetoothDevice.Dispose();
                return SubscriptionContext.CreateFallbackOnly(deviceId);
            }

            GattCharacteristic characteristic = characteristicsResult.Characteristics[0];
            bool supportsNotify =
                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate);

            if (!supportsNotify)
            {
                service.Dispose();
                bluetoothDevice.Dispose();
                return SubscriptionContext.CreateFallbackOnly(deviceId);
            }

            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = OnCharacteristicValueChanged;
            characteristic.ValueChanged += handler;

            GattClientCharacteristicConfigurationDescriptorValue cccdValue =
                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
                    ? GattClientCharacteristicConfigurationDescriptorValue.Notify
                    : GattClientCharacteristicConfigurationDescriptorValue.Indicate;

            GattCommunicationStatus status =
                await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

            if (status != GattCommunicationStatus.Success)
            {
                characteristic.ValueChanged -= handler;
                service.Dispose();
                bluetoothDevice.Dispose();
                return SubscriptionContext.CreateFallbackOnly(deviceId);
            }

            return new SubscriptionContext(deviceId, bluetoothDevice, service, characteristic, handler, notificationEnabled: true);
        }
        catch
        {
            return SubscriptionContext.CreateFallbackOnly(deviceId);
        }
    }

    private void OnCharacteristicValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        RaiseRefreshRequested("检测到设备电量变化。");
    }

    private void UpdatePollingTimer()
    {
        bool requiresPolling = _subscriptions.Count > 0 && _subscriptions.Values.Any(subscription => !subscription.NotificationEnabled);

        if (!requiresPolling)
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
            return;
        }

        _pollTimer ??= new Timer(OnPollTimerTick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _pollTimer.Change(PollInterval, PollInterval);
    }

    private void OnPollTimerTick(object? state)
    {
        RaiseRefreshRequested("正在按轮询策略刷新设备电量。");
    }

    private void RaiseRefreshRequested(string reason)
    {
        if (_disposed)
        {
            return;
        }

        RefreshRequested?.Invoke(this, new BluetoothRefreshRequestedEventArgs
        {
            Reason = reason,
        });
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class SubscriptionContext : IDisposable
    {
        private readonly BluetoothLEDevice? _bluetoothDevice;
        private readonly GattDeviceService? _service;
        private readonly GattCharacteristic? _characteristic;
        private readonly TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>? _handler;
        private bool _disposed;

        public SubscriptionContext(
            string deviceId,
            BluetoothLEDevice? bluetoothDevice,
            GattDeviceService? service,
            GattCharacteristic? characteristic,
            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs>? handler,
            bool notificationEnabled)
        {
            DeviceId = deviceId;
            _bluetoothDevice = bluetoothDevice;
            _service = service;
            _characteristic = characteristic;
            _handler = handler;
            NotificationEnabled = notificationEnabled;
        }

        public string DeviceId { get; }

        public bool NotificationEnabled { get; }

        public static SubscriptionContext CreateFallbackOnly(string deviceId)
        {
            return new SubscriptionContext(deviceId, null, null, null, null, notificationEnabled: false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_characteristic is not null && _handler is not null)
            {
                try
                {
                    _characteristic.ValueChanged -= _handler;
                    _ = _characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch
                {
                }
            }

            _service?.Dispose();
            _bluetoothDevice?.Dispose();
        }
    }
}

