using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlueBattery.Diagnostics;
using BlueBattery.Models;
using BlueBattery.Resources.Strings;
using BlueBattery.Services.Bluetooth;
using BlueBattery.Services.State;
using BlueBattery.Services.Settings;
using BlueBattery.Services.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Collections.Generic;

namespace BlueBattery;

public partial class App : Application
{
    private const string DefaultTooltip = nameof(DefaultTooltip);
    private readonly IBluetoothDeviceDiscoveryService _bluetoothDeviceDiscoveryService = new BluetoothDeviceDiscoveryService();
    private readonly IBatteryTelemetryService _bluetoothBatteryTelemetryService = new BluetoothBatteryTelemetryService();
    private readonly IAppStateStore _appStateStore = new JsonAppStateStore();
    private readonly IStartupLaunchService _startupLaunchService = new StartupLaunchService();
    private MainWindow? _hostWindow;
    private PanelWindow? _panelWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIconService;
    private bool _hasLoadedDeviceSnapshot;
    private bool _hasAttemptedStateRestore;
    private bool _isRefreshingDevices;
    private bool _pendingRefreshRequested;
    private CancellationTokenSource? _autoRefreshDebounceCts;
    private DateTimeOffset? _lastSuccessfulRefreshUtc;
    private AppStateSnapshot? _restoredSnapshot;
    private readonly Dictionary<string, DeviceBatteryInfo> _displayedDevices = new(StringComparer.Ordinal);

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
        _trayIconService.UpdateTooltip(AppStrings.AppTitle);

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
        EnqueueOnUiThread(async () =>
        {
            if (_hostWindow is null)
            {
                return;
            }

            EnsureSettingsWindow();
            if (_settingsWindow is null)
            {
                return;
            }

            await _settingsWindow.LoadAsync();
            _settingsWindow.ShowCentered(_hostWindow.WindowHandle);
            _panelWindow?.UpdateStatusMessage(AppStrings.SettingsOpenedStatus);
        });
    }

    private void OnAboutRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdateStatusMessage(AppStrings.AboutOpenedStatus);
            NativeMessageBox(AppStrings.AboutMessage, AppStrings.AboutCaption);
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
            _settingsWindow?.Close();
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

    private void EnsureSettingsWindow()
    {
        if (_settingsWindow is null || _settingsWindow.IsClosed)
        {
            _settingsWindow = new SettingsWindow(_startupLaunchService);
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
        _panelWindow?.UpdateEmptyState(AppStrings.RestoredSnapshotTitle, AppStrings.RestoredSnapshotDescription);
        _panelWindow?.UpdateLastRefresh(_lastSuccessfulRefreshUtc);
        _panelWindow?.UpdateStatusMessage(AppStrings.RestoredSnapshotStatus);
        _trayIconService?.UpdateTooltip(BuildTooltip(new BluetoothRefreshResult
        {
            Devices = staleDevices,
            ConnectedLeDeviceCount = staleDevices.Length,
        }, missingDeviceCount: 0));
        _trayIconService?.UpdateBatteryIcon(GetLowestBatteryPercent(staleDevices));
        UpdateDisplayedDevices(staleDevices);
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
        _panelWindow?.UpdateStatusMessage(AppStrings.RefreshingBatteryStatus);

        try
        {
            BluetoothRefreshResult result = await _bluetoothDeviceDiscoveryService.GetConnectedDevicesAsync();
            int missingDeviceCount = CountMissingDevices(result.Devices);
            IReadOnlyList<DeviceBatteryInfo> displayDevices = MergeWithMissingDevices(result.Devices);
            _panelWindow?.SetDevices(displayDevices);
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
            UpdateDisplayedDevices(displayDevices);
            EnsureDiscoveryMonitoringStarted();
        }
        catch (Exception ex)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            if (_panelWindow?.HasDevices == true)
            {
                _panelWindow.MarkDevicesAsStale(DeviceSnapshotState.RefreshFailedCache);
                _panelWindow.UpdateStatusMessage(AppStrings.BuildStatusRefreshFailedCache(timestamp, ex.Message));
            }
            else
            {
                _panelWindow?.SetDevices(Array.Empty<DeviceBatteryInfo>());
                _panelWindow?.UpdateEmptyState(
                    AppStrings.RefreshFailedTitle,
                    AppStrings.RefreshFailedDescription);
                _panelWindow?.UpdateStatusMessage(AppStrings.BuildStatusRefreshFailed(timestamp, ex.Message));
                _trayIconService?.UpdateBatteryIcon(null);
                _displayedDevices.Clear();
            }

            _panelWindow?.UpdateLastRefresh(_lastSuccessfulRefreshUtc);
            _trayIconService?.UpdateTooltip(AppStrings.TooltipRefreshFailed);
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
            return AppStrings.BuildStatusRefreshSuccessWithMissing(result.Devices.Count, missingDeviceCount, timestamp);
        }

            return AppStrings.BuildStatusRefreshSuccess(result.Devices.Count, result.ConnectedLeDeviceCount, timestamp);
        }

        if (missingDeviceCount > 0)
        {
            return AppStrings.BuildStatusOnlyMissing(missingDeviceCount, timestamp);
        }

        if (result.ConnectedLeDeviceCount > 0)
        {
            return AppStrings.BuildStatusNoReadable(result.ConnectedLeDeviceCount, timestamp);
        }

        return AppStrings.BuildStatusNoConnected(timestamp);
    }

    private static string BuildTooltip(BluetoothRefreshResult result, int missingDeviceCount)
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
            return AppStrings.EmptyStateMissingTitle;
        }

        if (result.ConnectedLeDeviceCount == 0)
        {
            return AppStrings.EmptyStateNoConnectedTitle;
        }

        return AppStrings.EmptyStateNoReadableTitle;
    }

    private static string BuildEmptyStateDescription(BluetoothRefreshResult result, int missingDeviceCount)
    {
        if (missingDeviceCount > 0)
        {
            return AppStrings.BuildEmptyStateMissingDescription(missingDeviceCount);
        }

        if (result.ConnectedLeDeviceCount == 0)
        {
            return AppStrings.EmptyStateNoConnectedDescription;
        }

        return AppStrings.EmptyStateNoReadableDescription;
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
            _panelWindow?.UpdateStatusMessage(AppStrings.BuildStatusAutoRefresh(reason));
            await RefreshDevicesAsync(forceRefresh: true);
        });
    }

    private int CountMissingDevices(IEnumerable<DeviceBatteryInfo> currentDevices)
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

    private IReadOnlyList<DeviceBatteryInfo> MergeWithMissingDevices(IReadOnlyList<DeviceBatteryInfo> currentDevices)
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

    private void UpdateDisplayedDevices(IEnumerable<DeviceBatteryInfo> devices)
    {
        _displayedDevices.Clear();

        foreach (DeviceBatteryInfo device in devices.Where(static device => !string.IsNullOrWhiteSpace(device.DeviceId)))
        {
            _displayedDevices[device.DeviceId] = device;
        }
    }
}
