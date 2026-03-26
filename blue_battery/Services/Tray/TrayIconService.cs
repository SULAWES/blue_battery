using System;
using System.Runtime.InteropServices;
using BlueBattery.Interop;

namespace BlueBattery.Services.Tray;

internal sealed class TrayIconService : IDisposable
{
    private const uint IconId = 1;
    private const int RefreshCommandId = 1001;
    private const int SettingsCommandId = 1002;
    private const int AboutCommandId = 1003;
    private const int ExitCommandId = 1004;
    private readonly IntPtr _windowHandle;
    private readonly NativeMethods.WndProc _wndProc;
    private readonly uint _taskbarCreatedMessage;
    private NativeMethods.NOTIFYICONDATA _notifyIconData;
    private IntPtr _previousWndProc;
    private IntPtr _iconHandle;
    private bool _disposed;

    public TrayIconService(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _wndProc = WindowProc;
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? AboutRequested;

    public event EventHandler? ExitRequested;

    public void Show()
    {
        _iconHandle = BatteryTrayIconRenderer.Create(batteryPercent: null);
        _notifyIconData = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = IconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_APP + 1,
            hIcon = _iconHandle,
            szTip = "blue_battery"
        };

        IntPtr newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc);
        _previousWndProc = NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GWL_WNDPROC, newWndProc);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _notifyIconData);
    }

    public void UpdateTooltip(string text)
    {
        _notifyIconData.szTip = text.Length <= 127 ? text : text[..127];
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _notifyIconData);
    }

    public void UpdateBatteryIcon(int? lowestBatteryPercent)
    {
        IntPtr newIconHandle = BatteryTrayIconRenderer.Create(lowestBatteryPercent);
        ReplaceIcon(newIconHandle);
    }

    public bool TryGetIconRect(out NativeMethods.RECT rect)
    {
        NativeMethods.NOTIFYICONIDENTIFIER identifier = new()
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONIDENTIFIER>(),
            hWnd = _windowHandle,
            uID = IconId
        };

        int result = NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect);
        return result == NativeMethods.S_OK;
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarCreatedMessage)
        {
            RestoreIconAfterExplorerRestart();
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_APP + 1)
        {
            int mouseMessage = lParam.ToInt32();

            if (mouseMessage == NativeMethods.WM_LBUTTONUP)
            {
                OpenRequested?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (mouseMessage == NativeMethods.WM_RBUTTONUP || mouseMessage == NativeMethods.WM_CONTEXTMENU)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        if (msg == NativeMethods.WM_COMMAND)
        {
            int commandId = wParam.ToInt32() & 0xFFFF;
            if (commandId == RefreshCommandId)
            {
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (commandId == SettingsCommandId)
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (commandId == AboutCommandId)
            {
                AboutRequested?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }

            if (commandId == ExitCommandId)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return IntPtr.Zero;
            }
        }

        return NativeMethods.CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr menuHandle = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MF_STRING, RefreshCommandId, "立即刷新");
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MF_STRING, SettingsCommandId, "设置");
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MF_STRING, AboutCommandId, "关于");
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MF_SEPARATOR, 0, null);
            NativeMethods.AppendMenu(menuHandle, NativeMethods.MF_STRING, ExitCommandId, "退出");

            NativeMethods.GetCursorPos(out NativeMethods.POINT point);
            NativeMethods.SetForegroundWindow(_windowHandle);
            NativeMethods.TrackPopupMenu(
                menuHandle,
                NativeMethods.TPM_LEFTALIGN | NativeMethods.TPM_RIGHTBUTTON,
                point.X,
                point.Y,
                0,
                _windowHandle,
                IntPtr.Zero);
        }
        finally
        {
            NativeMethods.DestroyMenu(menuHandle);
        }
    }

    private void ReplaceIcon(IntPtr newIconHandle)
    {
        IntPtr previousIconHandle = _iconHandle;
        _iconHandle = newIconHandle;
        _notifyIconData.hIcon = newIconHandle;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _notifyIconData);

        if (previousIconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(previousIconHandle);
        }
    }

    private void RestoreIconAfterExplorerRestart()
    {
        if (_iconHandle == IntPtr.Zero)
        {
            return;
        }

        _notifyIconData.cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>();
        _notifyIconData.hWnd = _windowHandle;
        _notifyIconData.uID = IconId;
        _notifyIconData.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        _notifyIconData.uCallbackMessage = NativeMethods.WM_APP + 1;
        _notifyIconData.hIcon = _iconHandle;

        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _notifyIconData);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _notifyIconData);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _notifyIconData);

        if (_previousWndProc != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GWL_WNDPROC, _previousWndProc);
        }

        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
        }
    }
}
