using System;
using System.Collections.Generic;

namespace BlueBattery.Models;

public sealed class DeviceBatteryInfoComparer : IComparer<DeviceBatteryInfo>
{
    public static DeviceBatteryInfoComparer Instance { get; } = new();

    public int Compare(DeviceBatteryInfo? x, DeviceBatteryInfo? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return 1;
        }

        if (y is null)
        {
            return -1;
        }

        int freshnessComparison = x.IsStale.CompareTo(y.IsStale);
        if (freshnessComparison != 0)
        {
            return freshnessComparison;
        }

        int batteryAvailabilityComparison = CompareBatteryAvailability(x.BatteryPercent, y.BatteryPercent);
        if (batteryAvailabilityComparison != 0)
        {
            return batteryAvailabilityComparison;
        }

        if (x.BatteryPercent.HasValue && y.BatteryPercent.HasValue)
        {
            int batteryComparison = x.BatteryPercent.Value.CompareTo(y.BatteryPercent.Value);
            if (batteryComparison != 0)
            {
                return batteryComparison;
            }
        }

        int timestampComparison = Nullable.Compare(y.LastUpdatedUtc, x.LastUpdatedUtc);
        if (timestampComparison != 0)
        {
            return timestampComparison;
        }

        int nameComparison = StringComparer.CurrentCultureIgnoreCase.Compare(x.DisplayName, y.DisplayName);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        return StringComparer.Ordinal.Compare(x.DeviceId, y.DeviceId);
    }

    private static int CompareBatteryAvailability(int? left, int? right)
    {
        bool leftHasValue = left.HasValue;
        bool rightHasValue = right.HasValue;

        if (leftHasValue == rightHasValue)
        {
            return 0;
        }

        return leftHasValue ? -1 : 1;
    }
}
