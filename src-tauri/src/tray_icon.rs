use tauri::image::Image;

mod fluent_icons {
    include!(concat!(env!("OUT_DIR"), "/fluent_battery_icons.rs"));
}

const WIDTH: u32 = fluent_icons::TRAY_ICON_SIZE;
const HEIGHT: u32 = fluent_icons::TRAY_ICON_SIZE;

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
    percent
        .and_then(render_fluent_battery_icon)
        .unwrap_or_else(|| render_numeric_fallback_icon(percent))
}

fn render_fluent_battery_icon(percent: u8) -> Option<IconBitmap> {
    let pixels = fluent_icons::fluent_battery_icon_rgba(
        fluent_battery_asset_index(percent),
        fluent_color_index(percent),
    )?;

    Some(IconBitmap {
        pixels: pixels.to_vec(),
        width: WIDTH,
        height: HEIGHT,
    })
}

fn fluent_battery_asset_index(percent: u8) -> usize {
    ((percent.min(100) as usize + 5) / 10).min(10)
}

fn fluent_color_index(percent: u8) -> usize {
    if percent <= 20 {
        fluent_icons::COLOR_LOW
    } else if percent <= 35 {
        fluent_icons::COLOR_MEDIUM
    } else {
        fluent_icons::COLOR_NORMAL
    }
}

