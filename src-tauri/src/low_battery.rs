use std::collections::{HashMap, HashSet};

use serde::Serialize;

use crate::{bluetooth::RefreshResult, settings::AppSettings};

const RECOVERY_HYSTERESIS_PERCENT: u8 = 5;

#[derive(Clone, Debug, Eq, PartialEq, Serialize)]
pub struct LowBatteryAlert {
    pub device_id: String,
    pub display_name: String,
    pub battery_percent: u8,
    pub threshold: u8,
}

#[derive(Debug, Default)]
pub struct LowBatteryAlertState {
    alerted_device_ids: HashSet<String>,
}

impl LowBatteryAlertState {
    pub fn evaluate(
        &mut self,
        result: &RefreshResult,
        settings: &AppSettings,
    ) -> Vec<LowBatteryAlert> {
        if !settings.low_battery_system_notification_enabled {
            self.alerted_device_ids.clear();
            return Vec::new();
        }

        let threshold = settings.low_battery_threshold;
        let recovery_threshold = threshold.saturating_add(RECOVERY_HYSTERESIS_PERCENT);
        let devices_by_id = result
            .devices
            .iter()
            .map(|device| (device.device_id.as_str(), device.battery_percent))
            .collect::<HashMap<_, _>>();

        self.alerted_device_ids.retain(|device_id| {
            devices_by_id
                .get(device_id.as_str())
                .is_some_and(|percent| *percent < recovery_threshold)
        });

        let mut alerts = Vec::new();
        for device in &result.devices {
            if device.battery_percent > threshold
                || self.alerted_device_ids.contains(&device.device_id)
            {
                continue;
            }

            self.alerted_device_ids.insert(device.device_id.clone());
            alerts.push(LowBatteryAlert {
                device_id: device.device_id.clone(),
                display_name: device.display_name.clone(),
                battery_percent: device.battery_percent,
                threshold,
            });
        }

        alerts
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::{
        bluetooth::{DeviceBatteryInfo, RefreshResult},
        settings::AppSettings,
    };

    fn result(devices: Vec<DeviceBatteryInfo>) -> RefreshResult {
        RefreshResult {
            devices,
            connected_le_device_count: 1,
            refreshed_at_ms: 123,
            errors: Vec::new(),
            issues: Vec::new(),
        }
    }

    fn device(id: &str, name: &str, percent: u8) -> DeviceBatteryInfo {
        DeviceBatteryInfo {
            device_id: id.to_string(),
            display_name: name.to_string(),
            battery_percent: percent,
            connection_state: "已连接".to_string(),
            source_kind: "GATT BAS".to_string(),
            updated_at_ms: 123,
        }
    }

    fn enabled_settings() -> AppSettings {
        AppSettings {
            low_battery_system_notification_enabled: true,
            low_battery_threshold: 20,
            ..AppSettings::default()
        }
    }

    #[test]
    fn disabled_notifications_do_not_emit_alerts() {
        let mut state = LowBatteryAlertState::default();
        let settings = AppSettings {
            low_battery_system_notification_enabled: false,
            low_battery_threshold: 20,
            ..AppSettings::default()
        };

        let alerts = state.evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings);

        assert!(alerts.is_empty());
    }

    #[test]
    fn enabled_notifications_emit_once_when_device_enters_low_battery() {
        let mut state = LowBatteryAlertState::default();
        let settings = enabled_settings();

        let alerts = state.evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings);
        let repeated = state.evaluate(&result(vec![device("keyboard", "Keychron", 17)]), &settings);

        assert_eq!(
            alerts,
            vec![LowBatteryAlert {
                device_id: "keyboard".to_string(),
                display_name: "Keychron".to_string(),
                battery_percent: 18,
                threshold: 20,
            }]
        );
        assert!(repeated.is_empty());
    }

    #[test]
    fn recovered_device_can_alert_again_after_hysteresis() {
        let mut state = LowBatteryAlertState::default();
        let settings = enabled_settings();

        assert_eq!(
            state
                .evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings)
                .len(),
            1
        );
        assert!(
            state
                .evaluate(&result(vec![device("keyboard", "Keychron", 24)]), &settings)
                .is_empty()
        );
        assert!(
            state
                .evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings)
                .is_empty()
        );
        assert!(
            state
                .evaluate(&result(vec![device("keyboard", "Keychron", 25)]), &settings)
                .is_empty()
        );

        let alerts = state.evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings);

        assert_eq!(alerts.len(), 1);
    }

    #[test]
    fn disconnected_device_resets_alert_state() {
        let mut state = LowBatteryAlertState::default();
        let settings = enabled_settings();

        assert_eq!(
            state
                .evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings)
                .len(),
            1
        );
        assert!(state.evaluate(&result(Vec::new()), &settings).is_empty());

        let alerts = state.evaluate(&result(vec![device("keyboard", "Keychron", 18)]), &settings);

        assert_eq!(alerts.len(), 1);
    }
}
