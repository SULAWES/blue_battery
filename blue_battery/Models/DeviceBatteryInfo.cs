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

    public bool IsStale { get; init; }

    public string BatteryText => BatteryPercent is int value
        ? $"{Math.Clamp(value, 0, 100)}%"
        : "--";

    public string MetaText => $"{ConnectionStateText} · {SourceKindText}";

    public string TimestampText => LastUpdatedUtc is DateTimeOffset timestamp
        ? $"更新于 {timestamp.ToLocalTime():HH:mm:ss}"
        : "等待首次读取";

    public string FreshnessText => IsStale ? "缓存值" : "最新值";
}
