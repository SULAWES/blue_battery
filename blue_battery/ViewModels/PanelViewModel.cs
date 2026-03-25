using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using BlueBattery.Models;
using Microsoft.UI.Xaml;

namespace BlueBattery.ViewModels;

public sealed class PanelViewModel : INotifyPropertyChanged
{
    private string _statusMessage = "等待蓝牙读取服务接入。";

    public PanelViewModel()
    {
        Devices.CollectionChanged += OnDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DeviceBatteryInfo> Devices { get; } = [];

    public string Subtitle => "显示当前已连接且可通过公开标准接口读取电量的设备。";

    public string ScopeHint => "仅显示 Windows 可原生读取电量且本应用读取成功的设备。";

    public string EmptyStateTitle => "暂无可显示设备";

    public string EmptyStateDescription => "设备列表结构已经就绪。接入蓝牙读取后，这里会只显示已连接且通过 GATT Battery Service 成功读取到电量的设备。";

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
        : "设备列表";

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
        Devices.Clear();

        foreach (DeviceBatteryInfo device in devices)
        {
            Devices.Add(device);
        }

        RaiseCollectionDerivedProperties();
    }

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
