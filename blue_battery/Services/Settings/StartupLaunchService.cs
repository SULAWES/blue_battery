using System;
using System.Threading.Tasks;
using BlueBattery.Resources.Strings;
using Windows.ApplicationModel;
using Windows.System;

namespace BlueBattery.Services.Settings;

public sealed class StartupLaunchService : IStartupLaunchService
{
    private const string StartupTaskId = "BlueBatteryStartup";

    public async Task<StartupTaskStateSnapshot> GetStateAsync()
    {
        StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
        return BuildSnapshot(startupTask.State);
    }

    public async Task<StartupTaskStateSnapshot> SetEnabledAsync(bool isEnabled)
    {
        StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);

        if (isEnabled)
        {
            StartupTaskState state = startupTask.State;

            if (state is StartupTaskState.Disabled or StartupTaskState.DisabledByUser)
            {
                state = await startupTask.RequestEnableAsync();
            }

            return BuildSnapshot(state);
        }

        startupTask.Disable();
        return BuildSnapshot(startupTask.State);
    }

    public Task OpenSystemStartupSettingsAsync()
    {
        return Launcher.LaunchUriAsync(new Uri("ms-settings:startupapps")).AsTask();
    }

    private static StartupTaskStateSnapshot BuildSnapshot(StartupTaskState state)
    {
        return state switch
        {
            StartupTaskState.Enabled => new StartupTaskStateSnapshot
            {
                IsEnabled = true,
                CanToggle = true,
                ShowSystemSettingsAction = false,
                StatusText = AppStrings.StartupEnabledStatus,
            },
            StartupTaskState.Disabled => new StartupTaskStateSnapshot
            {
                IsEnabled = false,
                CanToggle = true,
                ShowSystemSettingsAction = false,
                StatusText = AppStrings.StartupDisabledStatus,
            },
            StartupTaskState.DisabledByUser => new StartupTaskStateSnapshot
            {
                IsEnabled = false,
                CanToggle = false,
                ShowSystemSettingsAction = true,
                StatusText = AppStrings.StartupDisabledByUserStatus,
            },
            StartupTaskState.DisabledByPolicy => new StartupTaskStateSnapshot
            {
                IsEnabled = false,
                CanToggle = false,
                ShowSystemSettingsAction = false,
                StatusText = AppStrings.StartupDisabledByPolicyStatus,
            },
            _ => new StartupTaskStateSnapshot
            {
                IsEnabled = false,
                CanToggle = false,
                ShowSystemSettingsAction = false,
                StatusText = AppStrings.StartupUnknownStatus,
            },
        };
    }
}
