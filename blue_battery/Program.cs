using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BlueBattery.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace BlueBattery;

public static class Program
{
    private const string MainInstanceKey = "blue_battery.main";
    private const uint CwmoDefault = 0;
    private const uint Infinite = 0xFFFFFFFF;

    public static event EventHandler<AppActivationArguments>? RedirectedActivationRequested;

    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        SingleInstanceDiagnostics.Log("Program.Main.Enter");

        AppInstance keyInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        AppActivationArguments activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        SingleInstanceDiagnostics.Log("Program.Main.AfterFindOrRegister", activationArgs, keyInstance);

        if (!keyInstance.IsCurrent)
        {
            SingleInstanceDiagnostics.Log("Program.Main.RedirectingToExistingInstance", activationArgs, keyInstance);
            RedirectActivationTo(keyInstance, activationArgs);
            SingleInstanceDiagnostics.Log("Program.Main.RedirectCompleted", activationArgs, keyInstance);
            return 0;
        }

        keyInstance.Activated += OnActivated;
        SingleInstanceDiagnostics.Log("Program.Main.StartingApplication", activationArgs, keyInstance);

        Application.Start((p) =>
        {
            DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            App app = new();
        });

        return 0;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        SingleInstanceDiagnostics.Log("Program.KeyInstance.Activated", args);
        RedirectedActivationRequested?.Invoke(sender, args);
    }

    private static void RedirectActivationTo(AppInstance keyInstance, AppActivationArguments activationArgs)
    {
        using EventWaitHandle redirectEventHandle = new(false, EventResetMode.AutoReset);

        Task.Run(async () =>
        {
            await keyInstance.RedirectActivationToAsync(activationArgs);
            redirectEventHandle.Set();
        });

        CoWaitForMultipleObjects(
            CwmoDefault,
            Infinite,
            1,
            [redirectEventHandle.SafeWaitHandle.DangerousGetHandle()],
            out _);

        Process process = Process.GetProcessById((int)keyInstance.ProcessId);
        SetForegroundWindow(process.MainWindowHandle);
        SingleInstanceDiagnostics.Log("Program.RedirectActivationTo.SetForegroundWindow", activationArgs, keyInstance);
    }

    [DllImport("Ole32.dll")]
    private static extern uint CoWaitForMultipleObjects(
        uint dwFlags,
        uint dwMilliseconds,
        ulong nHandles,
        nint[] pHandles,
        out uint dwIndex);

    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
