use crate::{
    bluetooth::RefreshResult,
    panel_position::{PanelSize, Point, WorkArea, position_near_tray_anchor},
    tray_icon,
};
use tauri::{
    App, AppHandle, Manager, PhysicalPosition, WebviewWindow,
    menu::{Menu, MenuItem},
    tray::{MouseButton, MouseButtonState, TrayIcon, TrayIconBuilder, TrayIconEvent},
};

pub fn setup(app: &mut App, refresh: fn(AppHandle)) -> tauri::Result<TrayIcon> {
    let open_item = MenuItem::with_id(app, "open", "打开面板", true, None::<&str>)?;
    let refresh_item = MenuItem::with_id(app, "refresh", "刷新", true, None::<&str>)?;
    let quit_item = MenuItem::with_id(app, "quit", "退出", true, None::<&str>)?;
    let menu = Menu::with_items(app, &[&open_item, &refresh_item, &quit_item])?;

    let click_handle = app.handle().clone();
    let refresh_handle = app.handle().clone();

    TrayIconBuilder::with_id("blue-battery")
        .icon(tray_icon::render_battery_icon(None))
        .tooltip("Blue Battery")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_tray_icon_event(move |_tray, event| {
            if let TrayIconEvent::Click {
                position,
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                show_panel(&click_handle, Some(position));
            }
        })
        .on_menu_event(move |app, event| match event.id().as_ref() {
            "open" => show_panel(app, None),
            "refresh" => refresh(refresh_handle.clone()),
            "quit" => app.exit(0),
            _ => {}
        })
        .build(app)
}

pub fn build_tooltip(result: &RefreshResult) -> String {
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

fn show_panel(app: &AppHandle, anchor: Option<PhysicalPosition<f64>>) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = position_panel_near_anchor(&window, anchor);
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

fn position_panel_near_anchor(
    window: &WebviewWindow,
    anchor: Option<PhysicalPosition<f64>>,
) -> tauri::Result<()> {
    let anchor = anchor.or_else(|| window.cursor_position().ok());
    let monitor = match anchor {
        Some(anchor) => window
            .monitor_from_point(anchor.x, anchor.y)?
            .or(window.current_monitor()?)
            .or(window.primary_monitor()?),
        None => window.current_monitor()?.or(window.primary_monitor()?),
    };
    let Some(monitor) = monitor else {
        return Ok(());
    };

    let size = window.outer_size()?;
    let work_area = monitor.work_area();
    let work_area = WorkArea {
        x: work_area.position.x,
        y: work_area.position.y,
        width: work_area.size.width,
        height: work_area.size.height,
    };
    let anchor = anchor
        .map(|anchor| Point {
            x: anchor.x.round() as i32,
            y: anchor.y.round() as i32,
        })
        .unwrap_or_else(|| Point {
            x: work_area.x + i32::try_from(work_area.width).unwrap_or(i32::MAX),
            y: work_area.y + i32::try_from(work_area.height).unwrap_or(i32::MAX),
        });
    let position = position_near_tray_anchor(
        anchor,
        PanelSize {
            width: size.width,
            height: size.height,
        },
        work_area,
    );

    window.set_position(PhysicalPosition::new(position.x, position.y))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn refresh_result(
        devices: Vec<crate::bluetooth::DeviceBatteryInfo>,
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

    fn device(display_name: &str, battery_percent: u8) -> crate::bluetooth::DeviceBatteryInfo {
        crate::bluetooth::DeviceBatteryInfo {
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
        let result = RefreshResult {
            devices: Vec::new(),
            connected_le_device_count: 1,
            refreshed_at_ms: 123,
            errors: vec!["Keyboard: HRESULT 0x80070490".to_string()],
        };

        assert_eq!(build_tooltip(&result), "Blue Battery: 读取失败，稍后重试");
    }
}
