using System;
using System.Collections.Generic;
using System.Linq;
using BlueBattery.Models;

namespace BlueBattery.Services.Presentation;

public sealed class DisplayedDeviceTracker
{
    private readonly Dictionary<string, DeviceBatteryInfo> _displayedDevices = new(StringComparer.Ordinal);

    public int CountMissingDevices(IEnumerable<DeviceBatteryInfo> currentDevices)
    {
        if (_displayedDevices.Count == 0)
        {
            return 0;
        }

        HashSet<string> currentDeviceIds = currentDevices
            .Select(device => device.DeviceId)
            .Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .ToHashSet(StringComparer.Ordinal);

        return _displayedDevices.Keys.Count(deviceId => !currentDeviceIds.Contains(deviceId));
    }

    public IReadOnlyList<DeviceBatteryInfo> MergeWithDisconnectedSnapshots(IReadOnlyList<DeviceBatteryInfo> currentDevices)
    {
        if (_displayedDevices.Count == 0)
        {
            return currentDevices;
        }

        Dictionary<string, DeviceBatteryInfo> currentById = currentDevices
            .Where(static device => !string.IsNullOrWhiteSpace(device.DeviceId))
            .ToDictionary(device => device.DeviceId, StringComparer.Ordinal);

        List<DeviceBatteryInfo> mergedDevices = [.. currentDevices];

        foreach ((string deviceId, DeviceBatteryInfo previousDevice) in _displayedDevices)
        {
            if (!currentById.ContainsKey(deviceId))
            {
                mergedDevices.Add(previousDevice.ToDisconnectedSnapshot());
            }
        }

        return mergedDevices;
    }

    public void Track(IEnumerable<DeviceBatteryInfo> devices)
    {
        _displayedDevices.Clear();

        foreach (DeviceBatteryInfo device in devices.Where(static device => !string.IsNullOrWhiteSpace(device.DeviceId)))
        {
            _displayedDevices[device.DeviceId] = device;
        }
    }

    public void Clear()
    {
        _displayedDevices.Clear();
    }
}
