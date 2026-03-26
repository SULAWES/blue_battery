using System;
using System.Globalization;

namespace BlueBattery.Resources.Strings;

public static class AppStrings
{
    private static readonly AppStringCatalog ZhCn = new()
    {
        AppTitle = "blue_battery",
        PanelSubtitle = "显示当前已连接且可通过公开标准接口读取电量的设备。",
        ScopeHint = "仅显示 Windows 可原生读取电量且本应用读取成功的设备。",
        InfoBarMessage = "当前面板会显示实时值、启动恢复缓存和刷新失败缓存，并区分设备断开或暂不可读的状态。",
        DeviceCountLabel = "已显示设备",
        LowestBatteryLabel = "最低电量",
        CurrentStatusLabel = "当前状态",
        DeviceSectionTitle = "设备列表",
        InitialStatusMessage = "等待蓝牙读取服务接入。",
        InitialEmptyStateTitle = "暂无可显示设备",
        InitialEmptyStateDescription = "设备列表结构已经就绪。接入蓝牙读取后，这里会只显示已连接且通过 GATT Battery Service 成功读取到电量的设备。",
        NoSuccessfulRefreshYet = "尚无成功刷新",
        LastRefreshFormat = "最近成功刷新 {0}",
        ConnectedStateText = "已连接",
        DisconnectedStateText = "已断开",
        GattBasSourceText = "GATT BAS",
        UnknownDeviceName = "未知蓝牙设备",
        WaitingForFirstRead = "等待首次读取",
        FreshnessLatest = "最新值",
        FreshnessRestoredCache = "启动缓存",
        FreshnessRefreshFailedCache = "失败缓存",
        FreshnessDisconnected = "已断开",
        FreshnessFallbackCache = "缓存值",
        SettingsReservedStatus = "设置入口已预留，首版稍后接入。",
        SettingsReservedMessage = "设置入口已预留，当前版本尚未接入实际设置页。",
        AboutOpenedStatus = "已打开关于信息。",
        AboutCaption = "关于 blue_battery",
        AboutMessage = "blue_battery\r\nWinUI 3 托盘电量应用原型。\r\n当前阶段：托盘壳层、单实例与设备列表承载结构。",
        RefreshingBatteryStatus = "正在刷新蓝牙设备电量...",
        RestoredSnapshotTitle = "暂无实时数据",
        RestoredSnapshotDescription = "当前显示上次成功刷新保存的缓存值，应用正在读取最新蓝牙设备状态。",
        RestoredSnapshotStatus = "已恢复上次成功快照，正在读取最新设备状态...",
        RefreshFailedTitle = "刷新失败",
        RefreshFailedDescription = "当前未能读取蓝牙设备电量。请确认设备仍已连接，并稍后再次手动刷新。",
        EmptyStateNoConnectedTitle = "没有已连接蓝牙设备",
        EmptyStateNoConnectedDescription = "当前没有已连接的 Bluetooth LE 设备。连接受支持设备后，面板会自动刷新。",
        EmptyStateNoReadableTitle = "没有可读取电量的设备",
        EmptyStateNoReadableDescription = "已连接设备中没有通过公开标准接口读取到电量。应用只显示 BAS 读取成功的设备。",
        EmptyStateMissingTitle = "设备已断开或暂不可读",
        EmptyStateMissingDescriptionFormat = "此前显示的 {0} 台设备当前已断开，或暂时无法通过公开标准接口读取电量。",
        StatusRefreshSuccessFormat = "已刷新 {0} 台设备电量。已连接 LE 设备 {1} 台。时间 {2}。",
        StatusRefreshSuccessWithMissingFormat = "已刷新 {0} 台设备电量。另有 {1} 台设备已断开或暂不可读。时间 {2}。",
        StatusOnlyMissingFormat = "此前显示的 {0} 台设备当前已断开，或暂时无法读取电量。时间 {1}。",
        StatusNoReadableFormat = "已连接 LE 设备 {0} 台，但没有读取到可显示的公开电量数据。时间 {1}。",
        StatusNoConnectedFormat = "当前没有已连接的 LE 设备。时间 {0}。",
        StatusRefreshFailedCacheFormat = "刷新失败，当前显示上次成功读取的缓存值。{0}。{1}",
        StatusRefreshFailedFormat = "刷新失败。{0}。{1}",
        StatusAutoRefreshFormat = "{0} 正在自动刷新设备列表...",
        TooltipNoReadable = "blue_battery · 无可读取电量设备",
        TooltipRefreshFailed = "blue_battery · 刷新失败",
        TooltipDisconnected = "blue_battery · 设备已断开",
        TooltipSummaryFormat = "blue_battery · {0} 台设备 · 最低 {1}%",
        MenuRefresh = "立即刷新",
        MenuSettings = "设置",
        MenuAbout = "关于",
        MenuExit = "退出",
        ReasonBatteryChanged = "检测到设备电量变化。",
        ReasonPolling = "正在按轮询策略刷新设备电量。",
        ReasonDeviceAdded = "检测到蓝牙设备接入。",
        ReasonDeviceUpdated = "检测到蓝牙设备状态变化。",
        ReasonDeviceRemoved = "检测到蓝牙设备断开。",
    };

