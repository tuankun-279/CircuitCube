"""Board images for the polygon shapes and the arm-drawn wires, made from the ChatGPT square tile and wire.

usage: board_shapes.py <Assets/CircuitCube/Art/Board> [<folder with the old wire_straight.png per theme>]

Faces (board_pent, board_hex, board_kite, board_mega_corner, board_mega_edge):
  the square face board_tile.png is measured from each of its four sides (its bevel is lit from the top left, so the
  four sides differ); a new outline is drawn from a rounded distance field, each pixel taking the side profiles
  blended by the direction its edge faces. The flat middle keeps the square's own pixels (its fine grain).
  Outlines match PolyShapes.Outline in the game: centre at the image centre, side 0 to the right, farthest corner at
  OUTLINE_FILL of the half width.
Wires (wire_arm, wire_arm_core, wire_hub, wire_hub_core):
  the tube's cross-section measured from the cut straight wire (as wires_v2.py did) drawn as a strip (the arm, stretched
  from the tile centre to a side in the game) and as a disc (the hub in the tile centre). 256 px = 1 board unit, as on
  the square tile images (512 px for a tile 2 units wide).
"""
import math
import os
import sys

import numpy as np
from PIL import Image

BOARD = sys.argv[1]
OLD_WIRES = sys.argv[2] if len(sys.argv) > 2 else BOARD
T = 512
OUTLINE_FILL = 0.97
MEGA_CENTRE = 0.42
CORE_HALF = {"Dark": 11.0, "Light": 8.0}   # px at 256 px per unit, as wires_v2.py
PROFILE_LEN = 90                           # px of bevel measured from each side of the square
FLAT_FROM = 70                             # past this depth the square's own middle pixels are used
CORNER_R = 60.0

# circumradius of a typical tile of each outline in board units (from the game's shapes), so the bevel keeps its
# width in the world: the image always spans the outline, whatever the tile's size
WORLD_RADIUS = {"pent": 1.1, "hex": 1.35, "kite": 1.81, "mega_corner": 1.51, "mega_edge": 1.6}


# ---------------------------------------------------------------- outlines (same maths as PolyShapes.Outline)

def regular(n):
    return [(math.cos(math.radians(-180.0 / n + k * 360.0 / n)), math.sin(math.radians(-180.0 / n + k * 360.0 / n))) for k in range(n)]


def pent_pieces(kind):
    P = [(math.cos(math.radians(90 + 72 * k)), math.sin(math.radians(90 + 72 * k))) for k in range(5)]
    L = lambda a, b, t: (P[a][0] + (P[b][0] - P[a][0]) * t, P[a][1] + (P[b][1] - P[a][1]) * t)
    s = MEGA_CENTRE
    if kind == "kite":
        return [P[0], L(0, 1, 0.5), (0.0, 0.0), L(0, 4, 0.5)]
    if kind == "mega_corner":
        return [P[0], L(0, 1, 1 / 3), (P[0][0] * s, P[0][1] * s), L(4, 0, 2 / 3)]
    return [L(0, 1, 1 / 3), L(0, 1, 2 / 3), (P[1][0] * s, P[1][1] * s), (P[0][0] * s, P[0][1] * s)]


def normalise(pts):
    n = len(pts)
    cx = sum(p[0] for p in pts) / n
    cy = sum(p[1] for p in pts) / n
    mx = (pts[0][0] + pts[1][0]) / 2 - cx
    my = (pts[0][1] + pts[1][1]) / 2 - cy
    a = -math.atan2(my, mx)
    o = [((x - cx) * math.cos(a) - (y - cy) * math.sin(a), (x - cx) * math.sin(a) + (y - cy) * math.cos(a)) for x, y in pts]
    r = max(math.hypot(x, y) for x, y in o)
    return [(x / r, y / r) for x, y in o]


OUTLINES = {
    "pent": normalise(regular(5)),
    "hex": normalise(regular(6)),
    "kite": normalise(pent_pieces("kite")),
    "mega_corner": normalise(pent_pieces("mega_corner")),
    "mega_edge": normalise(pent_pieces("mega_edge")),
}


