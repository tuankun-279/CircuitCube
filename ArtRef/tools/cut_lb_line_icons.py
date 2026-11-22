"""Cut the leaderboard line icon sheet (3x3 white outlines on black, from Codex) into white sprites.

Usage: py -3.12 ArtRef/tools/cut_lb_line_icons.py [raw_sheet.png]

Input defaults to ArtRef/raw21_lb_line_icons.png. Every icon is scaled by the same factor (the largest icon
fills the 256 px square less a margin), so the stroke weight stays the same across the set. Writes 256x256
RGBA white sprites to Assets/CircuitCube/Art/UI/Icons and a contact sheet to ArtRef/tools/pv_lb_line_icons.png.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cut_leaderboard_icons import clean, to_alpha, tinted  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RAW = os.path.join(ROOT, "ArtRef", "raw21_lb_line_icons.png")
OUT_DIR = os.path.join(ROOT, "Assets", "CircuitCube", "Art", "UI", "Icons")
PREVIEW = os.path.join(ROOT, "ArtRef", "tools", "pv_lb_line_icons.png")

NAMES = [
    "icon_dock_cube", "icon_dock_tetra", "icon_dock_octa",
    "icon_dock_sphere", "icon_dock_dodeca", "icon_dock_megamix",
    "icon_info", "icon_chevron_left", "icon_chevron_right",
]
SIZE = 256
INNER = 232   # the largest icon spans this many pixels
GROW = 5      # max filter width: thickens the strokes to the mockup's 1.9 px at 24 px (the sheet drew them thinner)


def crops(alpha):
    h, w = alpha.shape
    out = []
    for i in range(9):
        r, c = divmod(i, 3)
        cell = clean(alpha[r * h // 3:(r + 1) * h // 3, c * w // 3:(c + 1) * w // 3])
        ys, xs = np.nonzero(cell > 0.02)
        out.append(cell[ys.min():ys.max() + 1, xs.min():xs.max() + 1])
    return out


def place(crop, scale):
    h, w = crop.shape
    nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
    img = Image.fromarray((crop * 255).astype(np.uint8), "L").resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("L", (SIZE, SIZE), 0)
    canvas.paste(img, ((SIZE - nw) // 2, (SIZE - nh) // 2))
    canvas = canvas.filter(ImageFilter.MaxFilter(GROW))
    white = Image.new("L", (SIZE, SIZE), 255)
    return Image.merge("RGBA", (white, white, white, canvas))


def sheet(sprites):
    cell, pad = 128, 16
    band_w = len(sprites) * (cell + pad) + pad
    band_h = cell + 2 * pad
    out = Image.new("RGBA", (band_w, band_h * 2), "#12152A")
    out.paste(Image.new("RGBA", (band_w, band_h), "#F3F4F8"), (0, band_h))
    for band, (y0, colour) in enumerate(((0, "#C3C6DA"), (band_h, "#626C88"))):
        for i, s in enumerate(sprites):
            icon = tinted(s, "#46B8C6" if NAMES[i] == "icon_info" else colour).resize((cell, cell), Image.LANCZOS)
            out.alpha_composite(icon, (pad + i * (cell + pad), y0 + pad))
    return out


def main():
    raw = sys.argv[1] if len(sys.argv) > 1 else RAW
    alpha = to_alpha(np.asarray(Image.open(raw).convert("RGB")))
    parts = crops(alpha)
    scale = INNER / max(max(p.shape) for p in parts)
    sprites = []
    for name, p in zip(NAMES, parts):
        s = place(p, scale)
        s.save(os.path.join(OUT_DIR, name + ".png"))
        sprites.append(s)
        print("wrote", name)
    sheet(sprites).save(PREVIEW)
    print("preview", PREVIEW)


if __name__ == "__main__":
    main()
