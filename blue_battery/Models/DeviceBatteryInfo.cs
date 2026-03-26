using System;
using BlueBattery.Resources.Strings;

namespace BlueBattery.Models;

public sealed class DeviceBatteryInfo
{
    public string DeviceId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int? BatteryPercent { get; init; }

    public string ConnectionStateText { get; init; } = AppStrings.ConnectedStateText;

    public string SourceKindText { get; init; } = AppStrings.GattBasSourceText;

    public DateTimeOffset? LastUpdatedUtc { get; init; }

    public DeviceSnapshotState SnapshotState { get; init; } = DeviceSnapshotState.Live;

    public bool IsStale => SnapshotState != DeviceSnapshotState.Live;

    public string BatteryText => BatteryPercent is int value
        ? $"{Math.Clamp(value, 0, 100)}%"
        : "--";

    public string MetaText => $"{ConnectionStateText} · {SourceKindText}";

    public string TimestampText => LastUpdatedUtc is DateTimeOffset timestamp
        ? $"更新于 {timestamp.ToLocalTime():HH:mm:ss}"
        : AppStrings.WaitingForFirstRead;

    public string FreshnessText => SnapshotState switch
    {
        DeviceSnapshotState.Live => AppStrings.FreshnessLatest,
        DeviceSnapshotState.RestoredCache => AppStrings.FreshnessRestoredCache,
        DeviceSnapshotState.RefreshFailedCache => AppStrings.FreshnessRefreshFailedCache,
        DeviceSnapshotState.Disconnected => AppStrings.FreshnessDisconnected,
        _ => AppStrings.FreshnessFallbackCache,
    };

    public DeviceBatteryInfo ToStaleSnapshot(DeviceSnapshotState snapshotState = DeviceSnapshotState.RefreshFailedCache)
    {
        return new DeviceBatteryInfo
        {
            DeviceId = DeviceId,
            DisplayName = DisplayName,
            BatteryPercent = BatteryPercent,
            ConnectionStateText = ConnectionStateText,
            SourceKindText = SourceKindText,
            LastUpdatedUtc = LastUpdatedUtc,
            SnapshotState = snapshotState,
        };
    }

    public DeviceBatteryInfo ToDisconnectedSnapshot()
    {
        return new DeviceBatteryInfo
        {
            DeviceId = DeviceId,
            DisplayName = DisplayName,
            BatteryPercent = BatteryPercent,
            ConnectionStateText = AppStrings.DisconnectedStateText,
            SourceKindText = SourceKindText,
            LastUpdatedUtc = LastUpdatedUtc,
            SnapshotState = DeviceSnapshotState.Disconnected,
        };
    }
}
