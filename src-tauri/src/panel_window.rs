use std::time::Duration;

use crate::panel_position::{PanelSize, Point, WorkArea, position_near_tray_anchor};
use tauri::{AppHandle, Manager, PhysicalPosition, WebviewWindow, WindowEvent};

const AUTO_HIDE_DELAY: Duration = Duration::from_millis(120);

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
enum PanelToggleAction {
    Show,
    Hide,
}

pub fn register_auto_hide(app: &AppHandle) -> tauri::Result<()> {
    let Some(window) = app.get_webview_window("main") else {
        return Ok(());
    };

    let panel = window.clone();
    window.on_window_event(move |event| {
        if matches!(event, WindowEvent::Focused(false)) {
            hide_after_focus_loss(panel.clone());
        }
    });

    Ok(())
}

pub fn toggle(app: &AppHandle, anchor: Option<PhysicalPosition<f64>>) -> tauri::Result<()> {
    let Some(window) = app.get_webview_window("main") else {
        return Ok(());
    };

    match toggle_action_for_visibility(window.is_visible().unwrap_or(false)) {
        PanelToggleAction::Show => show_window(&window, anchor),
        PanelToggleAction::Hide => window.hide(),
    }
}

pub fn show(app: &AppHandle, anchor: Option<PhysicalPosition<f64>>) -> tauri::Result<()> {
    let Some(window) = app.get_webview_window("main") else {
        return Ok(());
    };

    show_window(&window, anchor)
}

pub fn hide(app: &AppHandle) -> tauri::Result<()> {
    let Some(window) = app.get_webview_window("main") else {
        return Ok(());
    };

    window.hide()
}

fn hide_after_focus_loss(window: WebviewWindow) {
    std::thread::spawn(move || {
        std::thread::sleep(AUTO_HIDE_DELAY);
        if !window.is_focused().unwrap_or(false) {
            let _ = window.hide();
        }
    });
}

fn toggle_action_for_visibility(visible: bool) -> PanelToggleAction {
    if visible {
        PanelToggleAction::Hide
    } else {
        PanelToggleAction::Show
    }
}

fn show_window(window: &WebviewWindow, anchor: Option<PhysicalPosition<f64>>) -> tauri::Result<()> {
    position_panel_near_anchor(window, anchor)?;
    window.show()?;
    window.unminimize()?;
    window.set_focus()
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

    #[test]
    fn toggle_action_hides_a_visible_panel() {
        assert_eq!(toggle_action_for_visibility(true), PanelToggleAction::Hide);
    }

    #[test]
    fn toggle_action_shows_a_hidden_panel() {
        assert_eq!(toggle_action_for_visibility(false), PanelToggleAction::Show);
    }
}
