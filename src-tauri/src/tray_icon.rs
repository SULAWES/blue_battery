use tauri::image::Image;

const WIDTH: u32 = 32;
const HEIGHT: u32 = 32;
const BATMETER_BITMAP_RESOURCE: u16 = 358;
const BATMETER_LEVEL_MAX_FRAME: usize = 9;
const BATMETER_OUTLINE_FRAME: usize = 26;
const RT_BITMAP: u16 = 2;

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
        .and_then(render_system_batmeter_icon)
        .unwrap_or_else(|| render_tray_battery_icon(percent))
}

#[cfg(windows)]
fn render_system_batmeter_icon(percent: u8) -> Option<IconBitmap> {
    let strip = load_batmeter_strip()?;
    render_batmeter_frame(&strip, percent)
}

#[cfg(not(windows))]
fn render_system_batmeter_icon(_percent: u8) -> Option<IconBitmap> {
    None
}

fn batmeter_level_frame(percent: Option<u8>) -> usize {
    let Some(percent) = percent else {
        return BATMETER_OUTLINE_FRAME;
    };

    ((percent.min(100) as usize * BATMETER_LEVEL_MAX_FRAME) + 50) / 100
}

#[derive(Debug)]
struct BatmeterStrip {
    pixels: Vec<Color>,
    width: u32,
    height: u32,
}

fn render_batmeter_frame(strip: &BatmeterStrip, percent: u8) -> Option<IconBitmap> {
    if strip.height != HEIGHT || strip.width < WIDTH * (BATMETER_OUTLINE_FRAME as u32 + 1) {
        return None;
    }

    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let level_frame = batmeter_level_frame(Some(percent));
    let fill = level_color(percent);
    let stroke = Color(24, 24, 24, 255);

    for y in 0..HEIGHT {
        for x in 0..WIDTH {
            let outline = batmeter_light_pixel(strip, BATMETER_OUTLINE_FRAME, x, y)?;
            let level = batmeter_light_pixel(strip, level_frame, x, y)?;

            if outline {
                set_pixel(&mut pixels, x, y, stroke);
            } else if level {
                set_pixel(&mut pixels, x, y, fill);
            }
        }
    }

    Some(IconBitmap {
        pixels,
        width: WIDTH,
        height: HEIGHT,
    })
}

fn batmeter_light_pixel(strip: &BatmeterStrip, frame: usize, x: u32, y: u32) -> Option<bool> {
    let source_x = frame as u32 * WIDTH + x;
    let index = (y * strip.width + source_x) as usize;
    let Color(red, green, blue, _) = *strip.pixels.get(index)?;

    Some(red > 220 && green > 220 && blue > 220)
}

#[cfg(windows)]
fn load_batmeter_strip() -> Option<BatmeterStrip> {
    use std::{env, path::PathBuf};
    use windows::{
        Win32::{
            Foundation::FreeLibrary,
            System::LibraryLoader::{
                FindResourceW, LOAD_LIBRARY_AS_DATAFILE, LoadLibraryExW, LoadResource,
                LockResource, SizeofResource,
            },
        },
        core::PCWSTR,
    };

    let windir = env::var_os("WINDIR").map(PathBuf::from)?;
    let resource_path = windir.join("SystemResources").join("batmeter.dll.mun");
    let resource_path = wide_null(resource_path.to_string_lossy().as_ref());

    unsafe {
        let module = LoadLibraryExW(
            PCWSTR(resource_path.as_ptr()),
            None,
            LOAD_LIBRARY_AS_DATAFILE,
        )
        .ok()?;
        let resource = FindResourceW(
            Some(module),
            int_resource(BATMETER_BITMAP_RESOURCE),
            int_resource(RT_BITMAP),
        );

        let strip = if resource.is_invalid() {
            None
        } else {
            match LoadResource(Some(module), resource) {
                Ok(loaded) => {
                    let data = LockResource(loaded);
                    let size = SizeofResource(Some(module), resource) as usize;

                    if data.is_null() || size == 0 {
                        None
                    } else {
                        let bytes = std::slice::from_raw_parts(data as *const u8, size);
                        parse_batmeter_dib(bytes)
                    }
                }
                Err(_) => None,
            }
        };

        let _ = FreeLibrary(module);
        strip
    }
}

#[cfg(windows)]
fn int_resource(value: u16) -> windows::core::PCWSTR {
    windows::core::PCWSTR(value as usize as *const u16)
}

#[cfg(windows)]
fn wide_null(value: &str) -> Vec<u16> {
    value.encode_utf16().chain(std::iter::once(0)).collect()
}

