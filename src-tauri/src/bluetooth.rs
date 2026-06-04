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

    for device_info in device_infos {
        match read_device(device_info, refreshed_at_ms) {
            Ok(Some(device)) => devices.push(device),
            Ok(None) => {}
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
    })
}

fn read_device(
    device_info: DeviceInformation,
    refreshed_at_ms: i64,
) -> Result<Option<DeviceBatteryInfo>, String> {
    let device_id = device_info
        .Id()
        .map_err(|error| format!("Failed to read device id: {error}"))?;

    let bluetooth_device = BluetoothLEDevice::FromIdAsync(&device_id)
        .map_err(|error| format!("Failed to open Bluetooth device: {error}"))?
        .join()
        .map_err(|error| format!("Failed to open Bluetooth device: {error}"))?;

    if bluetooth_device
        .ConnectionStatus()
        .map_err(|error| format!("Failed to read connection status: {error}"))?
        != BluetoothConnectionStatus::Connected
    {
        let _ = bluetooth_device.Close();
        return Ok(None);
    }

    let display_name = resolve_display_name(&device_info, &bluetooth_device);
    let battery_percent = read_first_battery_percent(&bluetooth_device)
        .map_err(|error| format!("{display_name}: {error}"))?;

    let _ = bluetooth_device.Close();

    let Some(battery_percent) = battery_percent else {
        return Ok(None);
    };

    Ok(Some(DeviceBatteryInfo {
        device_id: device_id.to_string_lossy(),
        display_name,
        battery_percent,
        connection_state: "已连接".to_string(),
        source_kind: "GATT BAS".to_string(),
        updated_at_ms: refreshed_at_ms,
    }))
}

fn read_first_battery_percent(device: &BluetoothLEDevice) -> WindowsResult<Option<u8>> {
    let services_result = device
        .GetGattServicesForUuidWithCacheModeAsync(
            GattServiceUuids::Battery()?,
            BluetoothCacheMode::Uncached,
        )?
        .join()?;

    if services_result.Status()? != GattCommunicationStatus::Success {
        return Ok(None);
    }

    for service in services_result.Services()? {
        let percent = read_battery_service(&service)?;
        let _ = service.Close();

        if percent.is_some() {
            return Ok(percent);
        }
    }

    Ok(None)
}

fn read_battery_service(service: &GattDeviceService) -> WindowsResult<Option<u8>> {
    let characteristics_result = service
        .GetCharacteristicsForUuidWithCacheModeAsync(
            GattCharacteristicUuids::BatteryLevel()?,
            BluetoothCacheMode::Uncached,
        )?
        .join()?;

    if characteristics_result.Status()? != GattCommunicationStatus::Success {
        return Ok(None);
    }

    for characteristic in characteristics_result.Characteristics()? {
        let read_result = characteristic
            .ReadValueWithCacheModeAsync(BluetoothCacheMode::Uncached)?
            .join()?;

        if read_result.Status()? != GattCommunicationStatus::Success {
            continue;
        }

        if let Some(percent) = read_battery_level(read_result.Value()?)? {
            return Ok(Some(percent));
        }
    }

    Ok(None)
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
        .unwrap_or_else(|| "未知设备".to_string())
}

fn unix_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis()
        .try_into()
        .unwrap_or(i64::MAX)
}
