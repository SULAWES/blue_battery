#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::sync::{
    Mutex,
    atomic::{AtomicBool, Ordering},
};

use bluetooth::RefreshResult;
use diagnostics::{Diagnostics, RefreshSource};
use settings::AppSettings;
use single_instance::InstanceClaim;
use tauri::{AppHandle, Emitter, Manager, tray::TrayIcon};

mod bluetooth;
mod diagnostics;
mod panel_position;
mod panel_window;
mod settings;
mod single_instance;
mod startup;
mod tray;
mod tray_icon;

#[derive(Default)]
struct AppState {
    tray: Mutex<Option<TrayIcon>>,
    single_instance: Mutex<Option<single_instance::InstanceGuard>>,
    latest: Mutex<Option<RefreshResult>>,
    diagnostics: Mutex<Diagnostics>,
    settings: Mutex<AppSettings>,
    background_refresh_running: AtomicBool,
}

#[tauri::command]
async fn refresh_devices(app: AppHandle) -> Result<RefreshResult, String> {
    let task_result = tauri::async_runtime::spawn_blocking(bluetooth::read_connected_devices).await;

    let result = match task_result {
        Ok(Ok(result)) => result,
        Ok(Err(error)) => {
            record_refresh_failure(&app, RefreshSource::Foreground, &error);
            return Err(error);
        }
        Err(error) => {
            let message = format!("Refresh task failed: {error}");
            record_refresh_failure(&app, RefreshSource::Foreground, &message);
            return Err(message);
        }
    };

    if let Err(error) = apply_refresh_result(&app, &result) {
        record_refresh_failure(&app, RefreshSource::Foreground, &error);
        return Err(error);
    }

    record_refresh_result(&app, RefreshSource::Foreground, &result);
    Ok(result)
}

#[tauri::command]
fn get_diagnostics_report(app: AppHandle) -> Result<String, String> {
    diagnostics_report(&app)
}

#[tauri::command]
fn get_app_version() -> &'static str {
    env!("CARGO_PKG_VERSION")
}

#[tauri::command]
fn get_settings(app: AppHandle) -> Result<AppSettings, String> {
    load_settings_into_state(&app)
}

#[tauri::command]
fn update_settings(app: AppHandle, settings: AppSettings) -> Result<AppSettings, String> {
    let settings = settings::save(&app, &settings).map_err(|error| {
        record_diagnostic_event(&app, format!("settings update failed: {error}"));
        error
    })?;

    store_settings(&app, settings.clone())?;
    record_diagnostic_event(
        &app,
        format!(
            "settings updated: refresh_interval_seconds={} low_battery_status_enabled={} low_battery_threshold={} show_panel_on_startup={}",
            settings.refresh_interval_seconds,
            settings.low_battery_status_enabled,
            settings.low_battery_threshold,
            settings.show_panel_on_startup
        ),
    );
    refresh_latest_tray_state(&app)?;
    Ok(settings)
}

#[tauri::command]
fn reset_settings(app: AppHandle) -> Result<AppSettings, String> {
    let settings = settings::reset(&app).map_err(|error| {
        record_diagnostic_event(&app, format!("settings reset failed: {error}"));
        error
    })?;

    store_settings(&app, settings.clone())?;
    record_diagnostic_event(&app, "settings reset");
    refresh_latest_tray_state(&app)?;
    Ok(settings)
}

#[tauri::command]
fn get_startup_enabled() -> Result<bool, String> {
    startup::is_enabled()
}

#[tauri::command]
fn set_startup_enabled(app: AppHandle, enabled: bool) -> Result<bool, String> {
    match startup::set_enabled(enabled) {
        Ok(enabled) => {
            record_diagnostic_event(&app, format!("startup enabled={enabled}"));
            Ok(enabled)
        }
        Err(error) => {
            record_diagnostic_event(&app, format!("startup update failed: {error}"));
            Err(error)
        }
    }
}

