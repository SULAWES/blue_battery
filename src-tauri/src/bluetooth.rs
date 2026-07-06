use std::time::{SystemTime, UNIX_EPOCH};

use serde::Serialize;
use windows::{
    Devices::{
        Bluetooth::{
            BluetoothCacheMode, BluetoothConnectionStatus, BluetoothLEDevice,
            GenericAttributeProfile::{
                GattCharacteristicUuids, GattCommunicationStatus, GattDeviceService,
                GattServiceUuids,
            },
        },
        Enumeration::DeviceInformation,
    },
    Storage::Streams::{DataReader, IBuffer},
    core::Result as WindowsResult,
};

#[derive(Clone, Debug, Serialize)]
pub struct DeviceBatteryInfo {
    pub device_id: String,
    pub display_name: String,
    pub battery_percent: u8,
    pub connection_state: String,
    pub source_kind: String,
    pub updated_at_ms: i64,
}

#[derive(Clone, Debug, Serialize)]
pub struct RefreshResult {
    pub devices: Vec<DeviceBatteryInfo>,
    pub connected_le_device_count: u32,
    pub refreshed_at_ms: i64,
    pub errors: Vec<String>,
    pub issues: Vec<DeviceReadIssue>,
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum DeviceReadStatus {
    NotConnected,
    NoStandardBatteryService,
    Unreadable,
    ReadFailed,
}

impl DeviceReadStatus {
    pub fn label(&self) -> &'static str {
        match self {
            Self::NotConnected => "未连接",
            Self::NoStandardBatteryService => "无标准 Battery Service",
            Self::Unreadable => "不可读",
            Self::ReadFailed => "读取失败",
        }
    }
}

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct DeviceReadIssue {
    pub device_id: String,
    pub display_name: String,
    pub status: DeviceReadStatus,
    pub message: String,
}

impl DeviceReadIssue {
    pub fn summary(&self) -> String {
        format!("{}: {}", self.display_name, self.status.label())
    }
}

pub fn read_connected_devices() -> Result<RefreshResult, String> {
    let refreshed_at_ms = unix_ms();
    let selector = BluetoothLEDevice::GetDeviceSelectorFromConnectionStatus(
        BluetoothConnectionStatus::Connected,
    )
    .map_err(|error| format!("Failed to create Bluetooth selector: {error}"))?;

    let device_infos = DeviceInformation::FindAllAsyncAqsFilter(&selector)
        .map_err(|error| format!("Failed to enumerate Bluetooth devices: {error}"))?
        .join()
        .map_err(|error| format!("Failed to enumerate Bluetooth devices: {error}"))?;

    let connected_le_device_count = device_infos
        .Size()
        .map_err(|error| format!("Failed to read Bluetooth device count: {error}"))?;

    let mut devices = Vec::new();
    let mut errors = Vec::new();
    let mut issues = Vec::new();

    for device_info in device_infos {
        match read_device(device_info, refreshed_at_ms) {
            Ok(DeviceReadOutcome::Device(device)) => devices.push(device),
            Ok(DeviceReadOutcome::Issue(issue)) => issues.push(issue),
            Err(error) => errors.push(error),
        }
    }

    devices.sort_by(|left, right| {
        left.battery_percent
            .cmp(&right.battery_percent)
            .then_with(|| left.display_name.cmp(&right.display_name))
    });

    Ok(RefreshResult {
        devices,
        connected_le_device_count,
        refreshed_at_ms,
        errors,
        issues,
    })
}

enum DeviceReadOutcome {
    Device(DeviceBatteryInfo),
    Issue(DeviceReadIssue),
}

fn read_device(
    device_info: DeviceInformation,
    refreshed_at_ms: i64,
) -> Result<DeviceReadOutcome, String> {
    let device_id = device_info
        .Id()
        .map_err(|error| format!("Failed to read device id: {error}"))?;
    let device_id_text = device_id.to_string_lossy();
    let fallback_name = resolve_device_info_name(&device_info);

    let bluetooth_device = match BluetoothLEDevice::FromIdAsync(&device_id)
        .map_err(|error| format!("Failed to open Bluetooth device: {error}"))
        .and_then(|operation| {
            operation
                .join()
                .map_err(|error| format!("Failed to open Bluetooth device: {error}"))
        }) {
        Ok(device) => device,
        Err(error) => {
            return Ok(DeviceReadOutcome::Issue(device_issue(
                device_id_text,
                fallback_name,
                DeviceReadStatus::ReadFailed,
                error,
            )));
        }
    };

    if bluetooth_device
        .ConnectionStatus()
        .map_err(|error| format!("Failed to read connection status: {error}"))?
        != BluetoothConnectionStatus::Connected
    {
        let _ = bluetooth_device.Close();
        return Ok(DeviceReadOutcome::Issue(device_issue(
            device_id_text,
            fallback_name,
            DeviceReadStatus::NotConnected,
            "Device was no longer connected when opened.",
        )));
    }

    let display_name = resolve_display_name(&device_info, &bluetooth_device);
    let battery_percent = read_first_battery_percent(&bluetooth_device);

    let _ = bluetooth_device.Close();

    let battery_percent = match battery_percent {
        Ok(BatteryReadResult::Percent(percent)) => percent,
        Ok(BatteryReadResult::NoStandardBatteryService) => {
            return Ok(DeviceReadOutcome::Issue(device_issue(
                device_id_text,
                display_name,
                DeviceReadStatus::NoStandardBatteryService,
                "No standard Battery Service or Battery Level characteristic was exposed.",
            )));
        }
        Ok(BatteryReadResult::Unreadable) => {
            return Ok(DeviceReadOutcome::Issue(device_issue(
                device_id_text,
                display_name,
                DeviceReadStatus::Unreadable,
                "Windows found a standard battery path but did not return a readable value.",
            )));
        }
        Err(error) => {
            return Ok(DeviceReadOutcome::Issue(device_issue(
                device_id_text,
                display_name,
                DeviceReadStatus::ReadFailed,
                error.to_string(),
            )));
        }
    };

    Ok(DeviceReadOutcome::Device(DeviceBatteryInfo {
        device_id: device_id_text,
        display_name,
        battery_percent,
        connection_state: "已连接".to_string(),
        source_kind: "GATT BAS".to_string(),
        updated_at_ms: refreshed_at_ms,
    }))
}

