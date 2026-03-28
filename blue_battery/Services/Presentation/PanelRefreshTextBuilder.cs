using System;
using System.Collections.Generic;
using System.Linq;
using BlueBattery.Models;
using BlueBattery.Resources.Strings;
using BlueBattery.Services.Bluetooth;

namespace BlueBattery.Services.Presentation;

public sealed class PanelRefreshTextBuilder
{
    public PanelEmptyState BuildEmptyState(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (missingDeviceCount > 0)
        {
            return new PanelEmptyState(
                AppStrings.EmptyStateMissingTitle,
                AppStrings.BuildEmptyStateMissingDescription(missingDeviceCount));
        }

        if (result.ConnectedLeDeviceCount == 0)
        {
            return new PanelEmptyState(
                AppStrings.EmptyStateNoConnectedTitle,
                AppStrings.EmptyStateNoConnectedDescription);
        }

        return new PanelEmptyState(
            AppStrings.EmptyStateNoReadableTitle,
            AppStrings.EmptyStateNoReadableDescription);
    }

    public string BuildStatusMessage(BluetoothRefreshResult result, int missingDeviceCount, DateTimeOffset? timestamp = null)
    {
        string formattedTimestamp = (timestamp ?? DateTimeOffset.Now).ToLocalTime().ToString("HH:mm:ss");

        if (result.Devices.Count > 0)
        {
            if (missingDeviceCount > 0)
            {
                return AppStrings.BuildStatusRefreshSuccessWithMissing(
                    result.Devices.Count,
                    missingDeviceCount,
                    formattedTimestamp);
            }

            return AppStrings.BuildStatusRefreshSuccess(
                result.Devices.Count,
                result.ConnectedLeDeviceCount,
                formattedTimestamp);
        }

        if (missingDeviceCount > 0)
        {
            return AppStrings.BuildStatusOnlyMissing(missingDeviceCount, formattedTimestamp);
        }

        if (result.ConnectedLeDeviceCount > 0)
        {
            return AppStrings.BuildStatusNoReadable(result.ConnectedLeDeviceCount, formattedTimestamp);
        }

        return AppStrings.BuildStatusNoConnected(formattedTimestamp);
    }

    public string BuildTooltip(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (result.Devices.Count == 0)
        {
            if (missingDeviceCount > 0)
            {
                return AppStrings.TooltipDisconnected;
            }

            return result.ConnectedLeDeviceCount > 0
                ? AppStrings.TooltipNoReadable
                : AppStrings.AppTitle;
        }

        int lowestBattery = result.Devices
            .Where(device => device.BatteryPercent.HasValue)
            .Select(device => device.BatteryPercent!.Value)
            .DefaultIfEmpty(0)
            .Min();

        return AppStrings.BuildTooltipSummary(result.Devices.Count, lowestBattery);
    }

    public int? GetLowestBatteryPercent(IEnumerable<DeviceBatteryInfo> devices)
    {
        return devices
            .Where(device => device.BatteryPercent.HasValue)
            .Select(device => (int?)device.BatteryPercent!.Value)
            .Min();
    }

    public string BuildAutoRefreshStatus(string reason)
    {
        return AppStrings.BuildStatusAutoRefresh(reason);
    }
}

public readonly record struct PanelEmptyState(string Title, string Description);
