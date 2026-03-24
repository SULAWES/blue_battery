using System;
using System.Runtime.InteropServices;

namespace BlueBattery.Interop;

internal static class NativeMethods
{
    internal const int S_OK = 0;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOW = 5;
    internal const int WM_APP = 0x8000;
    internal const int WM_COMMAND = 0x0111;
    internal const int WM_LBUTTONUP = 0x0202;
    internal const int WM_RBUTTONUP = 0x0205;
    internal const int WM_CONTEXTMENU = 0x007B;
    internal const int GWL_WNDPROC = -4;
    internal const int MF_STRING = 0x0000;
    internal const int MF_SEPARATOR = 0x0800;
    internal const int TPM_LEFTALIGN = 0x0000;
    internal const int TPM_RIGHTBUTTON = 0x0002;
    internal const int NIM_ADD = 0x00000000;
    internal const int NIM_MODIFY = 0x00000001;
    internal const int NIM_DELETE = 0x00000002;
    internal const int NIF_MESSAGE = 0x00000001;
    internal const int NIF_ICON = 0x00000002;
    internal const int NIF_TIP = 0x00000004;
    internal const int IDI_APPLICATION = 32512;
    internal const uint MB_OK = 0x00000000;
    internal const uint MB_ICONINFORMATION = 0x00000040;

    internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NOTIFYICONDATA
    {
        internal uint cbSize;
        internal IntPtr hWnd;
        internal uint uID;
        internal uint uFlags;
        internal uint uCallbackMessage;
        internal IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string szTip;

        internal uint dwState;
        internal uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string szInfo;

        internal uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string szInfoTitle;

        internal uint dwInfoFlags;
        internal Guid guidItem;
        internal IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NOTIFYICONIDENTIFIER
    {
        internal uint cbSize;
        internal IntPtr hWnd;
        internal uint uID;
        internal Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll")]
    internal static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    internal static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    internal static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    internal static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    [DllImport("user32.dll")]
    internal static extern uint TrackPopupMenu(
        IntPtr hMenu,
        uint uFlags,
        int x,
        int y,
        int nReserved,
        IntPtr hWnd,
        IntPtr prcRect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        internal int X;
        internal int Y;
    }
}