enum BatteryReadResult {
    Percent(u8),
    NoStandardBatteryService,
    Unreadable,
}

fn read_first_battery_percent(device: &BluetoothLEDevice) -> WindowsResult<BatteryReadResult> {
    let services_result = device
        .GetGattServicesForUuidWithCacheModeAsync(
            GattServiceUuids::Battery()?,
            BluetoothCacheMode::Uncached,
        )?
        .join()?;

    if services_result.Status()? != GattCommunicationStatus::Success {
        return Ok(BatteryReadResult::Unreadable);
    }

    let mut saw_service = false;
    let mut saw_unreadable = false;

    for service in services_result.Services()? {
        saw_service = true;
        let percent = read_battery_service(&service)?;
        let _ = service.Close();

        match percent {
            BatteryReadResult::Percent(percent) => return Ok(BatteryReadResult::Percent(percent)),
            BatteryReadResult::Unreadable => saw_unreadable = true,
            BatteryReadResult::NoStandardBatteryService => {}
        }
    }

    if !saw_service {
        return Ok(BatteryReadResult::NoStandardBatteryService);
    }

    if saw_unreadable {
        Ok(BatteryReadResult::Unreadable)
    } else {
        Ok(BatteryReadResult::NoStandardBatteryService)
    }
}

fn read_battery_service(service: &GattDeviceService) -> WindowsResult<BatteryReadResult> {
    let characteristics_result = service
        .GetCharacteristicsForUuidWithCacheModeAsync(
            GattCharacteristicUuids::BatteryLevel()?,
            BluetoothCacheMode::Uncached,
        )?
        .join()?;

    if characteristics_result.Status()? != GattCommunicationStatus::Success {
        return Ok(BatteryReadResult::Unreadable);
    }

    let mut saw_characteristic = false;
    let mut saw_unreadable = false;

    for characteristic in characteristics_result.Characteristics()? {
        saw_characteristic = true;
        let read_result = characteristic
            .ReadValueWithCacheModeAsync(BluetoothCacheMode::Uncached)?
            .join()?;

        if read_result.Status()? != GattCommunicationStatus::Success {
            saw_unreadable = true;
            continue;
        }

        if let Some(percent) = read_battery_level(read_result.Value()?)? {
            return Ok(BatteryReadResult::Percent(percent));
        }

        saw_unreadable = true;
    }

    if !saw_characteristic {
        return Ok(BatteryReadResult::NoStandardBatteryService);
    }

    if saw_unreadable {
        Ok(BatteryReadResult::Unreadable)
    } else {
        Ok(BatteryReadResult::NoStandardBatteryService)
    }
}

fn read_battery_level(buffer: IBuffer) -> WindowsResult<Option<u8>> {
    if buffer.Length()? < 1 {
        return Ok(None);
    }

    let reader = DataReader::FromBuffer(&buffer)?;
    Ok(Some(reader.ReadByte()?.min(100)))
}

fn resolve_display_name(
    device_info: &DeviceInformation,
    bluetooth_device: &BluetoothLEDevice,
) -> String {
    bluetooth_device
        .Name()
        .ok()
        .map(|name| name.to_string_lossy())
        .filter(|name| !name.trim().is_empty())
        .or_else(|| {
            device_info
                .Name()
                .ok()
                .map(|name| name.to_string_lossy())
                .filter(|name| !name.trim().is_empty())
        })
        .unwrap_or_else(|| resolve_device_info_name(device_info))
}

fn resolve_device_info_name(device_info: &DeviceInformation) -> String {
    device_info
        .Name()
        .ok()
        .map(|name| name.to_string_lossy())
        .filter(|name| !name.trim().is_empty())
        .unwrap_or_else(|| "未知设备".to_string())
}

fn device_issue(
    device_id: String,
    display_name: String,
    status: DeviceReadStatus,
    message: impl Into<String>,
) -> DeviceReadIssue {
    DeviceReadIssue {
        device_id,
        display_name,
        status,
        message: message.into(),
    }
}

fn unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis()
        .try_into()
        .unwrap_or(i64::MAX)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn device_read_issue_summaries_are_reader_facing() {
        let issue = DeviceReadIssue {
            device_id: "keyboard".to_string(),
            display_name: "Keychron Z6 Ultra 8K".to_string(),
            status: DeviceReadStatus::NoStandardBatteryService,
            message: "No standard Battery Service was exposed.".to_string(),
        };

        assert_eq!(
            issue.summary(),
            "Keychron Z6 Ultra 8K: 无标准 Battery Service"
        );
    }

    #[test]
    fn device_read_statuses_distinguish_connection_and_read_failures() {
        assert_eq!(DeviceReadStatus::NotConnected.label(), "未连接");
        assert_eq!(DeviceReadStatus::Unreadable.label(), "不可读");
        assert_eq!(DeviceReadStatus::ReadFailed.label(), "读取失败");
    }
}
