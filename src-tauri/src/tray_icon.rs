use tauri::image::Image;
use windows::Win32::{
    Foundation::COLORREF,
    Graphics::Gdi::{
        ANTIALIASED_QUALITY, BI_RGB, BITMAPINFO, BITMAPINFOHEADER, CLIP_DEFAULT_PRECIS,
        CreateCompatibleDC, CreateDIBSection, CreateFontW, DEFAULT_CHARSET, DEFAULT_PITCH,
        DIB_RGB_COLORS, DeleteDC, DeleteObject, FF_DONTCARE, FW_NORMAL, HGDIOBJ, OUT_TT_PRECIS,
        SelectObject, SetBkMode, SetTextColor, TRANSPARENT, TextOutW,
    },
};

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
    render_system_battery_glyph(percent).unwrap_or_else(|| render_fallback_battery_icon(percent))
}

fn render_system_battery_glyph(percent: Option<u8>) -> Option<IconBitmap> {
    let color = percent.map(level_color).unwrap_or(Color(96, 96, 96, 255));
    let glyph = [battery_glyph_codepoint(percent)];
    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let mut bits = std::ptr::null_mut();
    let bitmap_info = BITMAPINFO {
        bmiHeader: BITMAPINFOHEADER {
            biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
            biWidth: WIDTH as i32,
            biHeight: -(HEIGHT as i32),
            biPlanes: 1,
            biBitCount: 32,
            biCompression: BI_RGB.0,
            biSizeImage: WIDTH * HEIGHT * 4,
            ..Default::default()
        },
        ..Default::default()
    };

    let face_name = wide_null("Segoe Fluent Icons");

    unsafe {
        let hdc = CreateCompatibleDC(None);
        if hdc.is_invalid() {
            return None;
        }

        let bitmap =
            match CreateDIBSection(Some(hdc), &bitmap_info, DIB_RGB_COLORS, &mut bits, None, 0) {
                Ok(bitmap) => bitmap,
                Err(_) => {
                    let _ = DeleteDC(hdc);
                    return None;
                }
            };

        let font = CreateFontW(
            28,
            0,
            0,
            0,
            FW_NORMAL.0 as i32,
            0,
            0,
            0,
            DEFAULT_CHARSET,
            OUT_TT_PRECIS,
            CLIP_DEFAULT_PRECIS,
            ANTIALIASED_QUALITY,
            DEFAULT_PITCH.0 as u32 | FF_DONTCARE.0 as u32,
            windows::core::PCWSTR(face_name.as_ptr()),
        );

        if font.is_invalid() {
            let _ = DeleteObject(HGDIOBJ(bitmap.0));
            let _ = DeleteDC(hdc);
            return None;
        }

        let old_bitmap = SelectObject(hdc, HGDIOBJ(bitmap.0));
        let old_font = SelectObject(hdc, HGDIOBJ(font.0));
        let _ = SetBkMode(hdc, TRANSPARENT);
        let _ = SetTextColor(
            hdc,
            COLORREF(color.0 as u32 | ((color.1 as u32) << 8) | ((color.2 as u32) << 16)),
        );
        let _ = TextOutW(hdc, 2, 2, &glyph);

        if !bits.is_null() {
            let dib = std::slice::from_raw_parts(bits as *const u8, pixels.len());
            for (source, target) in dib.chunks_exact(4).zip(pixels.chunks_exact_mut(4)) {
                let blue = source[0];
                let green = source[1];
                let red = source[2];
                let alpha = if red == 0 && green == 0 && blue == 0 {
                    0
                } else {
                    255
                };
                target[0] = red;
                target[1] = green;
                target[2] = blue;
                target[3] = alpha;
            }
        }

        let _ = SelectObject(hdc, old_font);
        let _ = SelectObject(hdc, old_bitmap);
        let _ = DeleteObject(HGDIOBJ(font.0));
        let _ = DeleteObject(HGDIOBJ(bitmap.0));
        let _ = DeleteDC(hdc);
    }

    if pixels.iter().any(|channel| *channel != 0) {
        Some(IconBitmap {
            pixels,
            width: WIDTH,
            height: HEIGHT,
        })
    } else {
        None
    }
}

fn render_fallback_battery_icon(percent: Option<u8>) -> IconBitmap {
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

fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
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

fn battery_glyph_codepoint(percent: Option<u8>) -> u16 {
    let Some(percent) = percent else {
        return 0xe83f;
    };

    let bucket = ((percent.min(100) as u16) + 5) / 10;
    if bucket >= 10 {
        0xe83f
    } else {
        0xe850 + bucket
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
    fn renders_system_battery_glyph_when_available() {
        assert!(render_system_battery_glyph(Some(48)).is_some());
    }

    #[test]
    fn renders_transparent_tray_bitmap_with_visible_battery_shape() {
        let bitmap = render_icon_bitmap(Some(48));

        assert_eq!(bitmap.width, 32);
        assert_eq!(bitmap.height, 32);
        assert!(nontransparent_pixels(&bitmap) > 20);
        assert_eq!(pixel(&bitmap, 1, 1).3, 0, "transparent background");
    }

    #[test]
    fn renders_readable_tray_battery_scale() {
        let bitmap = render_icon_bitmap(Some(48));
        let (min_x, min_y, max_x, max_y) =
            nontransparent_bounds(&bitmap).expect("visible tray icon");

        assert!(
            max_x - min_x + 1 >= 22,
            "battery glyph should use most of the tray icon width, got x bounds {min_x}..{max_x}"
        );
        assert!(
            max_y - min_y + 1 >= 12,
            "battery glyph should stay readable after tray scaling, got y bounds {min_y}..{max_y}"
        );
    }

    #[test]
    fn battery_glyph_uses_level_color() {
        let bitmap = render_icon_bitmap(Some(48));

        assert!(has_color(&bitmap, Color(16, 124, 16, 255)));
    }

    #[test]
    fn unknown_battery_uses_muted_color() {
        let bitmap = render_icon_bitmap(None);

        assert!(has_color(&bitmap, Color(96, 96, 96, 255)));
    }

    #[test]
    fn maps_percent_to_segoe_fluent_battery_glyphs() {
        assert_eq!(battery_glyph_codepoint(None), 0xe83f);
        assert_eq!(battery_glyph_codepoint(Some(0)), 0xe850);
        assert_eq!(battery_glyph_codepoint(Some(48)), 0xe855);
        assert_eq!(battery_glyph_codepoint(Some(99)), 0xe83f);
    }
}
