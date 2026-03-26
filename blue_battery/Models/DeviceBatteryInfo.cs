using System;

namespace BlueBattery.Models;

public sealed class DeviceBatteryInfo
{
    public string DeviceId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int? BatteryPercent { get; init; }

    public string ConnectionStateText { get; init; } = "已连接";

    public string SourceKindText { get; init; } = "GATT BAS";

    public DateTimeOffset? LastUpdatedUtc { get; init; }

    public DeviceSnapshotState SnapshotState { get; init; } = DeviceSnapshotState.Live;

    public bool IsStale => SnapshotState != DeviceSnapshotState.Live;

    public string BatteryText => BatteryPercent is int value
        ? $"{Math.Clamp(value, 0, 100)}%"
        : "--";

    public string MetaText => $"{ConnectionStateText} · {SourceKindText}";

    public string TimestampText => LastUpdatedUtc is DateTimeOffset timestamp
        ? $"更新于 {timestamp.ToLocalTime():HH:mm:ss}"
        : "等待首次读取";

    public string FreshnessText => SnapshotState switch
    {
        DeviceSnapshotState.Live => "最新值",
        DeviceSnapshotState.RestoredCache => "启动缓存",
        DeviceSnapshotState.RefreshFailedCache => "失败缓存",
        _ => "缓存值",
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
}
