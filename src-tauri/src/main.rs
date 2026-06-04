#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::sync::Mutex;

use bluetooth::RefreshResult;
use tauri::{
    AppHandle, Emitter, Manager,
    menu::{Menu, MenuItem},
    tray::{MouseButton, MouseButtonState, TrayIcon, TrayIconBuilder, TrayIconEvent},
};

mod bluetooth;
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
    let open_item = MenuItem::with_id(app, "open", "打开面板", true, None::<&str>)?;
    let refresh_item = MenuItem::with_id(app, "refresh", "刷新", true, None::<&str>)?;
    let quit_item = MenuItem::with_id(app, "quit", "退出", true, None::<&str>)?;
    let menu = Menu::with_items(app, &[&open_item, &refresh_item, &quit_item])?;

    let click_handle = app.handle().clone();
    let menu_handle = app.handle().clone();

    let tray = TrayIconBuilder::with_id("blue-battery")
        .icon(tray_icon::render_battery_icon(None))
        .tooltip("Blue Battery")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_tray_icon_event(move |_tray, event| {
            if matches!(
                event,
                TrayIconEvent::Click {
                    button: MouseButton::Left,
                    button_state: MouseButtonState::Up,
                    ..
                }
            ) {
                show_panel(&click_handle);
            }
        })
        .on_menu_event(move |app, event| match event.id().as_ref() {
            "open" => show_panel(app),
            "refresh" => refresh_in_background(menu_handle.clone()),
            "quit" => app.exit(0),
            _ => {}
        })
        .build(app)?;

    let state = app.state::<AppState>();
    *state
        .tray
        .lock()
        .map_err(|_| tauri::Error::Anyhow(anyhow::anyhow!("tray state lock poisoned")))? =
        Some(tray);

    Ok(())
}

fn show_panel(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
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
        tray.set_tooltip(Some(build_tooltip(result)))
            .map_err(|error| format!("Failed to update tray tooltip: {error}"))?;
    }

    Ok(())
}

fn build_tooltip(result: &RefreshResult) -> String {
    if result.devices.is_empty() {
        if !result.errors.is_empty() {
            return "Blue Battery: 读取失败，稍后重试".to_string();
        }

        return if result.connected_le_device_count == 0 {
            "Blue Battery: 没有已连接 BLE 设备".to_string()
        } else {
            format!(
                "Blue Battery: {} 个已连接 BLE 设备，没有标准电量",
                result.connected_le_device_count
            )
        };
    }

    let summary = result
        .devices
        .iter()
        .map(|device| format!("{}: {}%", device.display_name, device.battery_percent))
        .collect::<Vec<_>>()
        .join(" · ");

    format!("Blue Battery: {summary}")
}

#[cfg(test)]
mod tests {
    use super::*;

    fn refresh_result(
        devices: Vec<bluetooth::DeviceBatteryInfo>,
        connected_le_device_count: u32,
        errors: Vec<String>,
    ) -> RefreshResult {
        RefreshResult {
            devices,
            connected_le_device_count,
            refreshed_at_ms: 123,
            errors,
        }
    }

    fn device(display_name: &str, battery_percent: u8) -> bluetooth::DeviceBatteryInfo {
        bluetooth::DeviceBatteryInfo {
            device_id: format!("id-{display_name}"),
            display_name: display_name.to_string(),
            battery_percent,
            connection_state: "已连接".to_string(),
            source_kind: "GATT BAS".to_string(),
            updated_at_ms: 123,
        }
    }

    #[test]
    fn build_tooltip_reports_no_connected_devices() {
        let result = refresh_result(Vec::new(), 0, Vec::new());

        assert_eq!(build_tooltip(&result), "Blue Battery: 没有已连接 BLE 设备");
    }

    #[test]
    fn build_tooltip_reports_connected_devices_without_standard_battery() {
        let result = refresh_result(Vec::new(), 2, Vec::new());

        assert_eq!(
            build_tooltip(&result),
            "Blue Battery: 2 个已连接 BLE 设备，没有标准电量"
        );
    }

    #[test]
    fn build_tooltip_reports_device_battery_summary() {
        let result = refresh_result(vec![device("Keychron Z6 Ultra 8K", 48)], 1, Vec::new());

        assert_eq!(
            build_tooltip(&result),
            "Blue Battery: Keychron Z6 Ultra 8K: 48%"
        );
    }

    #[test]
    fn build_tooltip_reports_read_errors_when_no_devices_are_displayable() {
        let result = refresh_result(
            Vec::new(),
            1,
            vec!["Keyboard: HRESULT 0x80070490".to_string()],
        );

        assert_eq!(build_tooltip(&result), "Blue Battery: 读取失败，稍后重试");
    }
}
