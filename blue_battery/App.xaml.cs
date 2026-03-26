using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBattery.Diagnostics;
using BlueBattery.Models;
using BlueBattery.Services.Bluetooth;
using BlueBattery.Services.State;
using BlueBattery.Services.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Collections.Generic;

namespace BlueBattery;

public partial class App : Application
{
    private const string DefaultTooltip = "blue_battery";
    private readonly IBluetoothDeviceDiscoveryService _bluetoothDeviceDiscoveryService = new BluetoothDeviceDiscoveryService();
    private readonly IBatteryTelemetryService _bluetoothBatteryTelemetryService = new BluetoothBatteryTelemetryService();
    private readonly IAppStateStore _appStateStore = new JsonAppStateStore();
    private MainWindow? _hostWindow;
    private PanelWindow? _panelWindow;
    private TrayIconService? _trayIconService;
    private bool _hasLoadedDeviceSnapshot;
    private bool _hasAttemptedStateRestore;
    private bool _isRefreshingDevices;
    private bool _pendingRefreshRequested;
    private CancellationTokenSource? _autoRefreshDebounceCts;
    private DateTimeOffset? _lastSuccessfulRefreshUtc;
    private AppStateSnapshot? _restoredSnapshot;
    private readonly HashSet<string> _displayedDeviceIds = new(StringComparer.Ordinal);

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SingleInstanceDiagnostics.Log("App.OnLaunched");
        Program.RedirectedActivationRequested += OnRedirectedActivationRequested;

        _hostWindow = new MainWindow();
        _hostWindow.Activate();
        _hostWindow.HideHostWindow();

        _trayIconService = new TrayIconService(_hostWindow.WindowHandle);
        _trayIconService.OpenRequested += OnOpenRequested;
        _trayIconService.RefreshRequested += OnRefreshRequested;
        _trayIconService.SettingsRequested += OnSettingsRequested;
        _trayIconService.AboutRequested += OnAboutRequested;
        _trayIconService.ExitRequested += OnExitRequested;
        _trayIconService.Show();
        _trayIconService.UpdateTooltip(DefaultTooltip);

