"""Cut the v3 ChatGPT sheets (ArtRef/raw3_*.png) into game sprites.

usage: cut_v3.py <raw dir> <Assets/CircuitCube/Art>

Sheets (white shapes on black, so brightness becomes alpha and the sprite stays white for the game to tint):
  raw3_expert_icons.png   three Expert badge ideas -> UI/Icons/icon_expert_{comet,orbit,constellation}.png
  raw3_frame_divider.png  rounded frame (left) and fading line (right) -> UI/Common/ui_frame.png, ui_divider.png
  raw3_vignette.png       grey vignette mask -> UI/Common/ui_vignette.png (alpha: middle 0.88, edges 1)
Every pixel comes from the ChatGPT art; this only crops, removes specks, scales and turns brightness into alpha.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

RAW, OUT = sys.argv[1], sys.argv[2]
ICON = 128                     # same size as the other icons in UI/Icons


def white_alpha(gray):
    """A white sprite whose alpha is the sheet's brightness (black background -> transparent)."""
    a = np.clip((gray.astype(np.float32) - 12.0) / (255.0 - 24.0), 0.0, 1.0)
    rgba = np.zeros(gray.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255 + 0.5).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def load_gray(name):
    return np.array(Image.open(os.path.join(RAW, name)).convert("L"))


def objects(gray, thr=60, min_px=400, gap=40):
    """Bounding boxes of the shapes, parts closer than `gap` px merged (a comet's tail belongs to its star)."""
    mask = gray > thr
    grown = ndimage.binary_dilation(mask, iterations=gap // 2)
    labels, n = ndimage.label(grown)
    boxes = []
    for sl in ndimage.find_objects(labels):
        if mask[sl].sum() >= min_px:
            boxes.append(sl)
    return sorted(boxes, key=lambda s: s[1].start)


def crop(gray, sl, pad):
    y0 = max(0, sl[0].start - pad); y1 = min(gray.shape[0], sl[0].stop + pad)
    x0 = max(0, sl[1].start - pad); x1 = min(gray.shape[1], sl[1].stop + pad)
    return gray[y0:y1, x0:x1]


def square(img, size, fill=0.9):
    """Centre on a transparent square, the longer side `fill` of it."""
    w, h = img.size
    s = fill * size / max(w, h)
    img = img.resize((max(1, round(w * s)), max(1, round(h * s))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    out.paste(img, ((size - img.width) // 2, (size - img.height) // 2))
    return out


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)
    print("wrote", p, img.size)


def icons():
    g = load_gray("raw3_expert_icons.png")
    g = np.where(g < 24, 0, g)                      # flatten the JPEG-ish noise in the black
    boxes = objects(g)
    names = ["comet", "orbit", "constellation"]
    if len(boxes) != 3:
        raise SystemExit("expected 3 icons, found %d" % len(boxes))
    for name, sl in zip(names, boxes):
        save(square(white_alpha(crop(g, sl, 8)), ICON), "UI", "Icons", "icon_expert_" + name + ".png")


def frame_and_divider():
    g = load_gray("raw3_frame_divider.png")
    g = np.where(g < 24, 0, g)
    half = g.shape[1] // 2
    left, right = g[:, :half], g[:, half:]
    # frame: the outline's own bounds, scaled to the size of ui_square so the 9-slice corners match
    sl = objects(left, gap=10)[0]
    frame = white_alpha(crop(left, sl, 2))
    save(frame.resize((168, 174), Image.LANCZOS), "UI", "Common", "ui_frame.png")   # ui_square is 168x174
    # divider: the line with its faded ends, kept thin
    rows = np.where(right.max(axis=1) > 40)[0]
    cols = np.where(right.max(axis=0) > 8)[0]
    line = right[max(0, rows[0] - 3): rows[-1] + 4, cols[0]: cols[-1] + 1]
    save(white_alpha(line).resize((512, 8), Image.LANCZOS), "UI", "Common", "ui_divider.png")


def vignette():
    g = np.array(Image.open(os.path.join(RAW, "raw3_vignette.png")).convert("L").resize((256, 384), Image.LANCZOS)).astype(np.float32)
    mid = np.median(g[g.shape[0] // 3: 2 * g.shape[0] // 3, g.shape[1] // 3: 2 * g.shape[1] // 3])
    t = np.clip((g - mid) / max(1.0, 255.0 - mid), 0.0, 1.0)
    a = 0.88 + 0.12 * t                             # the middle keeps most of the dim, the edges go fully dark
    rgba = np.zeros(g.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255 + 0.5).astype(np.uint8)
    save(Image.fromarray(rgba, "RGBA"), "UI", "Common", "ui_vignette.png")


if __name__ == "__main__":
    icons()
    frame_and_divider()
    if os.path.exists(os.path.join(RAW, "raw3_vignette.png")):
        vignette()
