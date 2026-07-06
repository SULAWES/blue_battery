use std::collections::VecDeque;

use crate::bluetooth::RefreshResult;

const DEFAULT_CAPACITY: usize = 64;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RefreshSource {
    Foreground,
    Background,
}

impl RefreshSource {
    fn as_str(self) -> &'static str {
        match self {
            Self::Foreground => "foreground",
            Self::Background => "background",
        }
    }
}

#[derive(Debug)]
pub struct Diagnostics {
    capacity: usize,
    events: VecDeque<String>,
}

impl Default for Diagnostics {
    fn default() -> Self {
        Self::with_capacity(DEFAULT_CAPACITY)
    }
}

impl Diagnostics {
    pub fn with_capacity(capacity: usize) -> Self {
        Self {
            capacity: capacity.max(1),
            events: VecDeque::new(),
        }
    }

    pub fn record_event(&mut self, event: impl Into<String>) {
        if self.events.len() == self.capacity {
            let _ = self.events.pop_front();
        }
        self.events.push_back(event.into());
    }

    pub fn record_refresh_result(&mut self, source: RefreshSource, result: &RefreshResult) {
        let mut event = format!(
            "refresh ok: source={} refreshed_at_ms={} displayable_devices={} connected_ble={}",
            source.as_str(),
            result.refreshed_at_ms,
            result.devices.len(),
            result.connected_le_device_count
        );

        if !result.errors.is_empty() {
            event.push_str(" errors=");
            event.push_str(&result.errors.join(" | "));
        }

        if !result.issues.is_empty() {
            event.push_str(" issues=");
            event.push_str(
                &result
                    .issues
                    .iter()
                    .map(|issue| issue.summary())
                    .collect::<Vec<_>>()
                    .join(" | "),
            );
        }

        self.record_event(event);
    }

    pub fn record_refresh_failure(&mut self, source: RefreshSource, message: impl AsRef<str>) {
        self.record_event(format!(
            "refresh failed: source={} error={}",
            source.as_str(),
            message.as_ref()
        ));
    }

    pub fn record_refresh_skipped(&mut self, source: RefreshSource, reason: impl AsRef<str>) {
        self.record_event(format!(
            "refresh skipped: source={} reason={}",
            source.as_str(),
            reason.as_ref()
        ));
    }

    pub fn report(&self) -> String {
        if self.events.is_empty() {
            return "Blue Battery diagnostics\nNo diagnostic events recorded.".to_string();
        }

        let mut report = format!(
            "Blue Battery diagnostics\nretained_events={}/{}",
            self.events.len(),
            self.capacity
        );

        for event in &self.events {
            report.push('\n');
            report.push_str(event);
        }

        report
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::bluetooth::{DeviceBatteryInfo, DeviceReadIssue, DeviceReadStatus, RefreshResult};

    fn result(errors: Vec<String>) -> RefreshResult {
        RefreshResult {
            devices: vec![DeviceBatteryInfo {
                device_id: "keyboard".to_string(),
                display_name: "Keychron Z6 Ultra 8K".to_string(),
                battery_percent: 48,
                connection_state: "已连接".to_string(),
                source_kind: "GATT BAS".to_string(),
                updated_at_ms: 123,
            }],
            connected_le_device_count: 2,
            refreshed_at_ms: 456,
            errors,
            issues: Vec::new(),
        }
    }

    #[test]
    fn report_records_refresh_counts_time_and_errors() {
        let mut diagnostics = Diagnostics::default();

        diagnostics.record_refresh_result(
            RefreshSource::Foreground,
            &result(vec!["Keyboard: HRESULT 0x80070490".to_string()]),
        );

        let report = diagnostics.report();

        assert!(report.contains("source=foreground"));
        assert!(report.contains("refreshed_at_ms=456"));
        assert!(report.contains("displayable_devices=1"));
        assert!(report.contains("connected_ble=2"));
        assert!(report.contains("Keyboard: HRESULT 0x80070490"));
    }

    #[test]
    fn report_records_structured_device_issues() {
        let mut diagnostics = Diagnostics::default();
        let mut result = result(Vec::new());
        result.issues = vec![DeviceReadIssue {
            device_id: "mouse".to_string(),
            display_name: "OPPO Enco Free4".to_string(),
            status: DeviceReadStatus::NoStandardBatteryService,
            message: "No standard Battery Service was exposed.".to_string(),
        }];

        diagnostics.record_refresh_result(RefreshSource::Background, &result);

        let report = diagnostics.report();

        assert!(report.contains("issues=OPPO Enco Free4: 无标准 Battery Service"));
    }

    #[test]
    fn report_records_refresh_failures() {
        let mut diagnostics = Diagnostics::default();

        diagnostics.record_refresh_failure(RefreshSource::Background, "Bluetooth task failed");

        let report = diagnostics.report();

        assert!(report.contains("source=background"));
        assert!(report.contains("Bluetooth task failed"));
    }

    #[test]
    fn report_records_skipped_refreshes() {
        let mut diagnostics = Diagnostics::default();

        diagnostics
            .record_refresh_skipped(RefreshSource::Foreground, "refresh interval not elapsed");

        let report = diagnostics.report();

        assert!(report.contains("refresh skipped"));
        assert!(report.contains("source=foreground"));
        assert!(report.contains("refresh interval not elapsed"));
    }

    #[test]
    fn diagnostics_keep_a_bounded_history() {
        let mut diagnostics = Diagnostics::with_capacity(2);

        diagnostics.record_event("first");
        diagnostics.record_event("second");
        diagnostics.record_event("third");

        let report = diagnostics.report();

        assert!(!report.contains("first"));
        assert!(report.contains("second"));
        assert!(report.contains("third"));
    }
}
