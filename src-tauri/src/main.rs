#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::{
    sync::{
        Mutex,
        atomic::{AtomicBool, Ordering},
    },
    time::Duration,
};

use bluetooth::RefreshResult;
use tauri::{AppHandle, Emitter, Manager, tray::TrayIcon};

mod bluetooth;
mod panel_position;
mod tray;
mod tray_icon;

const AUTO_REFRESH_INTERVAL: Duration = Duration::from_secs(60);

#[derive(Default)]
struct AppState {
    tray: Mutex<Option<TrayIcon>>,
    latest: Mutex<Option<RefreshResult>>,
    background_refresh_running: AtomicBool,
}

#[tauri::command]
async fn refresh_devices(app: AppHandle) -> Result<RefreshResult, String> {
    let result = tauri::async_runtime::spawn_blocking(bluetooth::read_connected_devices)
        .await
        .map_err(|error| format!("Refresh task failed: {error}"))??;

    apply_refresh_result(&app, &result)?;
    Ok(result)
}

fn main() {
    tauri::Builder::default()
        .manage(AppState::default())
        .setup(|app| {
            setup_tray(app)?;

            let app_handle = app.handle().clone();
            refresh_in_background(app_handle.clone());
            start_auto_refresh(app_handle)?;

            Ok(())
        })
        .invoke_handler(tauri::generate_handler![refresh_devices])
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
        return;
    }

    tauri::async_runtime::spawn(async move {
        let task_result =
            tauri::async_runtime::spawn_blocking(bluetooth::read_connected_devices).await;

        if let Ok(Ok(result)) = task_result
            && apply_refresh_result(&app, &result).is_ok()
        {
            let _ = app.emit("devices-refreshed", result);
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
