#!/usr/bin/env python3
"""Generates every raster asset for the game.

Run from the repository root:

    python3 tools/generate_assets.py

Outputs:
    src/RugbyLeague.Game/Content/Textures/*.png      in-game textures
    src/RugbyLeague.Android/Resources/...             launcher icons and splash
    src/RugbyLeague.iOS/Assets.xcassets/...           app icon and launch image
    docs/images/*.png                                 documentation screenshots of the art

The art is deliberately simple, high-contrast and readable on a phone at arm's length.
"""
from __future__ import annotations

import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
CONTENT = os.path.join(ROOT, "src", "RugbyLeague.Game", "Content", "Textures")
ANDROID = os.path.join(ROOT, "src", "RugbyLeague.Android", "Resources")
IOS = os.path.join(ROOT, "src", "RugbyLeague.iOS", "Assets.xcassets")
DOCS = os.path.join(ROOT, "docs", "images")

NAVY = (11, 26, 38, 255)
DEEP_GREEN = (13, 61, 46, 255)
GRASS = (38, 104, 62, 255)
RED = (202, 52, 44, 255)
GOLD = (233, 184, 78, 255)
WHITE = (255, 255, 255, 255)
CREAM = (244, 240, 228, 255)

FONT_CANDIDATES = [
    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
    "/System/Library/Fonts/Supplemental/Arial.ttf",
    "/Library/Fonts/Arial.ttf",
]
FONT_REGULAR = "/System/Library/Fonts/Supplemental/Arial.ttf"


def font(size: int) -> ImageFont.FreeTypeFont:
    for path in FONT_CANDIDATES:
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def regular_font(size: int) -> ImageFont.FreeTypeFont:
    if os.path.exists(FONT_REGULAR):
        return ImageFont.truetype(FONT_REGULAR, size)
    return font(size)


def ensure(path: str) -> None:
    os.makedirs(path, exist_ok=True)


