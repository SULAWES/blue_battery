using System;
using BlueBattery.Interop;
using BlueBattery.Services.Settings;
using BlueBattery.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BlueBattery;

public sealed partial class SettingsWindow : Window
{
    private readonly IntPtr _hWnd;
    private readonly AppWindow _appWindow;
    private readonly IStartupLaunchService _startupLaunchService;
    private bool _isSynchronizingState;

    public SettingsWindow(IStartupLaunchService startupLaunchService)
    {
        _startupLaunchService = startupLaunchService;
        ViewModel = new SettingsViewModel();
        InitializeComponent();
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        ConfigureWindow();
    }

    public SettingsViewModel ViewModel { get; }

    public bool IsClosed { get; private set; }

    public IntPtr WindowHandle => _hWnd;

    public async System.Threading.Tasks.Task LoadAsync()
    {
        await RefreshStartupStateAsync();
    }

    public void ShowCentered(IntPtr ownerWindow)
    {
        SizeInt32 size = new(420, 240);
        WindowId ownerWindowId = Win32Interop.GetWindowIdFromWindow(ownerWindow);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(ownerWindowId, DisplayAreaFallback.Nearest);
        RectInt32 workArea = displayArea.WorkArea;
        int x = workArea.X + ((workArea.Width - size.Width) / 2);
        int y = workArea.Y + ((workArea.Height - size.Height) / 2);

        _appWindow.Resize(size);
        _appWindow.Move(new PointInt32(x, y));
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_SHOW);
        Activate();
    }

    private void ConfigureWindow()
    {
        _appWindow.Title = ViewModel.Title;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        Closed += (_, _) => IsClosed = true;
    }

    private async void StartupToggleSwitch_OnToggled(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizingState)
        {
            return;
        }

        await UpdateStartupStateAsync(StartupToggleSwitch.IsOn);
    }

    private async void OpenSystemStartupSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        await _startupLaunchService.OpenSystemStartupSettingsAsync();
    }

    private async System.Threading.Tasks.Task UpdateStartupStateAsync(bool isEnabled)
    {
        StartupTaskStateSnapshot snapshot = await _startupLaunchService.SetEnabledAsync(isEnabled);
        ApplySnapshot(snapshot);
    }

    private async System.Threading.Tasks.Task RefreshStartupStateAsync()
    {
        StartupTaskStateSnapshot snapshot = await _startupLaunchService.GetStateAsync();
        ApplySnapshot(snapshot);
    }

    private void ApplySnapshot(StartupTaskStateSnapshot snapshot)
    {
        _isSynchronizingState = true;
        ViewModel.StartupEnabled = snapshot.IsEnabled;
        ViewModel.CanToggleStartup = snapshot.CanToggle;
        ViewModel.ShowSystemSettingsAction = snapshot.ShowSystemSettingsAction;
        ViewModel.StartupStatusText = snapshot.StatusText;
        StartupToggleSwitch.IsOn = snapshot.IsEnabled;
        _isSynchronizingState = false;
    }
}