fn render_numeric_fallback_icon(percent: Option<u8>) -> IconBitmap {
    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let muted = Color(96, 96, 96, 255);
    let text = percent
        .map(|percent| percent.min(100).to_string())
        .unwrap_or_else(|| "--".to_string());
    let scale = if text.len() >= 3 { 3 } else { 4 };
    let color = percent.map(level_color).unwrap_or(muted);

    draw_pixel_text(&mut pixels, &text, scale, color);

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

fn draw_pixel_text(pixels: &mut [u8], text: &str, scale: u32, color: Color) {
    let glyph_width = 3;
    let glyph_height = 5;
    let gap = 2;
    let char_count = text.chars().count() as u32;
    let text_width = char_count * glyph_width * scale + char_count.saturating_sub(1) * gap;
    let text_height = glyph_height * scale;
    let start_x = WIDTH.saturating_sub(text_width) / 2;
    let start_y = HEIGHT.saturating_sub(text_height) / 2;
    let mut x = start_x;

    for ch in text.chars() {
        draw_digit_glyph(pixels, ch, x, start_y, scale, color);
        x += glyph_width * scale + gap;
    }
}

fn draw_digit_glyph(pixels: &mut [u8], ch: char, x: u32, y: u32, scale: u32, color: Color) {
    let pattern = digit_pattern(ch);

    for (row, bits) in pattern.iter().enumerate() {
        for col in 0..3 {
            if bits & (1 << (2 - col)) != 0 {
                draw_rect(
                    pixels,
                    x + col * scale,
                    y + row as u32 * scale,
                    scale,
                    scale,
                    color,
                );
            }
        }
    }
}

fn digit_pattern(ch: char) -> [u8; 5] {
    match ch {
        '0' => [0b111, 0b101, 0b101, 0b101, 0b111],
        '1' => [0b010, 0b110, 0b010, 0b010, 0b111],
        '2' => [0b111, 0b001, 0b111, 0b100, 0b111],
        '3' => [0b111, 0b001, 0b111, 0b001, 0b111],
        '4' => [0b101, 0b101, 0b111, 0b001, 0b001],
        '5' => [0b111, 0b100, 0b111, 0b001, 0b111],
        '6' => [0b111, 0b100, 0b111, 0b101, 0b111],
        '7' => [0b111, 0b001, 0b010, 0b010, 0b010],
        '8' => [0b111, 0b101, 0b111, 0b101, 0b111],
        '9' => [0b111, 0b101, 0b111, 0b001, 0b111],
        '-' => [0b000, 0b000, 0b111, 0b000, 0b000],
        _ => [0b111, 0b001, 0b011, 0b000, 0b010],
    }
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

    fn nontransparent_pixels(bitmap: &IconBitmap) -> usize {
        bitmap
            .pixels
            .chunks_exact(4)
            .filter(|pixel| pixel[3] > 0)
            .count()
    }

    fn nontransparent_bounds(bitmap: &IconBitmap) -> Option<(u32, u32, u32, u32)> {
        let mut min_x = bitmap.width;
        let mut min_y = bitmap.height;
        let mut max_x = 0;
        let mut max_y = 0;
        let mut found = false;

        for y in 0..bitmap.height {
            for x in 0..bitmap.width {
                if pixel(bitmap, x, y).3 > 0 {
                    found = true;
                    min_x = min_x.min(x);
                    min_y = min_y.min(y);
                    max_x = max_x.max(x);
                    max_y = max_y.max(y);
                }
            }
        }

        found.then_some((min_x, min_y, max_x, max_y))
    }

    fn has_color(bitmap: &IconBitmap, Color(red, green, blue, _alpha): Color) -> bool {
        bitmap
            .pixels
            .chunks_exact(4)
            .any(|pixel| pixel[0] == red && pixel[1] == green && pixel[2] == blue && pixel[3] > 0)
    }

    #[test]
    fn renders_transparent_tray_bitmap_with_visible_battery_shape() {
        let bitmap = render_icon_bitmap(Some(48));

        assert_eq!(bitmap.width, 40);
        assert_eq!(bitmap.height, 40);
        assert!(nontransparent_pixels(&bitmap) > 20);
        assert_eq!(pixel(&bitmap, 1, 1).3, 0, "transparent background");
    }

    #[test]
    fn renders_readable_tray_battery_scale() {
        let bitmap = render_icon_bitmap(Some(48));
        let (min_x, min_y, max_x, max_y) =
            nontransparent_bounds(&bitmap).expect("visible tray icon");

        assert!(
            max_x - min_x + 1 >= 32,
            "battery icon should use most of the tray icon width, got x bounds {min_x}..{max_x}"
        );
        assert!(
            max_y - min_y + 1 >= 20,
            "battery icon should stay readable after tray scaling, got y bounds {min_y}..{max_y}"
        );
        assert!(
            nontransparent_pixels(&bitmap) >= 140,
            "battery icon should have enough visible mass for the notification area"
        );
    }

    #[test]
    fn maps_battery_percent_to_fluent_asset_index() {
        assert_eq!(fluent_battery_asset_index(0), 0);
        assert_eq!(fluent_battery_asset_index(5), 1);
        assert_eq!(fluent_battery_asset_index(48), 5);
        assert_eq!(fluent_battery_asset_index(95), 10);
        assert_eq!(fluent_battery_asset_index(100), 10);
    }

    #[test]
    fn renders_fluent_battery_icon_with_level_color() {
        let bitmap = render_fluent_battery_icon(48).expect("fluent battery icon");
        let (min_x, min_y, max_x, max_y) =
            nontransparent_bounds(&bitmap).expect("visible fluent tray icon");

        assert_eq!(bitmap.width, 40);
        assert_eq!(bitmap.height, 40);
        assert_eq!(pixel(&bitmap, 0, 0).3, 0, "transparent background");
        assert!(max_x - min_x + 1 >= 32, "icon should fill tray width");
        assert!(max_y - min_y + 1 >= 20, "icon should fill tray height");
        assert!(has_color(&bitmap, Color(16, 124, 16, 255)));
    }

    #[test]
    fn renders_numeric_fallback_percent() {
        let bitmap = render_numeric_fallback_icon(Some(48));
        let (min_x, min_y, max_x, max_y) =
            nontransparent_bounds(&bitmap).expect("visible fallback number");

        assert_eq!(bitmap.width, 40);
        assert_eq!(bitmap.height, 40);
        assert!(
            max_x - min_x + 1 >= 15,
            "numeric fallback should be readable"
        );
        assert!(
            max_y - min_y + 1 >= 17,
            "numeric fallback should fill tray height"
        );
        assert!(has_color(&bitmap, Color(16, 124, 16, 255)));
    }

    #[test]
    fn renders_numeric_fallback_placeholder_for_unknown_battery() {
        let bitmap = render_numeric_fallback_icon(None);

        assert!(has_color(&bitmap, Color(96, 96, 96, 255)));
        assert_eq!(pixel(&bitmap, 1, 1).3, 0, "transparent background");
    }

    #[test]
    fn battery_icon_uses_level_color() {
        let bitmap = render_icon_bitmap(Some(48));

        assert!(has_color(&bitmap, Color(16, 124, 16, 255)));
    }

    #[test]
    fn unknown_battery_uses_muted_color() {
        let bitmap = render_icon_bitmap(None);

        assert!(has_color(&bitmap, Color(96, 96, 96, 255)));
    }
}
