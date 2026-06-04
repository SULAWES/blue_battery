#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::sync::Mutex;

use bluetooth::RefreshResult;
use tauri::{AppHandle, Emitter, Manager, tray::TrayIcon};

mod bluetooth;
mod tray;
mod tray_icon;

#[derive(Default)]
struct AppState {
    tray: Mutex<Option<TrayIcon>>,
    latest: Mutex<Option<RefreshResult>>,
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
            refresh_in_background(app_handle);

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

fn refresh_in_background(app: AppHandle) {
    tauri::async_runtime::spawn(async move {
        let task_result =
            tauri::async_runtime::spawn_blocking(bluetooth::read_connected_devices).await;

        let Ok(Ok(result)) = task_result else {
            return;
        };

        if apply_refresh_result(&app, &result).is_ok() {
            let _ = app.emit("devices-refreshed", result);
        }
    });
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
