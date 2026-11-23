"""Cut the leaderboard icon sheet (3x2 white icons on black) into white sprites.

Usage: py -3.12 ArtRef/tools/cut_leaderboard_icons.py [raw_sheet.png]

Input defaults to ArtRef/raw20_leaderboard_icons.png (generated with Codex imagegen).
Writes 256x256 RGBA white sprites (alpha = luminance) to Assets/CircuitCube/Art/UI/Icons
and a contact sheet to ArtRef/tools/pv_leaderboard_icons.png.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtRef", "raw20_leaderboard_icons.png")
OUT_DIR = os.path.join(ROOT, "Assets", "CircuitCube", "Art", "UI", "Icons")
PREVIEW = os.path.join(ROOT, "ArtRef", "tools", "pv_leaderboard_icons.png")

# Reading order of the 3x2 grid.
NAMES = ["icon_trophy", "icon_timer", "icon_medal", "icon_offline", "icon_edit", "icon_rankup"]
COLS, ROWS = 3, 2
SIZE = 256
MARGIN = 0.06            # empty border around the trimmed icon, as a fraction of SIZE
NOISE_FLOOR = 0.04       # luminance below this is background
CORE_THRESHOLD = 0.35    # luminance that counts as icon body for speck removal
MIN_COMPONENT = 150      # body components smaller than this (px) are specks
GLOW_RADIUS = 14         # glow kept within this many px of a kept component


def to_alpha(rgb):
    lum = rgb[..., :3].astype(np.float32) @ np.array([0.2126, 0.7152, 0.0722], np.float32) / 255.0
    return np.clip((lum - NOISE_FLOOR) / (1.0 - NOISE_FLOOR), 0.0, 1.0)


def clean(alpha):
    labels, count = ndimage.label(alpha > CORE_THRESHOLD)
    if count == 0:
        return alpha
    sizes = ndimage.sum(np.ones_like(alpha), labels, index=np.arange(1, count + 1))
    keep = np.isin(labels, np.nonzero(sizes >= MIN_COMPONENT)[0] + 1)
    near = ndimage.binary_dilation(keep, iterations=GLOW_RADIUS)
    return np.where(near, alpha, 0.0)


def fit_square(alpha):
    ys, xs = np.nonzero(alpha > 0.02)
    crop = alpha[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = crop.shape
    inner = int(round(SIZE * (1 - 2 * MARGIN)))
    scale = inner / max(h, w)
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    img = Image.fromarray((crop * 255).astype(np.uint8), "L").resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("L", (SIZE, SIZE), 0)
    canvas.paste(img, ((SIZE - nw) // 2, (SIZE - nh) // 2))
    white = Image.new("L", (SIZE, SIZE), 255)
    return Image.merge("RGBA", (white, white, white, canvas))


def tinted(sprite, hex_color):
    r, g, b = (int(hex_color[i:i + 2], 16) for i in (1, 3, 5))
    a = sprite.getchannel("A")
    return Image.merge("RGBA", tuple(Image.new("L", sprite.size, c) for c in (r, g, b)) + (a,))


def contact_sheet(sprites):
    medal = sprites["icon_medal"]
    rows = [
        [(n, sprites[n], None) for n in NAMES],
        [("icon_medal", medal, c) for c in ("#FFC83A", "#C9CEDD", "#D98E5A")],
    ]
    cell, pad = 128, 16
    cols = max(len(r) for r in rows)
    band_w = cols * (cell + pad) + pad
    band_h = len(rows) * (cell + pad) + pad
    sheet = Image.new("RGBA", (band_w, band_h * 2), "#12152A")
    sheet.paste(Image.new("RGBA", (band_w, band_h), "#F3F4F8"), (0, band_h))
    for band, (y0, plain) in enumerate(((0, "#FFFFFF"), (band_h, "#1C2033"))):
        for ri, row in enumerate(rows):
            for ci, (_, sprite, color) in enumerate(row):
                icon = tinted(sprite, color or plain).resize((cell, cell), Image.LANCZOS)
                sheet.alpha_composite(icon, (pad + ci * (cell + pad), y0 + pad + ri * (cell + pad)))
    return sheet


def main():
    raw = sys.argv[1] if len(sys.argv) > 1 else RAW
    rgb = np.asarray(Image.open(raw).convert("RGB"))
    alpha = to_alpha(rgb)
    h, w = alpha.shape
    sprites = {}
    for i, name in enumerate(NAMES):
        r, c = divmod(i, COLS)
        cell = alpha[r * h // ROWS:(r + 1) * h // ROWS, c * w // COLS:(c + 1) * w // COLS]
        sprite = fit_square(clean(cell))
        sprite.save(os.path.join(OUT_DIR, name + ".png"))
        sprites[name] = sprite
        print("wrote", name)
    contact_sheet(sprites).save(PREVIEW)
    print("preview", PREVIEW)


if __name__ == "__main__":
    main()
