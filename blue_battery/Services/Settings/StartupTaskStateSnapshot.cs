namespace BlueBattery.Services.Settings;

public sealed class StartupTaskStateSnapshot
{
    public bool IsEnabled { get; init; }

    public bool CanToggle { get; init; }

    public bool ShowSystemSettingsAction { get; init; }

    public required string StatusText { get; init; }
}
