using System;
using System.Collections.Generic;
using BlueBattery.Models;

namespace BlueBattery.Services.State;

public sealed class AppStateSnapshot
{
    public DateTimeOffset? LastSuccessfulRefreshUtc { get; init; }

    public IReadOnlyList<DeviceBatteryInfo> Devices { get; init; } = Array.Empty<DeviceBatteryInfo>();
}
