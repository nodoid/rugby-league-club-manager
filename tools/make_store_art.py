"""Builds the App Store / Google Play artwork from simulator captures.

Simulator frames are portrait (the device is held portrait while the landscape-only game draws
rotated), and iPadOS 26 gives the game a square window inside that screen. This script rotates the
frame, finds the game's own canvas, crops to the required aspect ratio and resizes to the exact
sizes Apple and Google require.
"""

import os
import numpy as np
from PIL import Image

ROOT = "/Users/pauljohnson/RiderProjects/rugby/store"
# iPhone with Dynamic Island: the medium display (iPhone 18 Pro, 6.3") is the size the App Store
# requires, so that is what goes in appstore/iphone. The 6.9" large-display size is kept too.
APPSTORE_IPHONE = (2622, 1206)
APPSTORE_IPHONE_LARGE = (2868, 1320)
APPSTORE_IPAD = (2752, 2064)
PLAY_PHONE = (1920, 1080)
PLAY_TABLET = (1920, 1200)

IPHONE_SHOTS = [
    ("01-title.png", "p04.png", 0),
    ("02-dashboard.png", "p07.png", 0),
    ("03-squad.png", "p10.png", 0),
    ("04-league-table.png", "p13.png", 0),
    ("05-matchday.png", "p17.png", 0),
    ("06-3d-kickoff.png", "p20.png", 0),
    ("07-3d-match.png", "p21.png", 0),
]

IPAD_SHOTS = [
    ("01-title.png", "i04.png"),
    ("02-dashboard.png", "i08.png"),
    ("03-squad.png", "i11.png"),
    ("04-league-table.png", "i13.png"),
    ("05-matchday.png", "i15.png"),
    ("06-3d-kickoff.png", "i17.png"),
    ("07-3d-match.png", "i19.png"),
]


def luminance(image):
    return np.asarray(image.convert("L")).astype(int)


# The iPadOS 26 window gives the game a 2064x1296 canvas inside the 2752x2064 rotated frame.
IPAD_CANVAS = (0, 768, 2064, 1296)
BACKGROUND = (10, 19, 26)


def ipad_canvas(path):
    """Rotates a portrait simulator frame and cuts out the game's own canvas.

    The 3D screen draws its HUD slightly higher than the management screens, so the top edge is
    taken from the frame itself to avoid shaving the scoreboard.
    """
    frame = Image.open(path).convert("RGB").rotate(90, expand=True)
    mask = np.asarray(frame.convert("L")).astype(int) > 26
    rows = np.where(mask.any(axis=1))[0]
    top = min(int(rows.min()), IPAD_CANVAS[1]) if rows.size else IPAD_CANVAS[1]
    height = min(IPAD_CANVAS[3], frame.height - top)
    return frame.crop((0, top, IPAD_CANVAS[2], top + height))


def fit_pad(image, size, background=BACKGROUND):
    """Scales to fit and centres on the requested canvas, padding rather than cutting the UI."""
    scale = min(size[0] / image.width, size[1] / image.height)
    resized = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGB", size, background)
    canvas.paste(resized, ((size[0] - resized.width) // 2, (size[1] - resized.height) // 2))
    return canvas


def crop_to_aspect(image, target_ratio):
    width, height = image.size
    ratio = width / height
    if ratio > target_ratio:
        new_width = int(round(height * target_ratio))
        left = (width - new_width) // 2
        return image.crop((left, 0, left + new_width, height))
    if ratio < target_ratio:
        new_height = int(round(width / target_ratio))
        top = (height - new_height) // 2
        return image.crop((0, top, width, top + new_height))
    return image


def main():
    band = ipad_canvas("/tmp/ipadshots/i08.png")
    print("iPad canvas", band.size, "aspect", round(band.width / band.height, 3))

    for directory in ("appstore/iphone", "appstore/iphone-6.9", "appstore/ipad", "googleplay/phone",
                      "googleplay/tablet"):
        os.makedirs(os.path.join(ROOT, directory), exist_ok=True)

    for name, source, _ in IPHONE_SHOTS:
        image = Image.open(os.path.join("/tmp/phshots", source)).convert("RGB")
        image.resize(APPSTORE_IPHONE_LARGE, Image.LANCZOS).save(
            os.path.join(ROOT, "appstore/iphone-6.9", name))
        image.resize(APPSTORE_IPHONE, Image.LANCZOS).save(os.path.join(ROOT, "appstore/iphone", name))

        play = crop_to_aspect(Image.open(os.path.join("/tmp/phshots", source)).convert("RGB"), 16 / 9)
        play = play.resize(PLAY_PHONE, Image.LANCZOS)
        play.save(os.path.join(ROOT, "googleplay/phone", name))

    for name, source in IPAD_SHOTS:
        band = ipad_canvas(os.path.join("/tmp/ipadshots", source))
        fit_pad(band, APPSTORE_IPAD).save(os.path.join(ROOT, "appstore/ipad", name))
        fit_pad(band, PLAY_TABLET).save(os.path.join(ROOT, "googleplay/tablet", name))

    for name, size in (("appstore/iphone", APPSTORE_IPHONE),
                       ("appstore/iphone-6.9", APPSTORE_IPHONE_LARGE),
                       ("appstore/ipad", APPSTORE_IPAD),
                       ("googleplay/phone", PLAY_PHONE), ("googleplay/tablet", PLAY_TABLET)):
        folder = os.path.join(ROOT, name)
        for file in sorted(os.listdir(folder)):
            got = Image.open(os.path.join(folder, file)).size
            print(f"{name}/{file}: {got[0]}x{got[1]} {'OK' if got == size else 'WRONG'}")


main()
