# Store artwork and copy

Everything here is generated from simulator captures of the game running the scripted demo tour
(`RL_DEMO=1`, see [docs/building.md](../docs/building.md#store-captures)). Nothing is mocked up:
the screenshots are the real UI at device resolution.

## Folders

| Folder | Contents | Required size |
| --- | --- | --- |
| `appstore/copy.md` | App name, subtitle, promo text, description, keywords, privacy answers. | — |
| `appstore/iphone/` | 7 screenshots, scaled from the 6.9" captures to the medium Dynamic Island size that App Store Connect requires. | 2622 × 1206 landscape |
| `appstore/iphone-6.9/` | The same seven screenshots at the 6.9" large-display size, for the optional larger slot. | 2868 × 1320 landscape |
| `appstore/ipad/` | 7 screenshots from an iPad Pro 13" simulator, plus the App Store preview. | 2752 × 2064 landscape |
| `appstore/ipad/preview-1600x1200.mp4` | 28-second App Store app preview, H.264, no audio. | 1600 × 1200 |
| `googleplay/copy.md` | Title, short description, full description, data-safety answers. | — |
| `googleplay/phone/` | Same seven screens, cropped to 16:9. | 1920 × 1080 |
| `googleplay/tablet/` | Tablet set, cropped to 16:10. | 1920 × 1200 |
| `privacy.md` | Privacy notice, linked from the support page and both listings. | — |

## Why the iPad shots are cropped

Since iPadOS 26 the system runs the game in a resizable window instead of full screen, and the
simulator holds the "device" in portrait while the game draws landscape. The capture therefore
contains the game's canvas plus black surround. `tools/make_store_art.py` rotates the frame, finds
the game canvas automatically and crops it to the exact aspect ratio Apple and Google require, so
the assets are the game's own pixels — not stretched, and never letterboxed.

## Regenerating

```bash
# 1. capture (with the simulator booted and the app installed)
SIMCTL_CHILD_RL_DEMO=1 xcrun simctl launch <udid> uk.co.allthejohnsons.rugbyleaguegame
# screenshots into /tmp/phshots (iPhone) and /tmp/ipadshots (iPad), preview recorded with
# xcrun simctl io <udid> recordVideo
# 2. rebuild the derived artwork
python3 tools/make_store_art.py
```

The script prints the size of every file it writes, so a wrong size is visible in the output.

## Apple size check

App Store Connect requires at least one iPhone screenshot for the Dynamic Island **medium display**
(1206 × 2622 portrait or 2622 × 1206 landscape) and, if the app runs on iPad, iPad 13"
(2064 × 2752 portrait or 2752 × 2064 landscape). The 6.9" large-display size (1320 × 2868 portrait
or 2868 × 1320 landscape) is optional, which is why those files live in `iphone-6.9/` rather than in
the folder you upload from. Every set here is landscape and matches its size exactly;
`make_store_art.py` re-checks every file on each run.