#[tauri::command]
fn clear_startup_entry(app: AppHandle) -> Result<bool, String> {
    match startup::clear_entry() {
        Ok(removed) => {
            record_diagnostic_event(&app, format!("startup entry cleared removed={removed}"));
            Ok(removed)
        }
        Err(error) => {
            record_diagnostic_event(&app, format!("startup entry cleanup failed: {error}"));
            Err(error)
        }
    }
}

#[tauri::command]
fn hide_panel(app: AppHandle) -> Result<(), String> {
    panel_window::hide(&app).map_err(|error| error.to_string())
}

#[tauri::command]
fn exit_app(app: AppHandle) {
    record_diagnostic_event(&app, "exit requested");
    app.exit(0);
}

fn main() {
    let instance_guard = match single_instance::claim_or_signal_existing()
        .expect("failed to initialize single-instance guard")
    {
        InstanceClaim::Primary(guard) => guard,
        InstanceClaim::SecondarySignaled => return,
    };

    tauri::Builder::default()
        .manage(AppState::default())
        .setup(move |app| {
            setup_tray(app)?;
            panel_window::register_auto_hide(app.handle())?;
            single_instance::start_activation_listener(&instance_guard, app.handle().clone())
                .map_err(|error| tauri::Error::Anyhow(anyhow::anyhow!(error)))?;

            let state = app.state::<AppState>();
            *state.single_instance.lock().map_err(|_| {
                tauri::Error::Anyhow(anyhow::anyhow!("single-instance state lock poisoned"))
            })? = Some(instance_guard);

            let app_handle = app.handle().clone();
            record_diagnostic_event(&app_handle, "app started");
            let settings = load_settings_into_state(&app_handle)
                .map_err(|error| tauri::Error::Anyhow(anyhow::anyhow!(error)))?;

            if settings.show_panel_on_startup {
                let _ = panel_window::show(&app_handle, None);
            }

            refresh_in_background(app_handle.clone());
            start_auto_refresh(app_handle)?;

            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            refresh_devices,
            get_diagnostics_report,
            get_app_version,
            get_settings,
            update_settings,
            reset_settings,
            get_startup_enabled,
            set_startup_enabled,
            clear_startup_entry,
            hide_panel,
            exit_app
        ])
        .run(tauri::generate_context!())
        .expect("error while running Blue Battery");
}

fn setup_tray(app: &mut tauri::App) -> tauri::Result<()> {
    let tray = tray::setup(app, refresh_in_background)?;

    let state = app.state::<AppState>();
    *state
        .tray
        .lock()
        .map_err(|_| tauri::Error::Anyhow(anyhow::anyhow!("tray state lock poisoned")))? =
        Some(tray);

    Ok(())
}

fn start_auto_refresh(app: AppHandle) -> tauri::Result<()> {
    std::thread::Builder::new()
        .name("blue-battery-refresh".to_string())
        .spawn(move || {
            loop {
                std::thread::sleep(current_settings(&app).refresh_interval());
                refresh_in_background(app.clone());
            }
        })
        .map(|_| ())
        .map_err(|error| tauri::Error::Anyhow(anyhow::anyhow!("auto refresh thread: {error}")))
}

fn refresh_in_background(app: AppHandle) {
    let state = app.state::<AppState>();
    if !try_begin_background_refresh(&state.background_refresh_running) {
        record_diagnostic_event(&app, "background refresh skipped: already running");
        return;
    }

    tauri::async_runtime::spawn(async move {
        let task_result =
            tauri::async_runtime::spawn_blocking(bluetooth::read_connected_devices).await;

        match task_result {
            Ok(Ok(result)) => match apply_refresh_result(&app, &result) {
                Ok(()) => {
                    record_refresh_result(&app, RefreshSource::Background, &result);
                    let _ = app.emit("devices-refreshed", result);
                }
                Err(error) => record_refresh_failure(&app, RefreshSource::Background, &error),
            },
            Ok(Err(error)) => record_refresh_failure(&app, RefreshSource::Background, &error),
            Err(error) => record_refresh_failure(
                &app,
                RefreshSource::Background,
                &format!("Refresh task failed: {error}"),
            ),
        }

        let state = app.state::<AppState>();
        finish_background_refresh(&state.background_refresh_running);
    });
}

