use tauri::image::Image;

const WIDTH: u32 = 32;
const HEIGHT: u32 = 32;

#[derive(Clone, Copy)]
struct Color(u8, u8, u8, u8);

pub fn render_battery_icon(percent: Option<u8>) -> Image<'static> {
    let mut pixels = vec![0; (WIDTH * HEIGHT * 4) as usize];
    let outline = Color(244, 248, 252, 242);
    let shadow = Color(8, 15, 24, 130);
    let muted = Color(112, 122, 132, 230);
    let fill = percent.map(level_color).unwrap_or(muted);

    draw_rect(&mut pixels, 4, 9, 23, 15, shadow);
    draw_rect(&mut pixels, 6, 8, 21, 15, outline);
    draw_rect(&mut pixels, 8, 10, 17, 11, Color(20, 30, 42, 170));
    draw_rect(&mut pixels, 27, 12, 3, 7, outline);
    draw_rect(&mut pixels, 28, 14, 1, 3, Color(20, 30, 42, 170));

    if let Some(percent) = percent {
        let fill_width = ((15u32 * percent as u32) + 99) / 100;
        if fill_width > 0 {
            draw_rect(&mut pixels, 9, 11, fill_width.min(15), 9, fill);
        }
    } else {
        draw_rect(&mut pixels, 10, 14, 13, 3, muted);
    }

    Image::new_owned(pixels, WIDTH, HEIGHT)
}

fn level_color(percent: u8) -> Color {
    if percent <= 20 {
        Color(232, 74, 69, 245)
    } else if percent <= 50 {
        Color(240, 164, 42, 245)
    } else {
        Color(35, 188, 122, 245)
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