fn parse_batmeter_dib(bytes: &[u8]) -> Option<BatmeterStrip> {
    if bytes.len() < 40 {
        return None;
    }

    let header_size = u32::from_le_bytes(bytes.get(0..4)?.try_into().ok()?);
    let width = i32::from_le_bytes(bytes.get(4..8)?.try_into().ok()?);
    let height = i32::from_le_bytes(bytes.get(8..12)?.try_into().ok()?);
    let bit_count = u16::from_le_bytes(bytes.get(14..16)?.try_into().ok()?);
    let compression = u32::from_le_bytes(bytes.get(16..20)?.try_into().ok()?);

    if header_size < 40 || width <= 0 || height == 0 || bit_count != 32 || compression != 0 {
        return None;
    }

    let width = width as u32;
    let height_abs = height.unsigned_abs();
    let row_stride = width.checked_mul(4)?;
    let pixel_offset = header_size as usize;
    let expected_size = pixel_offset.checked_add(row_stride.checked_mul(height_abs)? as usize)?;

    if bytes.len() < expected_size {
        return None;
    }

    let mut pixels = Vec::with_capacity((width * height_abs) as usize);
    for y in 0..height_abs {
        let source_y = if height > 0 { height_abs - 1 - y } else { y };
        let row = pixel_offset + (source_y * row_stride) as usize;

        for x in 0..width {
            let index = row + (x * 4) as usize;
            pixels.push(Color(
                bytes[index + 2],
                bytes[index + 1],
                bytes[index],
                bytes[index + 3],
            ));
        }
    }

    Some(BatmeterStrip {
        pixels,
        width,
        height: height_abs,
    })
}

fn render_tray_battery_icon(percent: Option<u8>) -> IconBitmap {
    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let stroke = Color(24, 24, 24, 255);
    let halo = Color(255, 255, 255, 210);
    let muted = Color(96, 96, 96, 255);
    let fill = percent.map(level_color).unwrap_or(muted);

    draw_battery_halo(&mut pixels, halo);
    draw_battery_outline(&mut pixels, stroke);

    if let Some(percent) = percent {
        let fill_width = ((18u32 * percent as u32) + 99) / 100;
        if fill_width > 0 {
            draw_rect(&mut pixels, 7, 13, fill_width.min(18), 6, fill);
        }
    } else {
        draw_rect(&mut pixels, 8, 15, 16, 3, muted);
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

fn draw_battery_halo(pixels: &mut [u8], color: Color) {
    draw_rect(pixels, 1, 6, 28, 20, color);
    draw_rect(pixels, 28, 11, 3, 10, color);
}

fn draw_battery_outline(pixels: &mut [u8], color: Color) {
    draw_rect(pixels, 3, 8, 24, 2, color);
    draw_rect(pixels, 3, 22, 24, 2, color);
    draw_rect(pixels, 3, 8, 2, 16, color);
    draw_rect(pixels, 25, 8, 2, 16, color);
    draw_rect(pixels, 27, 13, 3, 6, color);
    draw_rect(pixels, 6, 12, 1, 8, color);
    draw_rect(pixels, 24, 12, 1, 8, color);
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
            max_x - min_x + 1 >= 26,
            "battery icon should use most of the tray icon width, got x bounds {min_x}..{max_x}"
        );
        assert!(
            max_y - min_y + 1 >= 16,
            "battery icon should stay readable after tray scaling, got y bounds {min_y}..{max_y}"
        );
        assert!(
            nontransparent_pixels(&bitmap) >= 140,
            "battery icon should have enough visible mass for the notification area"
        );
    }

    #[test]
    fn maps_battery_percent_to_batmeter_frame() {
        assert_eq!(batmeter_level_frame(Some(100)), 9);
        assert_eq!(batmeter_level_frame(Some(48)), 4);
        assert_eq!(batmeter_level_frame(Some(0)), 0);
        assert_eq!(batmeter_level_frame(None), BATMETER_OUTLINE_FRAME);
    }

    #[cfg(windows)]
    #[test]
    fn renders_system_batmeter_icon_when_available() {
        let bitmap = render_system_batmeter_icon(48).expect("batmeter resource icon");

        assert_eq!(bitmap.width, 32);
        assert_eq!(bitmap.height, 32);
        assert_eq!(pixel(&bitmap, 0, 0).3, 0, "transparent background");
        assert!(has_color(&bitmap, Color(24, 24, 24, 255)));
        assert!(has_color(&bitmap, Color(16, 124, 16, 255)));
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
