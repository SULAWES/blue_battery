use serde::{Deserialize, Serialize};
use std::{
    fs::{self, File},
    io::Write,
    path::{Path, PathBuf},
    time::Duration,
};
use tauri::{AppHandle, Manager};

const SETTINGS_FILE_NAME: &str = "settings.json";
const SCHEMA_VERSION: u32 = 1;
const DEFAULT_REFRESH_INTERVAL_SECONDS: u64 = 60;
const DEFAULT_LOW_BATTERY_THRESHOLD: u8 = 20;
const ALLOWED_REFRESH_INTERVAL_SECONDS: [u64; 3] = [30, 60, 120];
const ALLOWED_LOW_BATTERY_THRESHOLDS: [u8; 4] = [10, 15, 20, 25];

#[derive(Clone, Debug, Deserialize, Eq, PartialEq, Serialize)]
#[serde(default, rename_all = "camelCase")]
pub struct AppSettings {
    pub schema_version: u32,
    pub refresh_interval_seconds: u64,
    pub low_battery_status_enabled: bool,
    pub low_battery_threshold: u8,
    pub low_battery_system_notification_enabled: bool,
    pub show_panel_on_startup: bool,
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            schema_version: SCHEMA_VERSION,
            refresh_interval_seconds: DEFAULT_REFRESH_INTERVAL_SECONDS,
            low_battery_status_enabled: true,
            low_battery_threshold: DEFAULT_LOW_BATTERY_THRESHOLD,
            low_battery_system_notification_enabled: false,
            show_panel_on_startup: false,
        }
    }
}

impl AppSettings {
    pub fn refresh_interval(&self) -> Duration {
        Duration::from_secs(self.refresh_interval_seconds)
    }
}

#[derive(Debug)]
pub struct LoadOutcome {
    pub settings: AppSettings,
    pub diagnostics: Vec<String>,
}

pub fn settings_path(app: &AppHandle) -> Result<PathBuf, String> {
    app.path()
        .app_config_dir()
        .map(|path| path.join(SETTINGS_FILE_NAME))
        .map_err(|error| format!("Failed to resolve settings directory: {error}"))
}

pub fn load(app: &AppHandle) -> Result<LoadOutcome, String> {
    load_from_path(&settings_path(app)?)
}

pub fn save(app: &AppHandle, settings: &AppSettings) -> Result<AppSettings, String> {
    save_to_path(&settings_path(app)?, settings)
}

pub fn reset(app: &AppHandle) -> Result<AppSettings, String> {
    save(app, &AppSettings::default())
}

pub fn load_from_path(path: &Path) -> Result<LoadOutcome, String> {
    if !path.exists() {
        return Ok(LoadOutcome {
            settings: AppSettings::default(),
            diagnostics: Vec::new(),
        });
    }

    let contents = fs::read_to_string(path)
        .map_err(|error| format!("Failed to read settings {}: {error}", path.display()))?;

    let settings = match serde_json::from_str::<AppSettings>(&contents) {
        Ok(settings) => settings,
        Err(error) => {
            return Ok(LoadOutcome {
                settings: AppSettings::default(),
                diagnostics: vec![format!(
                    "settings parse failed; using defaults: {}: {error}",
                    path.display()
                )],
            });
        }
    };

    Ok(sanitize(settings))
}

pub fn save_to_path(path: &Path, settings: &AppSettings) -> Result<AppSettings, String> {
    let outcome = sanitize(settings.clone());
    let settings = outcome.settings;

    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| {
            format!(
                "Failed to create settings directory {}: {error}",
                parent.display()
            )
        })?;
    }

    let temp_path = path.with_extension("json.tmp");
    let contents = serde_json::to_string_pretty(&settings)
        .map_err(|error| format!("Failed to serialize settings: {error}"))?;

    {
        let mut file = File::create(&temp_path).map_err(|error| {
            format!(
                "Failed to create temporary settings file {}: {error}",
                temp_path.display()
            )
        })?;
        file.write_all(contents.as_bytes()).map_err(|error| {
            format!(
                "Failed to write temporary settings file {}: {error}",
                temp_path.display()
            )
        })?;
        file.sync_all().map_err(|error| {
            format!(
                "Failed to flush temporary settings file {}: {error}",
                temp_path.display()
            )
        })?;
    }

    replace_settings_file(&temp_path, path)?;
    Ok(settings)
}