def vertical_gradient(size, top, bottom) -> Image.Image:
    width, height = size
    image = Image.new("RGB", (1, height))
    draw = ImageDraw.Draw(image)
    for y in range(height):
        t = y / max(1, height - 1)
        draw.point((0, y), tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return image.resize((width, height)).convert("RGBA")


def radial_glow(size, colour, strength=120) -> Image.Image:
    width, height = size
    glow = Image.new("L", size, 0)
    draw = ImageDraw.Draw(glow)
    steps = 48
    for i in range(steps, 0, -1):
        radius = int(min(width, height) * 0.55 * i / steps)
        alpha = int(strength * (1 - i / steps) ** 1.6)
        draw.ellipse(
            (width // 2 - radius, height // 2 - radius, width // 2 + radius, height // 2 + radius),
            fill=alpha,
        )
    glow = glow.filter(ImageFilter.GaussianBlur(28))
    layer = Image.new("RGBA", size, colour[:3] + (0,))
    layer.putalpha(glow)
    return layer


def shield_polygon(size, inset=0.10):
    """Points for a heraldic shield: straight shoulders and a rounded point."""
    width, height = size
    margin_x = width * inset
    top = height * inset
    shoulder = height * 0.44
    tip = height * (1 - inset * 0.8)

    points = [
        (margin_x, top),
        (width - margin_x, top),
        (width - margin_x, shoulder),
    ]
    for i in range(1, 33):
        t = i / 32
        x = width / 2 + (width / 2 - margin_x) * math.cos(t * math.pi / 2)
        y = shoulder + (tip - shoulder) * math.sin(t * math.pi / 2) ** 0.72
        points.append((x, y))
    for i in range(32, 0, -1):
        t = i / 32
        x = width / 2 - (width / 2 - margin_x) * math.cos(t * math.pi / 2)
        y = shoulder + (tip - shoulder) * math.sin(t * math.pi / 2) ** 0.72
        points.append((x, y))
    points.append((margin_x, shoulder))
    return points


def shield_mask(size, inset=0.06) -> Image.Image:
    """A heraldic shield silhouette used for the logo and club crests."""
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).polygon(shield_polygon(size, inset), fill=255)
    return mask


def draw_ball(draw: ImageDraw.ImageDraw, box, fill=CREAM, seam=(58, 42, 32, 255), width=4):
    draw.ellipse(box, fill=fill, outline=seam, width=width)
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    rx, ry = (x1 - x0) / 2, (y1 - y0) / 2
    draw.line([(cx - rx * 0.55, cy), (cx + rx * 0.55, cy)], fill=seam, width=width)
    for offset in (-0.18, 0.18):
        draw.arc(
            [cx - rx * 0.7, cy - ry * 0.55 + ry * offset, cx + rx * 0.7, cy + ry * 0.55 + ry * offset],
            200, 340, fill=seam, width=max(2, width - 1)
        )


def logo_image(size=1024, with_text=False, transparent=False) -> Image.Image:
    scale = size / 1024
    background = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    if not transparent:
        background = vertical_gradient((size, size), (9, 33, 29), (7, 17, 26))
        background.alpha_composite(radial_glow((size, size), (46, 160, 110, 255), 130))

    draw = ImageDraw.Draw(background)
    points = shield_polygon((size, size), inset=0.10)
    draw.polygon(points, fill=DEEP_GREEN)
    draw.line(points + [points[0]], fill=GOLD, width=max(4, int(16 * scale)), joint="curve")

    # Monogram and ball, stacked cleanly inside the shield.
    draw.text((size * 0.5, size * 0.35), "RL", font=font(int(210 * scale)), fill=WHITE, anchor="mm")
    draw_ball(
        draw,
        (size * 0.335, size * 0.56, size * 0.665, size * 0.76),
        fill=CREAM,
        width=max(3, int(9 * scale)),
    )

    if with_text:
        sub = regular_font(int(52 * scale))
        draw.text((size * 0.5, size * 0.88), "RUGBY LEAGUE", font=sub, fill=(214, 226, 214, 255), anchor="mm")

    return background


def splash_image(width=1920, height=1080) -> Image.Image:
    image = vertical_gradient((width, height), (8, 28, 26), (5, 12, 20))
    image.alpha_composite(radial_glow((width, height), (36, 138, 96, 255), 110))

    draw = ImageDraw.Draw(image)
    # Subtle pitch lines in the background.
    for i in range(1, 12):
        x = width * i / 12
        draw.line([(x, 0), (x, height)], fill=(255, 255, 255, 10), width=2)
    draw.line([(0, height * 0.70), (width, height * 0.70)], fill=(255, 255, 255, 18), width=3)

    logo_px = int(height * 0.44)
    logo = logo_image(logo_px, transparent=True)
    image.alpha_composite(logo, (int(width * 0.5 - logo_px / 2), int(height * 0.045)))

    draw.text((width / 2, height * 0.60), "RUGBY LEAGUE", font=font(int(height * 0.115)), fill=WHITE, anchor="mm")
    draw.text((width / 2, height * 0.70), "CLUB MANAGER  ·  SIMULATION & 3D",
              font=regular_font(int(height * 0.037)), fill=GOLD, anchor="mm")
    draw.text((width / 2, height * 0.80), "NRL  ·  SUPER LEAGUE  ·  THE CHAMPIONSHIP",
              font=regular_font(int(height * 0.028)), fill=(170, 190, 178, 255), anchor="mm")
    return image


def ui_panel(radius=26, size=96, base=(255, 255, 255, 255)) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=base)
    return image


def ui_button(size=96) -> Image.Image:
    image = ui_panel(24, size, (255, 255, 255, 255))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=24, outline=(255, 255, 255, 170), width=2)
    return image


def ball_texture(size=128) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw_ball(draw, (4, size * 0.22, size - 4, size * 0.78), width=4)
    return image


