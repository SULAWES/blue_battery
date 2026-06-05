#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    sync::{
        Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};

use bluetooth::RefreshResult;
use diagnostics::{Diagnostics, RefreshSource};
use tauri::{AppHandle, Emitter, Manager, tray::TrayIcon};

mod bluetooth;
mod diagnostics;
mod panel_position;
mod panel_window;
mod startup;
mod tray;
mod tray_icon;

const AUTO_REFRESH_INTERVAL: Duration = Duration::from_secs(60);

#[derive(Default)]
struct AppState {
    tray: Mutex<Option<TrayIcon>>,
    latest: Mutex<Option<RefreshResult>>,
    diagnostics: Mutex<Diagnostics>,
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
fn hide_panel(app: AppHandle) -> Result<(), String> {
    panel_window::hide(&app).map_err(|error| error.to_string())
}

fn main() {
    tauri::Builder::default()
        .manage(AppState::default())
        .setup(|app| {
            setup_tray(app)?;
            panel_window::register_auto_hide(app.handle())?;

            let app_handle = app.handle().clone();
            record_diagnostic_event(&app_handle, "app started");
            refresh_in_background(app_handle.clone());
            start_auto_refresh(app_handle)?;

            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            refresh_devices,
            get_diagnostics_report,
            get_startup_enabled,
            set_startup_enabled,
            hide_panel
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
                std::thread::sleep(AUTO_REFRESH_INTERVAL);
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
        tray.set_icon(Some(tray_icon::render_battery_icon(lowest)))
            .map_err(|error| format!("Failed to update tray icon: {error}"))?;
        tray.set_tooltip(Some(tray::build_tooltip(result)))
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
    fn automatic_refresh_interval_stays_lightweight() {
        assert!(AUTO_REFRESH_INTERVAL.as_secs() >= 30);
        assert!(AUTO_REFRESH_INTERVAL.as_secs() <= 60);
    }
}
