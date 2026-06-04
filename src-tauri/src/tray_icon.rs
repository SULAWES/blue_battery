use tauri::image::Image;

const WIDTH: u32 = 32;
const HEIGHT: u32 = 32;

#[derive(Clone, Copy, Debug, Eq, PartialEq)]
struct Color(u8, u8, u8, u8);

struct IconBitmap {
    pixels: Vec<u8>,
    width: u32,
    height: u32,
}

pub fn render_battery_icon(percent: Option<u8>) -> Image<'static> {
    let bitmap = render_icon_bitmap(percent);
    Image::new_owned(bitmap.pixels, bitmap.width, bitmap.height)
}

fn render_icon_bitmap(percent: Option<u8>) -> IconBitmap {
    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let stroke = Color(32, 32, 32, 255);
    let halo = Color(255, 255, 255, 210);
    let muted = Color(96, 96, 96, 255);
    let fill = percent.map(level_color).unwrap_or(muted);

    draw_power_plug(&mut pixels, stroke);
    draw_battery_halo(&mut pixels, halo);
    draw_battery_outline(&mut pixels, stroke);

    if let Some(percent) = percent {
        let fill_width = ((14u32 * percent as u32) + 99) / 100;
        if fill_width > 0 {
            draw_rect(&mut pixels, 9, 15, fill_width.min(14), 5, fill);
        }
    } else {
        draw_rect(&mut pixels, 11, 16, 10, 2, muted);
    }

    IconBitmap {
        pixels,
        width: WIDTH,
        height: HEIGHT,
    }
}

fn level_color(percent: u8) -> Color {
    if percent <= 20 {
        Color(196, 43, 28, 255)
    } else if percent <= 35 {
        Color(202, 80, 16, 255)
    } else {
        Color(16, 124, 16, 255)
    }
}

fn draw_power_plug(pixels: &mut [u8], color: Color) {
    draw_rect(pixels, 28, 9, 1, 7, color);
    draw_rect(pixels, 25, 9, 4, 1, color);
    draw_rect(pixels, 25, 7, 1, 3, color);
    draw_rect(pixels, 27, 7, 1, 3, color);
}

fn draw_battery_halo(pixels: &mut [u8], color: Color) {
    draw_rect(pixels, 4, 12, 22, 12, color);
    draw_rect(pixels, 25, 15, 4, 5, color);
}

fn draw_battery_outline(pixels: &mut [u8], color: Color) {
    draw_rect(pixels, 6, 13, 19, 1, color);
    draw_rect(pixels, 6, 22, 19, 1, color);
    draw_rect(pixels, 6, 13, 1, 10, color);
    draw_rect(pixels, 24, 13, 1, 10, color);
    draw_rect(pixels, 25, 15, 3, 5, color);
    draw_rect(pixels, 8, 15, 1, 5, color);
    draw_rect(pixels, 23, 15, 1, 5, color);
}

fn draw_rect(pixels: &mut [u8], x: u32, y: u32, width: u32, height: u32, color: Color) {
    for row in y..(y + height).min(HEIGHT) {
        for col in x..(x + width).min(WIDTH) {
            set_pixel(pixels, col, row, color);
        }
    }
}

fn set_pixel(pixels: &mut [u8], x: u32, y: u32, Color(r, g, b, a): Color) {
    let index = ((y * WIDTH + x) * 4) as usize;
    pixels[index] = r;
    pixels[index + 1] = g;
    pixels[index + 2] = b;
    pixels[index + 3] = a;
}

#[cfg(test)]
mod tests {
    use super::*;

    fn pixel(bitmap: &IconBitmap, x: u32, y: u32) -> Color {
        let index = ((y * bitmap.width + x) * 4) as usize;
        Color(
            bitmap.pixels[index],
            bitmap.pixels[index + 1],
            bitmap.pixels[index + 2],
            bitmap.pixels[index + 3],
        )
    }

    #[test]
    fn renders_windows_power_icon_silhouette() {
        let bitmap = render_icon_bitmap(Some(48));

        assert_eq!(bitmap.width, 32);
        assert_eq!(bitmap.height, 32);
        assert!(pixel(&bitmap, 6, 14).3 > 200, "left battery outline");
        assert!(pixel(&bitmap, 25, 15).3 > 200, "right battery terminal");
        assert!(pixel(&bitmap, 28, 9).3 > 200, "plug stem");
        assert_eq!(pixel(&bitmap, 1, 1).3, 0, "transparent background");
    }

    #[test]
    fn battery_fill_tracks_percent_without_overfilling() {
        let bitmap = render_icon_bitmap(Some(48));

        assert_eq!(pixel(&bitmap, 10, 15), Color(16, 124, 16, 255));
        assert_ne!(pixel(&bitmap, 21, 15), Color(16, 124, 16, 255));
    }

    #[test]
    fn unknown_battery_uses_muted_center_dash() {
        let bitmap = render_icon_bitmap(None);

        assert_eq!(pixel(&bitmap, 15, 16), Color(96, 96, 96, 255));
    }
}