def marker_texture(size=64) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((3, 3, size - 4, size - 4), fill=(255, 255, 255, 255))
    draw.ellipse((3, 3, size - 4, size - 4), outline=(20, 26, 30, 235), width=5)
    draw.ellipse((size * 0.36, size * 0.36, size * 0.64, size * 0.64), fill=(20, 26, 30, 220))
    return image


def shield_texture(size=256) -> Image.Image:
    mask = shield_mask((size, size), inset=0.04)
    image = Image.new("RGBA", (size, size), (255, 255, 255, 255))
    image.putalpha(mask)
    return image


def grass_texture(size=512, seed=7) -> Image.Image:
    if isinstance(size, int):
        size = (size, size)
    width, height = size
    rng = random.Random(seed)
    image = Image.new("RGBA", (width, height), GRASS)
    draw = ImageDraw.Draw(image)
    for y in range(0, height, 32):
        shade = 12 if (y // 32) % 2 == 0 else 0
        draw.rectangle((0, y, width, y + 32), fill=(38 - shade, 104 - shade * 2, 62 - shade))
    for _ in range(int(width * height / 40)):
        x, y = rng.randrange(width), rng.randrange(height)
        delta = rng.randint(-16, 18)
        draw.point((x, y), fill=(38 + delta, 104 + delta, 62 + delta // 2))
    return image


def pitch_texture(width=2048, height=1392) -> Image.Image:
    margin = int(width * 0.035)
    image = grass_texture((width, height))
    draw = ImageDraw.Draw(image, "RGBA")

    left, top, right, bottom = margin, margin, width - margin, height - margin
    line = (245, 245, 245, 205)
    width_line = 6

    draw.rectangle((left, top, right, bottom), outline=line, width=width_line)
    mid_x = (left + right) / 2
    draw.line((mid_x, top, mid_x, bottom), fill=line, width=width_line)

    # Ten-metre markings.
    for i in range(1, 10):
        x = left + (right - left) * i / 10
        draw.line((x, top, x, bottom), fill=(245, 245, 245, 70), width=3)

    # Try lines are ten metres in from each dead-ball line.
    try_left = left + (right - left) * 0.10
    try_right = right - (right - left) * 0.10
    draw.line((try_left, top, try_left, bottom), fill=(255, 255, 255, 230), width=8)
    draw.line((try_right, top, try_right, bottom), fill=(255, 255, 255, 230), width=8)

    # Goal posts at each end.
    for x in (try_left, try_right):
        for offset in (-3, 3):
            post_y = (top + bottom) / 2 + (bottom - top) * 0.11 * (offset / 3)
            draw.ellipse((x - 10, post_y - 10, x + 10, post_y + 10), fill=(250, 250, 250, 220))

    # Centre circle and logo.
    radius = (bottom - top) * 0.10
    draw.ellipse((mid_x - radius, (top + bottom) / 2 - radius, mid_x + radius, (top + bottom) / 2 + radius),
                 outline=(245, 245, 245, 120), width=4)

    return image


def trophy(size=256) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((size * 0.38, size * 0.72, size * 0.62, size * 0.80), radius=8, fill=(120, 92, 40, 255))
    draw.rectangle((size * 0.46, size * 0.58, size * 0.54, size * 0.74), fill=GOLD)
    draw.polygon(
        [(size * 0.24, size * 0.22), (size * 0.76, size * 0.22), (size * 0.68, size * 0.52),
         (size * 0.58, size * 0.62), (size * 0.42, size * 0.62), (size * 0.32, size * 0.52)],
        fill=GOLD,
    )
    draw.ellipse((size * 0.18, size * 0.30, size * 0.30, size * 0.52), outline=GOLD, width=10)
    draw.ellipse((size * 0.70, size * 0.30, size * 0.82, size * 0.52), outline=GOLD, width=10)
    draw.ellipse((size * 0.30, size * 0.10, size * 0.70, size * 0.26), fill=(250, 214, 120, 255))
    return image


# ---------------------------------------------------------------- Android launcher assets

ANDROID_DENSITIES = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}


def android_icons() -> None:
    base = logo_image(1024)

    for density, px in ANDROID_DENSITIES.items():
        folder = os.path.join(ANDROID, f"mipmap-{density}")
        ensure(folder)
        base.resize((px, px), Image.LANCZOS).save(os.path.join(folder, "ic_launcher.png"))

        round_img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
        mask = Image.new("L", (px * 4, px * 4), 0)
        ImageDraw.Draw(mask).ellipse((0, 0, px * 4 - 1, px * 4 - 1), fill=255)
        mask = mask.resize((px, px), Image.LANCZOS)
        round_img.paste(base.resize((px, px), Image.LANCZOS), (0, 0), mask)
        round_img.save(os.path.join(folder, "ic_launcher_round.png"))

        # Adaptive foreground: logo art inset inside the safe zone.
        fg_px = int(px * 108 / 48)
        fg = Image.new("RGBA", (fg_px, fg_px), (0, 0, 0, 0))
        art = logo_image(int(fg_px * 0.66), transparent=True)
        fg.alpha_composite(art, ((fg_px - art.width) // 2, (fg_px - art.height) // 2))
        fg.save(os.path.join(folder, "ic_launcher_foreground.png"))

    anydpi = os.path.join(ANDROID, "mipmap-anydpi-v26")
    ensure(anydpi)
    for name in ("ic_launcher", "ic_launcher_round"):
        with open(os.path.join(anydpi, f"{name}.xml"), "w", encoding="utf-8") as handle:
            handle.write(
                '<?xml version="1.0" encoding="utf-8"?>\n'
                '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
                '  <background android:drawable="@color/ic_launcher_background" />\n'
                '  <foreground android:drawable="@mipmap/ic_launcher_foreground" />\n'
                '  <monochrome android:drawable="@mipmap/ic_launcher_foreground" />\n'
                '</adaptive-icon>\n'
            )

    values = os.path.join(ANDROID, "values")
    ensure(values)
    with open(os.path.join(values, "colors.xml"), "w", encoding="utf-8") as handle:
        handle.write(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<resources>\n'
            '  <color name="ic_launcher_background">#0B1A26</color>\n'
            '  <color name="splash_background">#081C1A</color>\n'
            '  <color name="brand_gold">#E9B84E</color>\n'
            '</resources>\n'
        )

    drawable = os.path.join(ANDROID, "drawable")
    ensure(drawable)
    logo_image(512, transparent=True).save(os.path.join(drawable, "splash_logo.png"))

    for folder in ("values", "values-v31"):
        target = os.path.join(ANDROID, folder)
        ensure(target)

    with open(os.path.join(ANDROID, "values", "styles.xml"), "w", encoding="utf-8") as handle:
        handle.write(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<resources>\n'
            '  <style name="AppTheme" parent="android:Theme.Material.NoActionBar.Fullscreen">\n'
            '    <item name="android:windowBackground">@color/splash_background</item>\n'
            '    <item name="android:windowNoTitle">true</item>\n'
            '  </style>\n'
            '</resources>\n'
        )

    with open(os.path.join(ANDROID, "values-v31", "styles.xml"), "w", encoding="utf-8") as handle:
        handle.write(
            '<?xml version="1.0" encoding="utf-8"?>\n'
            '<resources>\n'
            '  <style name="AppTheme" parent="android:Theme.Material.NoActionBar.Fullscreen">\n'
            '    <item name="android:windowBackground">@color/splash_background</item>\n'
            '    <item name="android:windowNoTitle">true</item>\n'
            '    <item name="android:windowSplashScreenBackground">@color/splash_background</item>\n'
            '    <item name="android:windowSplashScreenAnimatedIcon">@drawable/splash_logo</item>\n'
            '  </style>\n'
            '</resources>\n'
        )


def ios_assets() -> None:
    appicon = os.path.join(IOS, "AppIcon.appiconset")
    ensure(appicon)
    logo_image(1024).convert("RGB").save(os.path.join(appicon, "AppIcon-1024.png"))
    with open(os.path.join(appicon, "Contents.json"), "w", encoding="utf-8") as handle:
        handle.write(
            '{\n'
            '  "images" : [\n'
            '    {\n'
            '      "filename" : "AppIcon-1024.png",\n'
            '      "idiom" : "universal",\n'
            '      "platform" : "ios",\n'
            '      "size" : "1024x1024"\n'
            '    }\n'
            '  ],\n'
            '  "info" : { "author" : "xcode", "version" : 1 }\n'
            '}\n'
        )

    launch = os.path.join(IOS, "LaunchImage.imageset")
    ensure(launch)
    art = splash_image(2732, 2048)
    art.save(os.path.join(launch, "LaunchImage.png"))
    art.resize((1366, 1024), Image.LANCZOS).save(os.path.join(launch, "LaunchImage@2x.png"))
    with open(os.path.join(launch, "Contents.json"), "w", encoding="utf-8") as handle:
        handle.write(
            '{\n'
            '  "images" : [\n'
            '    { "filename" : "LaunchImage.png", "idiom" : "universal", "scale" : "1x" },\n'
            '    { "filename" : "LaunchImage@2x.png", "idiom" : "universal", "scale" : "2x" }\n'
            '  ],\n'
            '  "info" : { "author" : "xcode", "version" : 1 }\n'
            '}\n'
        )

    icon_set = os.path.join(IOS, "AppIcon.appiconset", "..", "Contents.json")
    with open(os.path.normpath(icon_set), "w", encoding="utf-8") as handle:
        handle.write('{ "info" : { "author" : "xcode", "version" : 1 } }\n')


def background_image(width=1920, height=1080) -> Image.Image:
    """A quiet menu backdrop: deep green, faint pitch markings and a watermark crest."""
    image = vertical_gradient((width, height), (9, 27, 26), (5, 11, 17))
    image.alpha_composite(radial_glow((width, height), (34, 120, 88, 255), 90))

    draw = ImageDraw.Draw(image)
    for i in range(1, 14):
        x = width * i / 14
        draw.line([(x, 0), (x, height)], fill=(255, 255, 255, 8), width=2)
    draw.line([(0, height * 0.78), (width, height * 0.78)], fill=(255, 255, 255, 12), width=2)

    # Vignette.
    vignette = Image.new("L", (width, height), 0)
    vdraw = ImageDraw.Draw(vignette)
    for i in range(40):
        alpha = int(120 * (1 - i / 40) ** 2)
        vdraw.rectangle((i * 8, i * 5, width - i * 8, height - i * 5), outline=alpha, width=10)
    dark = Image.new("RGBA", (width, height), (3, 8, 12, 255))
    image.paste(dark, (0, 0), vignette)

    return image


def main() -> None:
    ensure(CONTENT)
    ensure(DOCS)

    logo_image(1024, transparent=True).save(os.path.join(CONTENT, "logo.png"))
    background_image(1920, 1080).save(os.path.join(CONTENT, "background.png"))
    splash_image(1920, 1080).save(os.path.join(CONTENT, "splash.png"))
    ui_panel().save(os.path.join(CONTENT, "panel.png"))
    ui_button().save(os.path.join(CONTENT, "button.png"))
    ball_texture().save(os.path.join(CONTENT, "ball.png"))
    marker_texture().save(os.path.join(CONTENT, "marker.png"))
    shield_texture().save(os.path.join(CONTENT, "shield.png"))
    grass_texture().save(os.path.join(CONTENT, "grass.png"))
    pitch_texture().save(os.path.join(CONTENT, "pitch.png"))
    trophy().save(os.path.join(CONTENT, "trophy.png"))

    android_icons()
    ios_assets()

    # Documentation previews.
    logo_image(512).save(os.path.join(DOCS, "logo.png"))
    splash_image(1280, 720).save(os.path.join(DOCS, "splash.png"))
    pitch_texture(1024, 696).save(os.path.join(DOCS, "pitch.png"))

    print("Assets generated.")


if __name__ == "__main__":
    main()
