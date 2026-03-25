using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBattery.Diagnostics;
using BlueBattery.Services.Bluetooth;
using BlueBattery.Services.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace BlueBattery;

public partial class App : Application
{
    private const string DefaultTooltip = "blue_battery";
    private readonly BluetoothBatteryDeviceService _bluetoothBatteryDeviceService = new();
    private readonly BluetoothBatteryTelemetryService _bluetoothBatteryTelemetryService = new();
    private readonly BluetoothDeviceWatcherService _bluetoothDeviceWatcherService = new();
    private MainWindow? _hostWindow;
    private PanelWindow? _panelWindow;
    private TrayIconService? _trayIconService;
    private bool _hasLoadedDeviceSnapshot;
    private bool _isRefreshingDevices;
    private bool _pendingRefreshRequested;
    private bool _watcherStarted;
    private CancellationTokenSource? _autoRefreshDebounceCts;

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

        _bluetoothDeviceWatcherService.RefreshRequested += OnAutoRefreshRequested;
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
            _bluetoothDeviceWatcherService.RefreshRequested -= OnAutoRefreshRequested;
            _bluetoothDeviceWatcherService.Dispose();
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
            BluetoothRefreshResult result = await _bluetoothBatteryDeviceService.GetConnectedDevicesAsync();
            _panelWindow?.SetDevices(result.Devices);
            _panelWindow?.UpdateStatusMessage(BuildStatusMessage(result));
            _trayIconService?.UpdateTooltip(BuildTooltip(result));
            await _bluetoothBatteryTelemetryService.UpdateTrackedDevicesAsync(result.Devices.Select(device => device.DeviceId));
            _hasLoadedDeviceSnapshot = true;
            EnsureWatcherStarted();
        }
        catch (Exception ex)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _panelWindow?.SetDevices(Array.Empty<BlueBattery.Models.DeviceBatteryInfo>());
            _panelWindow?.UpdateStatusMessage($"刷新失败。{timestamp}。{ex.Message}");
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

    private static string BuildStatusMessage(BluetoothRefreshResult result)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");

        if (result.Devices.Count > 0)
        {
            return $"已刷新 {result.Devices.Count} 台设备电量。已连接 LE 设备 {result.ConnectedLeDeviceCount} 台。时间 {timestamp}。";
        }

        if (result.ConnectedLeDeviceCount > 0)
        {
            return $"已连接 LE 设备 {result.ConnectedLeDeviceCount} 台，但没有读取到可显示的公开电量数据。时间 {timestamp}。";
        }

        return $"当前没有已连接的 LE 设备。时间 {timestamp}。";
    }

    private static string BuildTooltip(BluetoothRefreshResult result)
    {
        if (result.Devices.Count == 0)
        {
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

    private void EnsureWatcherStarted()
    {
        if (_watcherStarted)
        {
            return;
        }

        _bluetoothDeviceWatcherService.Start();
        _watcherStarted = true;
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
}
