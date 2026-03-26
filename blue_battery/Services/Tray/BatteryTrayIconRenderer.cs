using System;
using System.Runtime.InteropServices;
using BlueBattery.Interop;

namespace BlueBattery.Services.Tray;

internal static class BatteryTrayIconRenderer
{
    private const int IconSize = 32;
    private const int Transparent = 0x00000000;
    private const int Outline = unchecked((int)0xFF58636E);
    private const int NormalFill = unchecked((int)0xFF6A7A88);
    private const int WarningFill = unchecked((int)0xFFC66B1A);
    private const int UnknownFill = unchecked((int)0xFF8D98A3);

    public static IntPtr Create(int? batteryPercent)
    {
        int[] pixels = new int[IconSize * IconSize];
        DrawBatteryShell(pixels);

        if (batteryPercent.HasValue)
        {
            DrawBatteryLevel(pixels, batteryPercent.Value);
        }
        else
        {
            DrawUnknownState(pixels);
        }

        return CreateIconFromPixels(pixels);
    }

    private static void DrawBatteryShell(int[] pixels)
    {
        DrawRectOutline(pixels, 5, 8, 20, 14, Outline);
        FillRect(pixels, 25, 12, 3, 6, Outline);
    }

    private static void DrawBatteryLevel(int[] pixels, int batteryPercent)
    {
        int bucket = GetBucketedBatteryPercent(batteryPercent);
        int innerWidth = 16;
        int fillWidth = bucket <= 5 ? 2 : Math.Max(2, (int)Math.Round(innerWidth * (bucket / 100d)));
        int fillColor = bucket < 20 ? WarningFill : NormalFill;
        FillRect(pixels, 7, 10, fillWidth, 10, fillColor);
    }

    private static void DrawUnknownState(int[] pixels)
    {
        FillRect(pixels, 9, 14, 10, 2, UnknownFill);
    }

    private static int GetBucketedBatteryPercent(int batteryPercent)
    {
        int clamped = Math.Clamp(batteryPercent, 0, 100);
        if (clamped >= 95)
        {
            return 100;
        }

        if (clamped <= 5)
        {
            return 5;
        }

        return Math.Max(10, (clamped / 10) * 10);
    }

    private static void DrawRectOutline(int[] pixels, int x, int y, int width, int height, int color)
    {
        FillRect(pixels, x, y, width, 1, color);
        FillRect(pixels, x, y + height - 1, width, 1, color);
        FillRect(pixels, x, y, 1, height, color);
        FillRect(pixels, x + width - 1, y, 1, height, color);
    }

    private static void FillRect(int[] pixels, int x, int y, int width, int height, int color)
    {
        int startX = Math.Clamp(x, 0, IconSize);
        int startY = Math.Clamp(y, 0, IconSize);
        int endX = Math.Clamp(x + width, 0, IconSize);
        int endY = Math.Clamp(y + height, 0, IconSize);

        for (int row = startY; row < endY; row++)
        {
            int rowStart = row * IconSize;
            for (int column = startX; column < endX; column++)
            {
                pixels[rowStart + column] = color;
            }
        }
    }

    private static IntPtr CreateIconFromPixels(int[] pixels)
    {
        NativeMethods.BITMAPINFO bitmapInfo = new()
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = IconSize,
                biHeight = -IconSize,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
            }
        };

        IntPtr colorBitmap = NativeMethods.CreateDIBSection(
            IntPtr.Zero,
            ref bitmapInfo,
            NativeMethods.DIB_RGB_COLORS,
            out IntPtr bitmapBits,
            IntPtr.Zero,
            0);

        if (colorBitmap == IntPtr.Zero || bitmapBits == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法创建托盘图标位图。");
        }

        IntPtr maskBitmap = NativeMethods.CreateBitmap(IconSize, IconSize, 1, 1, IntPtr.Zero);
        if (maskBitmap == IntPtr.Zero)
        {
            NativeMethods.DeleteObject(colorBitmap);
            throw new InvalidOperationException("无法创建托盘图标遮罩位图。");
        }

        try
        {
            Marshal.Copy(pixels, 0, bitmapBits, pixels.Length);
            NativeMethods.ICONINFO iconInfo = new()
            {
                fIcon = true,
                hbmColor = colorBitmap,
                hbmMask = maskBitmap,
            };

            IntPtr iconHandle = NativeMethods.CreateIconIndirect(ref iconInfo);
            if (iconHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("无法生成托盘图标句柄。");
            }

            return iconHandle;
        }
        finally
        {
            NativeMethods.DeleteObject(colorBitmap);
            NativeMethods.DeleteObject(maskBitmap);
        }
    }
}