fn try_begin_background_refresh(running: &AtomicBool) -> bool {
    !running.swap(true, Ordering::AcqRel)
}

fn finish_background_refresh(running: &AtomicBool) {
    running.store(false, Ordering::Release);
}

fn load_settings_into_state(app: &AppHandle) -> Result<AppSettings, String> {
    let outcome = settings::load(app)?;
    for event in &outcome.diagnostics {
        record_diagnostic_event(app, event);
    }
    store_settings(app, outcome.settings.clone())?;
    Ok(outcome.settings)
}

fn store_settings(app: &AppHandle, settings: AppSettings) -> Result<(), String> {
    let state = app.state::<AppState>();
    *state
        .settings
        .lock()
        .map_err(|_| "settings state lock poisoned".to_string())? = settings;
    Ok(())
}

fn current_settings(app: &AppHandle) -> AppSettings {
    let state = app.state::<AppState>();
    state
        .settings
        .lock()
        .map(|settings| settings.clone())
        .unwrap_or_default()
}

fn refresh_latest_tray_state(app: &AppHandle) -> Result<(), String> {
    let latest = {
        let state = app.state::<AppState>();
        state
            .latest
            .lock()
            .map_err(|_| "latest state lock poisoned".to_string())?
            .clone()
    };

    if let Some(result) = latest {
        apply_refresh_result(app, &result)?;
    }

    Ok(())
}

fn apply_refresh_result(app: &AppHandle, result: &RefreshResult) -> Result<(), String> {
    let state = app.state::<AppState>();

    *state
        .latest
        .lock()
        .map_err(|_| "latest state lock poisoned".to_string())? = Some(result.clone());

    if let Some(tray) = state
        .tray
        .lock()
        .map_err(|_| "tray state lock poisoned".to_string())?
        .as_ref()
    {
        let lowest = result
            .devices
            .iter()
            .map(|device| device.battery_percent)
            .min();
        let settings = current_settings(app);
        tray.set_icon(Some(tray_icon::render_battery_icon(lowest)))
            .map_err(|error| format!("Failed to update tray icon: {error}"))?;
        tray.set_tooltip(Some(tray::build_tooltip(result, &settings)))
            .map_err(|error| format!("Failed to update tray tooltip: {error}"))?;
    }

    Ok(())
}

fn record_diagnostic_event(app: &AppHandle, event: impl Into<String>) {
    let state = app.state::<AppState>();
    if let Ok(mut diagnostics) = state.diagnostics.lock() {
        diagnostics.record_event(event);
    }
}

fn record_refresh_result(app: &AppHandle, source: RefreshSource, result: &RefreshResult) {
    let state = app.state::<AppState>();
    if let Ok(mut diagnostics) = state.diagnostics.lock() {
        diagnostics.record_refresh_result(source, result);
    }
}

fn record_refresh_failure(app: &AppHandle, source: RefreshSource, message: impl AsRef<str>) {
    let state = app.state::<AppState>();
    if let Ok(mut diagnostics) = state.diagnostics.lock() {
        diagnostics.record_refresh_failure(source, message);
    }
}

fn diagnostics_report(app: &AppHandle) -> Result<String, String> {
    let state = app.state::<AppState>();
    state
        .diagnostics
        .lock()
        .map(|diagnostics| diagnostics.report())
        .map_err(|_| "diagnostics state lock poisoned".to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::AtomicBool;

    #[test]
    fn background_refresh_gate_prevents_overlapping_refreshes() {
        let running = AtomicBool::new(false);

        assert!(try_begin_background_refresh(&running));
        assert!(!try_begin_background_refresh(&running));

        finish_background_refresh(&running);
        assert!(try_begin_background_refresh(&running));
    }

    #[test]
    fn default_automatic_refresh_interval_stays_lightweight() {
        let interval = AppSettings::default().refresh_interval();

        assert!(interval.as_secs() >= 30);
        assert!(interval.as_secs() <= 60);
    }
}