fn sanitize(mut settings: AppSettings) -> LoadOutcome {
    let mut diagnostics = Vec::new();

    if settings.schema_version != SCHEMA_VERSION {
        diagnostics.push(format!(
            "settings schemaVersion={} is unsupported; using schemaVersion={SCHEMA_VERSION}",
            settings.schema_version
        ));
        settings.schema_version = SCHEMA_VERSION;
    }

    if !ALLOWED_REFRESH_INTERVAL_SECONDS.contains(&settings.refresh_interval_seconds) {
        diagnostics.push(format!(
            "settings refreshIntervalSeconds={} is invalid; using {DEFAULT_REFRESH_INTERVAL_SECONDS}",
            settings.refresh_interval_seconds
        ));
        settings.refresh_interval_seconds = DEFAULT_REFRESH_INTERVAL_SECONDS;
    }

    if !ALLOWED_LOW_BATTERY_THRESHOLDS.contains(&settings.low_battery_threshold) {
        diagnostics.push(format!(
            "settings lowBatteryThreshold={} is invalid; using {DEFAULT_LOW_BATTERY_THRESHOLD}",
            settings.low_battery_threshold
        ));
        settings.low_battery_threshold = DEFAULT_LOW_BATTERY_THRESHOLD;
    }

    LoadOutcome {
        settings,
        diagnostics,
    }
}

fn replace_settings_file(temp_path: &Path, target_path: &Path) -> Result<(), String> {
    if !target_path.exists() {
        return fs::rename(temp_path, target_path).map_err(|error| {
            format!(
                "Failed to move settings file {} to {}: {error}",
                temp_path.display(),
                target_path.display()
            )
        });
    }

    let backup_path = target_path.with_extension("json.bak");
    if backup_path.exists() {
        let _ = fs::remove_file(&backup_path);
    }

    fs::rename(target_path, &backup_path).map_err(|error| {
        format!(
            "Failed to prepare settings replacement {}: {error}",
            target_path.display()
        )
    })?;

    match fs::rename(temp_path, target_path) {
        Ok(()) => {
            let _ = fs::remove_file(&backup_path);
            Ok(())
        }
        Err(error) => {
            let _ = fs::rename(&backup_path, target_path);
            Err(format!(
                "Failed to replace settings file {}: {error}",
                target_path.display()
            ))
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        fs,
        path::PathBuf,
        time::{SystemTime, UNIX_EPOCH},
    };

    fn temp_settings_path(name: &str) -> PathBuf {
        let unique = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        std::env::temp_dir()
            .join("blue-battery-settings-tests")
            .join(format!("{name}-{unique}"))
            .join("settings.json")
    }

    #[test]
    fn missing_settings_file_returns_defaults() {
        let path = temp_settings_path("missing");

        let outcome = load_from_path(&path).expect("load settings");

        assert_eq!(outcome.settings, AppSettings::default());
        assert!(outcome.diagnostics.is_empty());
    }

    #[test]
    fn invalid_settings_values_fall_back_to_defaults_with_diagnostics() {
        let path = temp_settings_path("invalid-values");
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        fs::write(
            &path,
            r#"{
              "schemaVersion": 99,
              "refreshIntervalSeconds": 5,
              "lowBatteryStatusEnabled": false,
              "lowBatteryThreshold": 50,
              "lowBatterySystemNotificationEnabled": true,
              "showPanelOnStartup": true
            }"#,
        )
        .unwrap();

        let outcome = load_from_path(&path).expect("load settings");

        assert_eq!(outcome.settings.schema_version, 1);
        assert_eq!(outcome.settings.refresh_interval_seconds, 60);
        assert!(!outcome.settings.low_battery_status_enabled);
        assert_eq!(outcome.settings.low_battery_threshold, 20);
        assert!(outcome.settings.low_battery_system_notification_enabled);
        assert!(outcome.settings.show_panel_on_startup);
        assert!(
            outcome
                .diagnostics
                .iter()
                .any(|event| event.contains("schemaVersion"))
        );
        assert!(
            outcome
                .diagnostics
                .iter()
                .any(|event| event.contains("refreshIntervalSeconds"))
        );
        assert!(
            outcome
                .diagnostics
                .iter()
                .any(|event| event.contains("lowBatteryThreshold"))
        );
    }

    #[test]
    fn save_settings_writes_camel_case_json_and_round_trips() {
        let path = temp_settings_path("round-trip");
        let settings = AppSettings {
            refresh_interval_seconds: 120,
            low_battery_threshold: 15,
            low_battery_status_enabled: false,
            show_panel_on_startup: true,
            ..AppSettings::default()
        };

        save_to_path(&path, &settings).expect("save settings");

        let contents = fs::read_to_string(&path).expect("read settings json");
        assert!(contents.contains("\"schemaVersion\""));
        assert!(contents.contains("\"refreshIntervalSeconds\""));
        assert!(contents.contains("\"lowBatteryThreshold\""));
        assert!(!contents.contains("refresh_interval_seconds"));

        let outcome = load_from_path(&path).expect("load saved settings");
        assert_eq!(outcome.settings, settings);
    }
}
