using System;
using BlueBattery.Diagnostics;
using BlueBattery.Services.Tray;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace BlueBattery;

public partial class App : Application
{
    private const string DefaultTooltip = "blue_battery";
    private MainWindow? _hostWindow;
    private PanelWindow? _panelWindow;
    private TrayIconService? _trayIconService;

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
    }

    private void OnOpenRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
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
        });
    }

    private void OnRefreshRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _trayIconService?.UpdateTooltip($"blue_battery · 最近刷新 {timestamp}");
            EnsurePanelWindow();
            _panelWindow?.UpdatePlaceholderStatus($"已执行手动刷新，占位数据更新时间 {timestamp}");
        });
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdatePlaceholderStatus("设置入口已预留，首版稍后接入。");
            NativeMessageBox("设置入口已预留，当前版本尚未接入实际设置页。", "blue_battery");
        });
    }

    private void OnAboutRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            EnsurePanelWindow();
            _panelWindow?.UpdatePlaceholderStatus("已打开关于信息。");
            NativeMessageBox("blue_battery\r\nWinUI 3 托盘电量应用原型。\r\n当前阶段：托盘壳层与轻量面板。", "关于 blue_battery");
        });
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        EnqueueOnUiThread(() =>
        {
            SingleInstanceDiagnostics.Log("App.OnExitRequested");
            Program.RedirectedActivationRequested -= OnRedirectedActivationRequested;
            _panelWindow?.ForceClose();
            _trayIconService?.Dispose();
            _hostWindow?.Close();
            Exit();
        });
    }

    private void OnRedirectedActivationRequested(object? sender, AppActivationArguments args)
    {
        SingleInstanceDiagnostics.Log("App.OnRedirectedActivationRequested", args);
        EnqueueOnUiThread(() =>
        {
            if (_hostWindow is null)
            {
                return;
            }

            _hostWindow.Activate();
            OpenPanel();
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
}