    private static AppStringCatalog Current => CultureInfo.CurrentUICulture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)
        ? ZhCn
        : ZhCn;

    public static string AppTitle => Current.AppTitle;
    public static string PanelSubtitle => Current.PanelSubtitle;
    public static string ScopeHint => Current.ScopeHint;
    public static string InfoBarMessage => Current.InfoBarMessage;
    public static string DeviceCountLabel => Current.DeviceCountLabel;
    public static string LowestBatteryLabel => Current.LowestBatteryLabel;
    public static string CurrentStatusLabel => Current.CurrentStatusLabel;
    public static string DeviceSectionTitle => Current.DeviceSectionTitle;
    public static string InitialStatusMessage => Current.InitialStatusMessage;
    public static string InitialEmptyStateTitle => Current.InitialEmptyStateTitle;
    public static string InitialEmptyStateDescription => Current.InitialEmptyStateDescription;
    public static string NoSuccessfulRefreshYet => Current.NoSuccessfulRefreshYet;
    public static string ConnectedStateText => Current.ConnectedStateText;
    public static string DisconnectedStateText => Current.DisconnectedStateText;
    public static string GattBasSourceText => Current.GattBasSourceText;
    public static string UnknownDeviceName => Current.UnknownDeviceName;
    public static string WaitingForFirstRead => Current.WaitingForFirstRead;
    public static string FreshnessLatest => Current.FreshnessLatest;
    public static string FreshnessRestoredCache => Current.FreshnessRestoredCache;
    public static string FreshnessRefreshFailedCache => Current.FreshnessRefreshFailedCache;
    public static string FreshnessDisconnected => Current.FreshnessDisconnected;
    public static string FreshnessFallbackCache => Current.FreshnessFallbackCache;
    public static string SettingsReservedStatus => Current.SettingsReservedStatus;
    public static string SettingsReservedMessage => Current.SettingsReservedMessage;
    public static string AboutOpenedStatus => Current.AboutOpenedStatus;
    public static string AboutCaption => Current.AboutCaption;
    public static string AboutMessage => Current.AboutMessage;
    public static string RefreshingBatteryStatus => Current.RefreshingBatteryStatus;
    public static string RestoredSnapshotTitle => Current.RestoredSnapshotTitle;
    public static string RestoredSnapshotDescription => Current.RestoredSnapshotDescription;
    public static string RestoredSnapshotStatus => Current.RestoredSnapshotStatus;
    public static string RefreshFailedTitle => Current.RefreshFailedTitle;
    public static string RefreshFailedDescription => Current.RefreshFailedDescription;
    public static string EmptyStateNoConnectedTitle => Current.EmptyStateNoConnectedTitle;
    public static string EmptyStateNoConnectedDescription => Current.EmptyStateNoConnectedDescription;
    public static string EmptyStateNoReadableTitle => Current.EmptyStateNoReadableTitle;
    public static string EmptyStateNoReadableDescription => Current.EmptyStateNoReadableDescription;
    public static string EmptyStateMissingTitle => Current.EmptyStateMissingTitle;
    public static string TooltipNoReadable => Current.TooltipNoReadable;
    public static string TooltipRefreshFailed => Current.TooltipRefreshFailed;
    public static string TooltipDisconnected => Current.TooltipDisconnected;
    public static string MenuRefresh => Current.MenuRefresh;
    public static string MenuSettings => Current.MenuSettings;
    public static string MenuAbout => Current.MenuAbout;
    public static string MenuExit => Current.MenuExit;
    public static string ReasonBatteryChanged => Current.ReasonBatteryChanged;
    public static string ReasonPolling => Current.ReasonPolling;
    public static string ReasonDeviceAdded => Current.ReasonDeviceAdded;
    public static string ReasonDeviceUpdated => Current.ReasonDeviceUpdated;
    public static string ReasonDeviceRemoved => Current.ReasonDeviceRemoved;

    public static string BuildLastRefreshText(DateTimeOffset timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.LastRefreshFormat, timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture));

    public static string BuildEmptyStateMissingDescription(int missingDeviceCount)
        => string.Format(CultureInfo.CurrentCulture, Current.EmptyStateMissingDescriptionFormat, missingDeviceCount);

    public static string BuildStatusRefreshSuccess(int displayedDeviceCount, int connectedLeDeviceCount, string timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusRefreshSuccessFormat, displayedDeviceCount, connectedLeDeviceCount, timestamp);

    public static string BuildStatusRefreshSuccessWithMissing(int displayedDeviceCount, int missingDeviceCount, string timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusRefreshSuccessWithMissingFormat, displayedDeviceCount, missingDeviceCount, timestamp);

    public static string BuildStatusOnlyMissing(int missingDeviceCount, string timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusOnlyMissingFormat, missingDeviceCount, timestamp);

    public static string BuildStatusNoReadable(int connectedLeDeviceCount, string timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusNoReadableFormat, connectedLeDeviceCount, timestamp);

    public static string BuildStatusNoConnected(string timestamp)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusNoConnectedFormat, timestamp);

    public static string BuildStatusRefreshFailedCache(string timestamp, string message)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusRefreshFailedCacheFormat, timestamp, message);

    public static string BuildStatusRefreshFailed(string timestamp, string message)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusRefreshFailedFormat, timestamp, message);

    public static string BuildStatusAutoRefresh(string reason)
        => string.Format(CultureInfo.CurrentCulture, Current.StatusAutoRefreshFormat, reason);

    public static string BuildTooltipSummary(int deviceCount, int lowestBattery)
        => string.Format(CultureInfo.CurrentCulture, Current.TooltipSummaryFormat, deviceCount, lowestBattery);

    private sealed class AppStringCatalog
    {
        public required string AppTitle { get; init; }
        public required string PanelSubtitle { get; init; }
        public required string ScopeHint { get; init; }
        public required string InfoBarMessage { get; init; }
        public required string DeviceCountLabel { get; init; }
        public required string LowestBatteryLabel { get; init; }
        public required string CurrentStatusLabel { get; init; }
        public required string DeviceSectionTitle { get; init; }
        public required string InitialStatusMessage { get; init; }
        public required string InitialEmptyStateTitle { get; init; }
        public required string InitialEmptyStateDescription { get; init; }
        public required string NoSuccessfulRefreshYet { get; init; }
        public required string LastRefreshFormat { get; init; }
        public required string ConnectedStateText { get; init; }
        public required string DisconnectedStateText { get; init; }
        public required string GattBasSourceText { get; init; }
        public required string UnknownDeviceName { get; init; }
        public required string WaitingForFirstRead { get; init; }
        public required string FreshnessLatest { get; init; }
        public required string FreshnessRestoredCache { get; init; }
        public required string FreshnessRefreshFailedCache { get; init; }
        public required string FreshnessDisconnected { get; init; }
        public required string FreshnessFallbackCache { get; init; }
        public required string SettingsReservedStatus { get; init; }
        public required string SettingsReservedMessage { get; init; }
        public required string AboutOpenedStatus { get; init; }
        public required string AboutCaption { get; init; }
        public required string AboutMessage { get; init; }
        public required string RefreshingBatteryStatus { get; init; }
        public required string RestoredSnapshotTitle { get; init; }
        public required string RestoredSnapshotDescription { get; init; }
        public required string RestoredSnapshotStatus { get; init; }
        public required string RefreshFailedTitle { get; init; }
        public required string RefreshFailedDescription { get; init; }
        public required string EmptyStateNoConnectedTitle { get; init; }
        public required string EmptyStateNoConnectedDescription { get; init; }
        public required string EmptyStateNoReadableTitle { get; init; }
        public required string EmptyStateNoReadableDescription { get; init; }
        public required string EmptyStateMissingTitle { get; init; }
        public required string EmptyStateMissingDescriptionFormat { get; init; }
        public required string StatusRefreshSuccessFormat { get; init; }
        public required string StatusRefreshSuccessWithMissingFormat { get; init; }
        public required string StatusOnlyMissingFormat { get; init; }
        public required string StatusNoReadableFormat { get; init; }
        public required string StatusNoConnectedFormat { get; init; }
        public required string StatusRefreshFailedCacheFormat { get; init; }
        public required string StatusRefreshFailedFormat { get; init; }
        public required string StatusAutoRefreshFormat { get; init; }
        public required string TooltipNoReadable { get; init; }
        public required string TooltipRefreshFailed { get; init; }
        public required string TooltipDisconnected { get; init; }
        public required string TooltipSummaryFormat { get; init; }
        public required string MenuRefresh { get; init; }
        public required string MenuSettings { get; init; }
        public required string MenuAbout { get; init; }
        public required string MenuExit { get; init; }
        public required string ReasonBatteryChanged { get; init; }
        public required string ReasonPolling { get; init; }
        public required string ReasonDeviceAdded { get; init; }
        public required string ReasonDeviceUpdated { get; init; }
        public required string ReasonDeviceRemoved { get; init; }
    }
}
