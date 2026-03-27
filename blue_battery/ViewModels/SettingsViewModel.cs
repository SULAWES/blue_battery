using System.ComponentModel;
using BlueBattery.Resources.Strings;
using Microsoft.UI.Xaml;

namespace BlueBattery.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private bool _startupEnabled;
    private bool _canToggleStartup;
    private bool _showSystemSettingsAction;
    private string _startupStatusText = AppStrings.StartupLoadingStatus;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => AppStrings.SettingsTitle;

    public string StartupSectionTitle => AppStrings.StartupSectionTitle;

    public string StartupToggleHeader => AppStrings.StartupToggleHeader;

    public string StartupToggleDescription => AppStrings.StartupToggleDescription;

    public string OpenSystemStartupSettingsText => AppStrings.OpenSystemStartupSettingsText;

    public bool StartupEnabled
    {
        get => _startupEnabled;
        set
        {
            if (_startupEnabled == value)
            {
                return;
            }

            _startupEnabled = value;
            OnPropertyChanged(nameof(StartupEnabled));
        }
    }

    public bool CanToggleStartup
    {
        get => _canToggleStartup;
        set
        {
            if (_canToggleStartup == value)
            {
                return;
            }

            _canToggleStartup = value;
            OnPropertyChanged(nameof(CanToggleStartup));
        }
    }

    public string StartupStatusText
    {
        get => _startupStatusText;
        set
        {
            if (_startupStatusText == value)
            {
                return;
            }

            _startupStatusText = value;
            OnPropertyChanged(nameof(StartupStatusText));
        }
    }

    public bool ShowSystemSettingsAction
    {
        get => _showSystemSettingsAction;
        set
        {
            if (_showSystemSettingsAction == value)
            {
                return;
            }

            _showSystemSettingsAction = value;
            OnPropertyChanged(nameof(ShowSystemSettingsAction));
            OnPropertyChanged(nameof(SystemSettingsActionVisibility));
        }
    }

    public Visibility SystemSettingsActionVisibility => ShowSystemSettingsAction
        ? Visibility.Visible
        : Visibility.Collapsed;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
