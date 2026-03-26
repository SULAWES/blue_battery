using System;
using BlueBattery.Interop;
using BlueBattery.Models;
using BlueBattery.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BlueBattery;

public sealed partial class PanelWindow : Window
{
    private const int PanelWidth = 420;
    private const int PanelHeight = 520;
    private const int PanelMargin = 12;
    private const int EdgeTolerance = 32;
    private readonly IntPtr _hWnd;
    private readonly AppWindow _appWindow;
    private bool _forceClosing;

    public PanelWindow()
    {
        ViewModel = new PanelViewModel();
        InitializeComponent();
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        ConfigureWindowChrome();

        Activated += OnActivated;
        Closed += (_, _) =>
        {
            IsVisible = false;
            IsClosed = true;
        };
    }

    public bool IsVisible { get; private set; }

    public bool IsClosed { get; private set; }

    public PanelViewModel ViewModel { get; }

    public IntPtr WindowHandle => _hWnd;

    public void ShowNearCursor()
    {
        NativeMethods.GetCursorPos(out NativeMethods.POINT cursor);
        DisplayArea displayArea = DisplayArea.GetFromPoint(
            new PointInt32(cursor.X, cursor.Y),
            DisplayAreaFallback.Nearest);

        ShowAtPosition(displayArea, cursor.X - PanelWidth + 16, cursor.Y - PanelHeight - PanelMargin, preferBelow: true);
    }

    internal void ShowNearTrayIcon(NativeMethods.RECT trayIconRect)
    {
        int anchorX = trayIconRect.Left + ((trayIconRect.Right - trayIconRect.Left) / 2);
        int anchorY = trayIconRect.Top + ((trayIconRect.Bottom - trayIconRect.Top) / 2);
        DisplayArea displayArea = DisplayArea.GetFromPoint(
            new PointInt32(anchorX, anchorY),
            DisplayAreaFallback.Nearest);

        RectInt32 workArea = displayArea.WorkArea;
        int x;
        int y;

        if (trayIconRect.Top >= workArea.Y + workArea.Height - EdgeTolerance)
        {
            x = trayIconRect.Right - PanelWidth;
            y = trayIconRect.Top - PanelHeight - PanelMargin;
            ShowAtPosition(displayArea, x, y, preferBelow: false);
            return;
        }

        if (trayIconRect.Bottom <= workArea.Y + EdgeTolerance)
        {
            x = trayIconRect.Right - PanelWidth;
            y = trayIconRect.Bottom + PanelMargin;
            ShowAtPosition(displayArea, x, y, preferBelow: true);
            return;
        }

        if (trayIconRect.Left >= workArea.X + workArea.Width - EdgeTolerance)
        {
            x = trayIconRect.Left - PanelWidth - PanelMargin;
            y = trayIconRect.Top;
            ShowAtPosition(displayArea, x, y, preferBelow: false);
            return;
        }

        x = trayIconRect.Right + PanelMargin;
        y = trayIconRect.Top;
        ShowAtPosition(displayArea, x, y, preferBelow: false);
    }

    public void UpdateStatusMessage(string status)
    {
        ViewModel.UpdateStatusMessage(status);
    }

    public void SetDevices(System.Collections.Generic.IEnumerable<DeviceBatteryInfo> devices)
    {
        ViewModel.SetDevices(devices);
    }

    public void MarkDevicesAsStale(DeviceSnapshotState snapshotState = DeviceSnapshotState.RefreshFailedCache)
    {
        ViewModel.MarkDevicesAsStale(snapshotState);
    }

    public void UpdateEmptyState(string title, string description)
    {
        ViewModel.UpdateEmptyState(title, description);
    }

    public void UpdateLastRefresh(DateTimeOffset? lastRefreshUtc)
    {
        ViewModel.UpdateLastRefresh(lastRefreshUtc);
    }

    public bool HasDevices => ViewModel.HasDevices;

    private void ConfigureWindowChrome()
    {
        _appWindow.IsShownInSwitchers = false;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }
    }

    private void ShowAtPosition(DisplayArea displayArea, int proposedX, int proposedY, bool preferBelow)
    {
        RectInt32 workArea = displayArea.WorkArea;
        int width = PanelWidth;
        int height = PanelHeight;
        int rightBoundary = workArea.X + workArea.Width - width - PanelMargin;
        int bottomBoundary = workArea.Y + workArea.Height - height - PanelMargin;

        int x = Math.Clamp(proposedX, workArea.X + PanelMargin, rightBoundary);
        int y = proposedY;
        if (preferBelow && y < workArea.Y + PanelMargin)
        {
            y = workArea.Y + PanelMargin;
        }

        y = Math.Clamp(y, workArea.Y + PanelMargin, bottomBoundary);

        _appWindow.Resize(new SizeInt32(width, height));
        _appWindow.Move(new PointInt32(x, y));

        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_SHOW);
        Activate();
        IsVisible = true;
    }

    public void HidePanel()
    {
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_HIDE);
        IsVisible = false;
    }

    public void ForceClose()
    {
        _forceClosing = true;
        Close();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_forceClosing)
        {
            return;
        }

        if (args.WindowActivationState == WindowActivationState.Deactivated && IsVisible)
        {
            HidePanel();
        }
    }
}
