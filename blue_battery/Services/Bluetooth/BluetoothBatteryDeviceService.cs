using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBattery.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BlueBattery.Services.Bluetooth;

public sealed class BluetoothBatteryDeviceService
{
    public async Task<BluetoothRefreshResult> GetConnectedDevicesAsync(CancellationToken cancellationToken = default)
    {
        string selector = BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected);
        DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);
        List<DeviceBatteryInfo> supportedDevices = [];

        foreach (DeviceInformation deviceInfo in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DeviceBatteryInfo? deviceBatteryInfo = await TryReadBatteryAsync(deviceInfo);
            if (deviceBatteryInfo is not null)
            {
                supportedDevices.Add(deviceBatteryInfo);
            }
        }

        List<DeviceBatteryInfo> orderedDevices = supportedDevices
            .OrderBy(device => device.BatteryPercent ?? int.MaxValue)
            .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new BluetoothRefreshResult
        {
            Devices = orderedDevices,
            ConnectedLeDeviceCount = devices.Count,
        };
    }

    private static async Task<DeviceBatteryInfo?> TryReadBatteryAsync(DeviceInformation deviceInfo)
    {
        try
        {
            using BluetoothLEDevice? bluetoothDevice = await BluetoothLEDevice.FromIdAsync(deviceInfo.Id);
            if (bluetoothDevice is null)
            {
                return null;
            }

            GattDeviceServicesResult servicesResult = await bluetoothDevice.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery,
                BluetoothCacheMode.Uncached);

            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                return null;
            }

            foreach (GattDeviceService service in servicesResult.Services)
            {
                using (service)
                {
                    GattCharacteristicsResult characteristicsResult = await service.GetCharacteristicsForUuidAsync(
                        GattCharacteristicUuids.BatteryLevel,
                        BluetoothCacheMode.Uncached);

                    if (characteristicsResult.Status != GattCommunicationStatus.Success)
                    {
                        continue;
                    }

                    foreach (GattCharacteristic characteristic in characteristicsResult.Characteristics)
                    {
                        GattReadResult readResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                        if (readResult.Status != GattCommunicationStatus.Success || readResult.Value is null)
                        {
                            continue;
                        }

                        byte? batteryPercent = TryReadBatteryPercent(readResult.Value);
                        if (!batteryPercent.HasValue)
                        {
                            continue;
                        }

                        return new DeviceBatteryInfo
                        {
                            DeviceId = deviceInfo.Id,
                            DisplayName = ResolveDisplayName(deviceInfo, bluetoothDevice),
                            BatteryPercent = batteryPercent.Value,
                            ConnectionStateText = "已连接",
                            SourceKindText = "GATT BAS",
                            LastUpdatedUtc = DateTimeOffset.UtcNow,
                            IsStale = false,
                        };
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string ResolveDisplayName(DeviceInformation deviceInfo, BluetoothLEDevice bluetoothDevice)
    {
        if (!string.IsNullOrWhiteSpace(bluetoothDevice.Name))
        {
            return bluetoothDevice.Name;
        }

        if (!string.IsNullOrWhiteSpace(deviceInfo.Name))
        {
            return deviceInfo.Name;
        }

        return "未知蓝牙设备";
    }

    private static byte? TryReadBatteryPercent(IBuffer value)
    {
        if (value.Length < 1)
        {
            return null;
        }

        using DataReader reader = DataReader.FromBuffer(value);
        return reader.ReadByte();
    }
}