def to_pixels(outline):
    # image y points down; the game's v points up
    return [(256 + x * OUTLINE_FILL * 256, 256 - y * OUTLINE_FILL * 256) for x, y in outline]


# ---------------------------------------------------------------- distance field of a rounded convex polygon

def inset(poly, d):
    """Convex polygon moved inwards by d (px)."""
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    sgn = 1 if area > 0 else -1
    lines = []
    for i in range(n):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]
        ex, ey = x1 - x0, y1 - y0
        l = math.hypot(ex, ey)
        nx, ny = sgn * ey / l, -sgn * ex / l      # outward normal
        lines.append((nx, ny, nx * x0 + ny * y0 - d))
    out = []
    for i in range(n):
        a1, b1, c1 = lines[i - 1]
        a2, b2, c2 = lines[i]
        det = a1 * b2 - a2 * b1
        out.append(((c1 * b2 - c2 * b1) / det, (a1 * c2 - a2 * c1) / det))
    return out


def sdf(poly):
    """Signed distance (px, negative inside) of every pixel centre to a convex polygon."""
    ys, xs = np.mgrid[0:T, 0:T].astype(np.float64) + 0.5
    n = len(poly)
    area = sum(poly[i][0] * poly[(i + 1) % n][1] - poly[(i + 1) % n][0] * poly[i][1] for i in range(n))
    sgn = 1 if area > 0 else -1
    plane = np.full((T, T), -1e9)
    seg = np.full((T, T), 1e9)
    for i in range(n):
        (x0, y0), (x1, y1) = poly[i], poly[(i + 1) % n]
        ex, ey = x1 - x0, y1 - y0
        l = math.hypot(ex, ey)
        nx, ny = sgn * ey / l, -sgn * ex / l
        plane = np.maximum(plane, (xs - x0) * nx + (ys - y0) * ny)
        t = np.clip(((xs - x0) * ex + (ys - y0) * ey) / (l * l), 0, 1)
        seg = np.minimum(seg, np.hypot(xs - (x0 + t * ex), ys - (y0 + t * ey)))
    return np.where(plane <= 0, plane, seg)


# ---------------------------------------------------------------- faces

def side_profiles(tile):
    """RGB(A) against depth from each side of the square: right, bottom, left, top (image directions 0, 90, 180, 270)."""
    a = tile.astype(np.float64)
    band = slice(200, 312)
    d = np.arange(PROFILE_LEN)
    right = a[band, T - 1 - d, :].mean(axis=0)
    left = a[band, d, :].mean(axis=0)
    top = a[d, band, :].mean(axis=1)
    bottom = a[T - 1 - d, band, :].mean(axis=1)
    return [right, bottom, left, top]


def face(tile, outline, bevel_scale):
    poly = to_pixels(outline)
    r = CORNER_R * bevel_scale
    sd = sdf(inset(poly, r)) - r            # rounded outline: negative inside
    depth = -sd / bevel_scale               # depth in the square's px
    gy, gx = np.gradient(sd)
    theta = np.arctan2(gy, gx)              # direction the nearest edge faces (image coords)
    prof = side_profiles(tile)
    dd = np.clip(depth, 0, PROFILE_LEN - 1)
    img = np.zeros((T, T, 4))
    for side, ang in enumerate((0.0, math.pi / 2, math.pi, -math.pi / 2)):
        w = np.maximum(0.0, np.cos(theta - ang)) ** 2
        for c in range(4):
            img[..., c] += w * np.interp(dd, np.arange(PROFILE_LEN), prof[side][:, c])
    # the flat middle: the square's own pixels, faded in so the grain carries on
    k = np.clip((depth - (FLAT_FROM - 8)) / 8.0, 0, 1)[..., None]
    img = img * (1 - k) + tile.astype(np.float64) * k
    img[..., 3] = 255
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))


