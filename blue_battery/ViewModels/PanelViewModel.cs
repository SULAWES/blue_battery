using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using BlueBattery.Models;
using BlueBattery.Resources.Strings;
using Microsoft.UI.Xaml;

namespace BlueBattery.ViewModels;

public sealed class PanelViewModel : INotifyPropertyChanged
{
    private string _statusMessage = AppStrings.InitialStatusMessage;
    private string _emptyStateTitle = AppStrings.InitialEmptyStateTitle;
    private string _emptyStateDescription = AppStrings.InitialEmptyStateDescription;
    private string _lastRefreshText = AppStrings.NoSuccessfulRefreshYet;

    public PanelViewModel()
    {
        Devices.CollectionChanged += OnDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceBatteryInfo> Devices { get; } = [];

    public string AppTitle => AppStrings.AppTitle;

    public string Subtitle => AppStrings.PanelSubtitle;

    public string ScopeHint => AppStrings.ScopeHint;

    public string InfoBarMessage => AppStrings.InfoBarMessage;

    public string DeviceCountLabel => AppStrings.DeviceCountLabel;

    public string LowestBatteryLabel => AppStrings.LowestBatteryLabel;

    public string CurrentStatusLabel => AppStrings.CurrentStatusLabel;

    public string EmptyStateTitle
    {
        get => _emptyStateTitle;
        private set
        {
            if (_emptyStateTitle == value)
            {
                return;
            }

            _emptyStateTitle = value;
            OnPropertyChanged(nameof(EmptyStateTitle));
        }
    }

    public string EmptyStateDescription
    {
        get => _emptyStateDescription;
        private set
        {
            if (_emptyStateDescription == value)
            {
                return;
            }

            _emptyStateDescription = value;
            OnPropertyChanged(nameof(EmptyStateDescription));
        }
    }

    public string DeviceCountText => Devices.Count.ToString();

    public string LowestBatteryText
    {
        get
        {
            int? lowestBattery = Devices
                .Where(device => device.BatteryPercent.HasValue)
                .Select(device => (int?)device.BatteryPercent!.Value)
                .Min();

            return lowestBattery.HasValue
                ? $"{lowestBattery.Value}%"
                : "--";
        }
    }

    public string DeviceSectionTitle => Devices.Count > 0
        ? $"设备列表 ({Devices.Count})"
        : AppStrings.DeviceSectionTitle;

    public string LastRefreshText
    {
        get => _lastRefreshText;
        private set
        {
            if (_lastRefreshText == value)
            {
                return;
            }

            _lastRefreshText = value;
            OnPropertyChanged(nameof(LastRefreshText));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public Visibility DeviceListVisibility => Devices.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => Devices.Count == 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public void UpdateStatusMessage(string statusMessage)
    {
        StatusMessage = statusMessage;
    }

    public void SetDevices(IEnumerable<DeviceBatteryInfo> devices)
    {
        List<DeviceBatteryInfo> orderedDevices = devices.ToList();
        orderedDevices.Sort(DeviceBatteryInfoComparer.Instance);

        Devices.Clear();

        foreach (DeviceBatteryInfo device in orderedDevices)
        {
            Devices.Add(device);
        }

        RaiseCollectionDerivedProperties();
    }

    public void MarkDevicesAsStale(DeviceSnapshotState snapshotState = DeviceSnapshotState.RefreshFailedCache)
    {
        if (Devices.Count == 0)
        {
            return;
        }

        List<DeviceBatteryInfo> staleDevices = Devices
            .Select(device => device.ToStaleSnapshot(snapshotState))
            .ToList();

        SetDevices(staleDevices);
    }

    public void UpdateEmptyState(string title, string description)
    {
        EmptyStateTitle = title;
        EmptyStateDescription = description;
    }

    public void UpdateLastRefresh(DateTimeOffset? lastRefreshUtc)
    {
        LastRefreshText = lastRefreshUtc is DateTimeOffset timestamp
            ? AppStrings.BuildLastRefreshText(timestamp)
            : AppStrings.NoSuccessfulRefreshYet;
    }

    public bool HasDevices => Devices.Count > 0;

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseCollectionDerivedProperties();
    }

    private void RaiseCollectionDerivedProperties()
    {
        OnPropertyChanged(nameof(DeviceCountText));
        OnPropertyChanged(nameof(LowestBatteryText));
        OnPropertyChanged(nameof(DeviceSectionTitle));
        OnPropertyChanged(nameof(DeviceListVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
