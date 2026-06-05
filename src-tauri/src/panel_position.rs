const EDGE_MARGIN: i32 = 8;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct Point {
    pub x: i32,
    pub y: i32,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PanelSize {
    pub width: u32,
    pub height: u32,
}

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct WorkArea {
    pub x: i32,
    pub y: i32,
    pub width: u32,
    pub height: u32,
}

pub fn position_near_tray_anchor(anchor: Point, panel: PanelSize, work_area: WorkArea) -> Point {
    let panel_width = i32::try_from(panel.width).unwrap_or(i32::MAX);
    let panel_height = i32::try_from(panel.height).unwrap_or(i32::MAX);
    let work_right = work_area.x + i32::try_from(work_area.width).unwrap_or(i32::MAX);
    let work_bottom = work_area.y + i32::try_from(work_area.height).unwrap_or(i32::MAX);

    let min_x = work_area.x + EDGE_MARGIN;
    let min_y = work_area.y + EDGE_MARGIN;
    let max_x = work_right - EDGE_MARGIN - panel_width;
    let max_y = work_bottom - EDGE_MARGIN - panel_height;

    let raw_x = if anchor.x < work_area.x {
        min_x
    } else if anchor.x > work_right {
        max_x
    } else {
        anchor.x - panel_width / 2
    };

    let raw_y = if anchor.y < work_area.y {
        min_y
    } else if anchor.y > work_bottom {
        max_y
    } else if anchor.y < work_area.y + i32::try_from(work_area.height / 2).unwrap_or(i32::MAX) {
        min_y
    } else {
        max_y
    };

    Point {
        x: clamp_axis(raw_x, min_x, max_x),
        y: clamp_axis(raw_y, min_y, max_y),
    }
}

fn clamp_axis(value: i32, min: i32, max: i32) -> i32 {
    if max < min {
        min
    } else {
        value.clamp(min, max)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn area(x: i32, y: i32, width: u32, height: u32) -> WorkArea {
        WorkArea {
            x,
            y,
            width,
            height,
        }
    }

    fn size(width: u32, height: u32) -> PanelSize {
        PanelSize { width, height }
    }

    fn point(x: i32, y: i32) -> Point {
        Point { x, y }
    }

    #[test]
    fn positions_panel_above_bottom_tray_without_crossing_work_area() {
        let position =
            position_near_tray_anchor(point(1850, 1078), size(360, 460), area(0, 0, 1920, 1040));

        assert_eq!(position, point(1552, 572));
    }

    #[test]
    fn positions_panel_below_top_taskbar() {
        let position =
            position_near_tray_anchor(point(1850, 8), size(360, 460), area(0, 40, 1920, 1040));

        assert_eq!(position.y, 48);
        assert!(position.x >= 8);
        assert!(position.x + 360 <= 1912);
    }

    #[test]
    fn positions_panel_inside_work_area_for_left_taskbar() {
        let position =
            position_near_tray_anchor(point(12, 900), size(360, 460), area(48, 0, 1872, 1080));

        assert_eq!(position.x, 56);
        assert!(position.y >= 8);
        assert!(position.y + 460 <= 1072);
    }

    #[test]
    fn positions_panel_inside_work_area_for_right_taskbar() {
        let position =
            position_near_tray_anchor(point(1916, 900), size(360, 460), area(0, 0, 1872, 1080));

        assert_eq!(position.x, 1504);
        assert!(position.y >= 8);
        assert!(position.y + 460 <= 1072);
    }

    #[test]
    fn clamps_panel_when_anchor_is_inside_work_area_corner() {
        let position =
            position_near_tray_anchor(point(1910, 1030), size(360, 460), area(0, 0, 1920, 1040));

        assert_eq!(position, point(1552, 572));
    }
}
