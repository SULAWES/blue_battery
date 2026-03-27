using System.Threading.Tasks;

namespace BlueBattery.Services.Settings;

public interface IStartupLaunchService
{
    Task<StartupTaskStateSnapshot> GetStateAsync();

    Task<StartupTaskStateSnapshot> SetEnabledAsync(bool isEnabled);

    Task OpenSystemStartupSettingsAsync();
}