        _bluetoothDeviceDiscoveryService.RefreshRequested += OnAutoRefreshRequested;
        _bluetoothBatteryTelemetryService.RefreshRequested += OnAutoRefreshRequested;
    }

    private void OnOpenRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(async () =>
        {
            if (_hostWindow is null)
            {
                return;
            }

            EnsurePanelWindow();
            if (_panelWindow is null)
            {
                return;
            }

            if (_panelWindow.IsVisible)
            {
                _panelWindow.HidePanel();
                return;
            }

            OpenPanel();

            if (!_hasLoadedDeviceSnapshot)
            {
                await RestoreSnapshotIfAvailableAsync();
                await RefreshDevicesAsync(forceRefresh: true);
            }
        });
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(async () =>
        {
            EnsurePanelWindow();
            await RefreshDevicesAsync(forceRefresh: true);
        });
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdateStatusMessage("设置入口已预留，首版稍后接入。");
            NativeMessageBox("设置入口已预留，当前版本尚未接入实际设置页。", "blue_battery");
        });
    }

    private void OnAboutRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdateStatusMessage("已打开关于信息。");
            NativeMessageBox("blue_battery\r\nWinUI 3 托盘电量应用原型。\r\n当前阶段：托盘壳层、单实例与设备列表承载结构。", "关于 blue_battery");
        });
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            SingleInstanceDiagnostics.Log("App.OnExitRequested");
            Program.RedirectedActivationRequested -= OnRedirectedActivationRequested;
            _autoRefreshDebounceCts?.Cancel();
            _bluetoothDeviceDiscoveryService.RefreshRequested -= OnAutoRefreshRequested;
            _bluetoothDeviceDiscoveryService.Dispose();
            _bluetoothBatteryTelemetryService.RefreshRequested -= OnAutoRefreshRequested;
            _bluetoothBatteryTelemetryService.Dispose();
            _panelWindow?.ForceClose();
            _trayIconService?.Dispose();
            _hostWindow?.Close();
            Exit();
        });
    }

    private void OnRedirectedActivationRequested(object? sender, AppActivationArguments args)
    {
        SingleInstanceDiagnostics.Log("App.OnRedirectedActivationRequested", args);
        EnqueueOnUiThread(async () =>
        {
            if (_hostWindow is null)
            {
                return;
            }

            _hostWindow.Activate();
            OpenPanel();

            if (!_hasLoadedDeviceSnapshot)
            {
                await RestoreSnapshotIfAvailableAsync();
                await RefreshDevicesAsync(forceRefresh: true);
            }
        });
    }

    private void EnsurePanelWindow()
    {
        if (_panelWindow is null || _panelWindow.IsClosed)
        {
            _panelWindow = new PanelWindow();
        }
    }

    private void OpenPanel()
    {
        EnsurePanelWindow();

        if (_trayIconService is not null && _trayIconService.TryGetIconRect(out var rect))
        {
            _panelWindow?.ShowNearTrayIcon(rect);
            return;
        }

        _panelWindow?.ShowNearCursor();
    }

    private async Task RestoreSnapshotIfAvailableAsync()
    {
        if (_hasAttemptedStateRestore)
        {
            return;
        }

        _hasAttemptedStateRestore = true;
        _restoredSnapshot = await _appStateStore.LoadAsync();
        if (_restoredSnapshot is null)
        {
            return;
        }

        _lastSuccessfulRefreshUtc = _restoredSnapshot.LastSuccessfulRefreshUtc;
        if (_restoredSnapshot.Devices.Count == 0)
        {
            return;
        }

        EnsurePanelWindow();
        DeviceBatteryInfo[] staleDevices = _restoredSnapshot.Devices
            .Select(device => device.ToStaleSnapshot(DeviceSnapshotState.RestoredCache))
            .ToArray();

        _panelWindow?.SetDevices(staleDevices);
        _panelWindow?.UpdateEmptyState("暂无实时数据", "当前显示上次成功刷新保存的缓存值，应用正在读取最新蓝牙设备状态。");
        _panelWindow?.UpdateLastRefresh(_lastSuccessfulRefreshUtc);
        _panelWindow?.UpdateStatusMessage("已恢复上次成功快照，正在读取最新设备状态...");
        _trayIconService?.UpdateTooltip(BuildTooltip(new BluetoothRefreshResult
        {
            Devices = staleDevices,
            ConnectedLeDeviceCount = staleDevices.Length,
        }, missingDeviceCount: 0));
        _trayIconService?.UpdateBatteryIcon(GetLowestBatteryPercent(staleDevices));
        UpdateDisplayedDeviceIds(staleDevices);
    }

    private async Task RefreshDevicesAsync(bool forceRefresh)
    {
        if (_isRefreshingDevices)
        {
            _pendingRefreshRequested = true;
            return;
        }

        if (!forceRefresh && _hasLoadedDeviceSnapshot)
        {
            return;
        }

        _isRefreshingDevices = true;
        EnsurePanelWindow();
        _panelWindow?.UpdateStatusMessage("正在刷新蓝牙设备电量...");

        try
        {
            BluetoothRefreshResult result = await _bluetoothDeviceDiscoveryService.GetConnectedDevicesAsync();
            int missingDeviceCount = CountMissingDevices(result.Devices);
            _panelWindow?.SetDevices(result.Devices);
            _panelWindow?.UpdateEmptyState(
                BuildEmptyStateTitle(result, missingDeviceCount),
                BuildEmptyStateDescription(result, missingDeviceCount));
            _panelWindow?.UpdateStatusMessage(BuildStatusMessage(result, missingDeviceCount));
            _trayIconService?.UpdateTooltip(BuildTooltip(result, missingDeviceCount));
            _trayIconService?.UpdateBatteryIcon(GetLowestBatteryPercent(result.Devices));
            await _bluetoothBatteryTelemetryService.UpdateTrackedDevicesAsync(result.Devices.Select(device => device.DeviceId));
            _lastSuccessfulRefreshUtc = DateTimeOffset.UtcNow;
            _panelWindow?.UpdateLastRefresh(_lastSuccessfulRefreshUtc);
            _hasLoadedDeviceSnapshot = true;
            _restoredSnapshot = new AppStateSnapshot
            {
                LastSuccessfulRefreshUtc = _lastSuccessfulRefreshUtc,
                Devices = result.Devices.ToArray(),
            };
            await _appStateStore.SaveAsync(_restoredSnapshot);
            UpdateDisplayedDeviceIds(result.Devices);
            EnsureDiscoveryMonitoringStarted();
        }
        catch (Exception ex)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (_panelWindow?.HasDevices == true)
            {
                _panelWindow.MarkDevicesAsStale(DeviceSnapshotState.RefreshFailedCache);
                _panelWindow.UpdateStatusMessage($"刷新失败，当前显示上次成功读取的缓存值。{timestamp}。{ex.Message}");
            }
            else
            {
                _panelWindow?.SetDevices(Array.Empty<DeviceBatteryInfo>());
                _panelWindow?.UpdateEmptyState(
                    "刷新失败",
                    "当前未能读取蓝牙设备电量。请确认设备仍已连接，并稍后再次手动刷新。");
                _panelWindow?.UpdateStatusMessage($"刷新失败。{timestamp}。{ex.Message}");
                _trayIconService?.UpdateBatteryIcon(null);
                _displayedDeviceIds.Clear();
            }

            _panelWindow?.UpdateLastRefresh(_lastSuccessfulRefreshUtc);
            _trayIconService?.UpdateTooltip($"{DefaultTooltip} · 刷新失败");
        }
        finally
        {
            _isRefreshingDevices = false;
        }

        if (_pendingRefreshRequested)
        {
            _pendingRefreshRequested = false;
            await RefreshDevicesAsync(forceRefresh: true);
        }
    }

    private static string BuildStatusMessage(BluetoothRefreshResult result, int missingDeviceCount)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        if (result.Devices.Count > 0)
        {
            if (missingDeviceCount > 0)
            {
                return $"已刷新 {result.Devices.Count} 台设备电量。另有 {missingDeviceCount} 台设备已断开或暂不可读。时间 {timestamp}。";
            }

            return $"已刷新 {result.Devices.Count} 台设备电量。已连接 LE 设备 {result.ConnectedLeDeviceCount} 台。时间 {timestamp}。";
        }

        if (missingDeviceCount > 0)
        {
            return $"此前显示的 {missingDeviceCount} 台设备当前已断开，或暂时无法读取电量。时间 {timestamp}。";
        }

        if (result.ConnectedLeDeviceCount > 0)
        {
            return $"已连接 LE 设备 {result.ConnectedLeDeviceCount} 台，但没有读取到可显示的公开电量数据。时间 {timestamp}。";
        }

        return $"当前没有已连接的 LE 设备。时间 {timestamp}。";
    }

    private static string BuildTooltip(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (result.Devices.Count == 0)
        {
            if (missingDeviceCount > 0)
            {
                return $"{DefaultTooltip} · 设备已断开";
            }

            return result.ConnectedLeDeviceCount > 0
                ? $"{DefaultTooltip} · 无可读取电量设备"
                : DefaultTooltip;
        }

        int lowestBattery = result.Devices
            .Where(device => device.BatteryPercent.HasValue)
            .Select(device => device.BatteryPercent!.Value)
            .DefaultIfEmpty(0)
            .Min();

        return $"{DefaultTooltip} · {result.Devices.Count} 台设备 · 最低 {lowestBattery}%";
    }

    private static int? GetLowestBatteryPercent(System.Collections.Generic.IEnumerable<DeviceBatteryInfo> devices)
    {
        return devices
            .Where(device => device.BatteryPercent.HasValue)
            .Select(device => (int?)device.BatteryPercent!.Value)
            .Min();
    }

    private static string BuildEmptyStateTitle(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (missingDeviceCount > 0)
        {
            return "设备已断开或暂不可读";
        }

        if (result.ConnectedLeDeviceCount == 0)
        {
            return "没有已连接蓝牙设备";
        }

        return "没有可读取电量的设备";
    }

    private static string BuildEmptyStateDescription(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (missingDeviceCount > 0)
        {
            return $"此前显示的 {missingDeviceCount} 台设备当前已断开，或暂时无法通过公开标准接口读取电量。";
        }

        if (result.ConnectedLeDeviceCount == 0)
        {
            return "当前没有已连接的 Bluetooth LE 设备。连接受支持设备后，面板会自动刷新。";
        }

        return "已连接设备中没有通过公开标准接口读取到电量。应用只显示 BAS 读取成功的设备。";
    }

    private void NativeMessageBox(string message, string caption)
    {
        IntPtr owner = _panelWindow?.WindowHandle ?? _hostWindow?.WindowHandle ?? IntPtr.Zero;
        Interop.NativeMethods.MessageBox(
            owner,
            message,
            caption,
            Interop.NativeMethods.MB_OK | Interop.NativeMethods.MB_ICONINFORMATION);
    }

    private void EnqueueOnUiThread(Action action)
    {
        DispatcherQueue dispatcherQueue = _hostWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        dispatcherQueue.TryEnqueue(() => action());
    }

    private void EnsureDiscoveryMonitoringStarted()
    {
        if (_bluetoothDeviceDiscoveryService.IsMonitoring)
        {
            return;
        }

        _bluetoothDeviceDiscoveryService.StartMonitoring();
    }

    private void OnAutoRefreshRequested(object? sender, BluetoothRefreshRequestedEventArgs args)
    {
        CancellationTokenSource debounceCts = new();
        CancellationTokenSource? previousCts = Interlocked.Exchange(ref _autoRefreshDebounceCts, debounceCts);
        previousCts?.Cancel();
        previousCts?.Dispose();

        _ = DebouncedAutoRefreshAsync(args.Reason, debounceCts.Token);
    }

    private async Task DebouncedAutoRefreshAsync(string reason, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        EnqueueOnUiThread(async () =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdateStatusMessage($"{reason} 正在自动刷新设备列表...");
            await RefreshDevicesAsync(forceRefresh: true);
        });
    }

    private int CountMissingDevices(IEnumerable<DeviceBatteryInfo> currentDevices)
    {
        if (_displayedDeviceIds.Count == 0)
        {
            return 0;
        }

        HashSet<string> currentDeviceIds = currentDevices
            .Select(device => device.DeviceId)
            .Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId))
            .ToHashSet(StringComparer.Ordinal);

        return _displayedDeviceIds.Count(deviceId => !currentDeviceIds.Contains(deviceId));
    }

    private void UpdateDisplayedDeviceIds(IEnumerable<DeviceBatteryInfo> devices)
    {
        _displayedDeviceIds.Clear();

        foreach (string deviceId in devices
                     .Select(device => device.DeviceId)
                     .Where(static deviceId => !string.IsNullOrWhiteSpace(deviceId))
                     .Distinct(StringComparer.Ordinal))
        {
            _displayedDeviceIds.Add(deviceId);
        }
    }
}