def check_square(tile):
    """Redraw the square itself and report how close it is to the original (fit of CORNER_R)."""
    sq = normalise(regular(4))
    poly = [(256 + x * 256 * math.sqrt(2), 256 - y * 256 * math.sqrt(2)) for x, y in sq]
    r = CORNER_R
    sd = sdf(inset(poly, r)) - r
    depth = -sd
    gy, gx = np.gradient(sd)
    theta = np.arctan2(gy, gx)
    prof = side_profiles(tile)
    dd = np.clip(depth, 0, PROFILE_LEN - 1)
    img = np.zeros((T, T, 4))
    for side, ang in enumerate((0.0, math.pi / 2, math.pi, -math.pi / 2)):
        w = np.maximum(0.0, np.cos(theta - ang)) ** 2
        for c in range(4):
            img[..., c] += w * np.interp(dd, np.arange(PROFILE_LEN), prof[side][:, c])
    corner = (slice(0, 120), slice(0, 120))
    return float(np.abs(img[corner][..., :3] - tile[corner][..., :3].astype(np.float64)).mean())


# ---------------------------------------------------------------- wires

def tube_profile(path):
    """Cross-section of the cut straight wire: RGBA by distance (px) from its centre line (as wires_v2.py)."""
    a = np.array(Image.open(path).convert("RGBA")).astype(np.float64)
    mean = a[:, 120:392].mean(axis=1)
    al = mean[:, 3]
    centre = (al * np.arange(T)).sum() / max(al.sum(), 1)
    d = np.abs(np.arange(T) - centre)
    order = np.argsort(d)
    dist = np.arange(0, 200, 0.5)
    prof = np.zeros((len(dist), 4))
    for c in range(4):
        prof[:, c] = np.interp(dist, d[order], mean[order, c])
    prof[:, 3] = np.maximum.accumulate(prof[::-1, 3])[::-1]
    prof[:, :3] = np.clip(prof[:, :3] / max(prof[0, :3].max(), 1) * 255, 0, 255)
    return dist, prof


def wire_images(theme, dist, prof):
    hc = CORE_HALF[theme]

    def tube(d):
        img = np.zeros(d.shape + (4,))
        for c in range(4):
            img[..., c] = np.interp(d, dist, prof[:, c], right=0)
        return Image.fromarray(img.clip(0, 255).astype(np.uint8))

    def core(d):
        img = np.zeros(d.shape + (4,), np.uint8)
        img[..., :3] = 255
        img[..., 3] = (np.clip(hc + 0.75 - d, 0, 1.5) / 1.5 * 255).astype(np.uint8)
        return Image.fromarray(img)

    # arm: 64 px along (stretched in the game), 256 px across = 1 board unit
    ys = np.mgrid[0:256, 0:64][0].astype(np.float64) + 0.5
    d_arm = np.abs(ys - 128.0)
    # hub: 256 x 256 = 1 board unit, the same section turned round the centre
    ys, xs = np.mgrid[0:256, 0:256].astype(np.float64) + 0.5
    d_hub = np.hypot(xs - 128.0, ys - 128.0)
    return {"wire_arm": tube(d_arm), "wire_arm_core": core(d_arm), "wire_hub": tube(d_hub), "wire_hub_core": core(d_hub)}


def main():
    for theme in ("Dark", "Light"):
        folder = os.path.join(BOARD, theme)
        tile = np.array(Image.open(os.path.join(folder, "board_tile.png")).convert("RGBA"))
        print(theme, "square redraw, mean corner error:", round(check_square(tile), 2))
        for name, outline in OUTLINES.items():
            scale = (OUTLINE_FILL * 256 / WORLD_RADIUS[name]) / 256.0
            face(tile, outline, scale).save(os.path.join(folder, "board_" + name + ".png"))
        dist, prof = tube_profile(os.path.join(OLD_WIRES, theme, "wire_straight.png"))
        for name, img in wire_images(theme, dist, prof).items():
            img.save(os.path.join(folder, name + ".png"))


if __name__ == "__main__":
    main()
