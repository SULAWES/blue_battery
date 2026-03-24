using System;
using BlueBattery.Interop;
using Microsoft.UI.Xaml;

namespace BlueBattery;

public sealed partial class MainWindow : Window
{
    private readonly IntPtr _hWnd;

    public MainWindow()
    {
        InitializeComponent();
        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
    }

    public IntPtr WindowHandle => _hWnd;

    public void HideHostWindow()
    {
        NativeMethods.ShowWindow(_hWnd, NativeMethods.SW_HIDE);
    }
}
