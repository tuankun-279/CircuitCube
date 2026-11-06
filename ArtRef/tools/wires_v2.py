"""Rebuild the V2 wire sprites so lines are continuous, centred and evenly lit.

usage: wires_v2.py <Art/V2/Board dir>

The first cut (cut_v2.py) pasted ChatGPT wire pieces together, which left gaps in the white core, streaky glow,
an off-centre core and glow that faded out right at the tile edge (a visible notch where two tiles meet).
This script keeps the ChatGPT look but redraws the geometry:
  * the tube's cross-section (colour and alpha against distance from the centre line) is measured from the cut
    straight wire, averaged along its length and made symmetric;
  * every wire is drawn from a distance field: arms run from the tile centre through the port and on past the tile
    edge, so the tube and its glow are at full strength where the next tile continues them;
  * the white core is a clean band of the measured width, on the same centre line;
  * triangle tiles are clipped to the triangle, so the cut at each edge is exactly where the neighbour starts.
"""
import math
import os
import sys

import numpy as np
from PIL import Image

BOARD = sys.argv[1]
T = 512                                # sprite size
SQUARE = {                             # arm directions (image coords, y down) of the canonical square wires
    "wire_end": [0], "wire_straight": [0, 180], "wire_corner": [0, 90], "wire_tee": [0, 90, 180], "wire_cross": [0, 90, 180, 270],
}
TRI = {"tri_wire_end": [0], "tri_wire_bend": [0, 120], "tri_wire_fork": [0, 120, 240]}
TRI_SCALE = 2.0 / 3.0                  # triangle quads are 1.5x larger in the game, so lines are drawn thinner
TRI_VERTS = [(0.0, 255.5), (384.0, 34.0), (384.0, 477.0)]   # the triangle inside board_tri.png


def profile(theme):
    """Cross-section of the tube: RGBA by distance (px) from the centre line, from the cut straight wire."""
    a = np.array(Image.open(os.path.join(BOARD, theme, "wire_straight.png")).convert("RGBA")).astype(np.float64)
    cols = a[:, 120:392]                                  # stay clear of the tile edges
    mean = cols.mean(axis=1)                              # average along the wire: no streaks
    al = mean[:, 3]
    centre = (al * np.arange(T)).sum() / max(al.sum(), 1)
    d = np.abs(np.arange(T) - centre)
    order = np.argsort(d)
    # symmetric: average both sides at the same distance, sampled on a 0.5 px grid
    dist = np.arange(0, 200, 0.5)
    prof = np.zeros((len(dist), 4))
    for c in range(4):
        prof[:, c] = np.interp(dist, d[order], mean[order, c])
    prof[:, 3] = np.maximum.accumulate(prof[::-1, 3])[::-1]   # alpha never rises going outwards
    # the tube body is white so the game tint gives the exact colour
    prof[:, :3] = np.clip(prof[:, :3] / max(prof[0, :3].max(), 1) * 255, 0, 255)
    return dist, prof


# Half width (px) of the white core on a square tile. Narrower than the first cut's 17 px in dark mode, so the
# energy colour shows on both sides of the core instead of a white line with a thin coloured rim.
CORE_HALF = {"Dark": 11.0, "Light": 8.0}


def seg_dist(px, py, ax, ay, bx, by):
    """Distance from pixels to segment a-b."""
    vx, vy = bx - ax, by - ay
    t = ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy)
    t = np.clip(t, 0, 1)
    return np.hypot(px - (ax + t * vx), py - (ay + t * vy))


def field(angles, centre, reach):
    """Distance to the union of arms from the centre outwards (they run past the tile edge)."""
    ys, xs = np.mgrid[0:T, 0:T].astype(np.float64) + 0.5
    d = np.full((T, T), 1e9)
    for ang in angles:
        r = math.radians(ang)
        ex, ey = centre[0] + math.cos(r) * reach, centre[1] + math.sin(r) * reach
        d = np.minimum(d, seg_dist(xs, ys, centre[0], centre[1], ex, ey))
    return d


def tri_mask():
    ys, xs = np.mgrid[0:T, 0:T].astype(np.float64) + 0.5
    m = np.ones((T, T), bool)
    v = TRI_VERTS
    for i in range(3):
        (x0, y0), (x1, y1) = v[i], v[(i + 1) % 3]
        ox, oy = v[(i + 2) % 3]
        side = lambda x, y: (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0)
        m &= np.sign(side(xs, ys)) == np.sign(side(ox, oy))
    return m


def render(d, dist, prof, scale, half_core):
    dd = d / scale
    img = np.zeros((T, T, 4))
    for c in range(4):
        img[..., c] = np.interp(dd, dist, prof[:, c], right=0)
    tube = Image.fromarray(img.clip(0, 255).astype(np.uint8))
    hc = half_core * scale
    core_a = np.clip(hc + 0.75 - d, 0, 1.5) / 1.5 * 255            # 1.5 px anti-aliased edge
    core = np.zeros((T, T, 4), np.uint8)
    core[..., :3] = 255
    core[..., 3] = core_a.astype(np.uint8)
    return tube, Image.fromarray(core)


def main():
    tmask = tri_mask()
    for theme in ("Dark", "Light"):
        dist, prof = profile(theme)
        hc = CORE_HALF[theme]
        print(theme, "tube alpha at 0/20/40/60 px:", [int(np.interp(x, dist, prof[:, 3])) for x in (0, 20, 40, 60)], "core half", hc)
        for name, angles in SQUARE.items():
            tube, core = render(field(angles, (256.0, 256.0), 600), dist, prof, 1.0, hc)
            tube.save(os.path.join(BOARD, theme, name + ".png"))
            core.save(os.path.join(BOARD, theme, name + "_core.png"))
        for name, angles in TRI.items():
            # image y points down: 120 degrees in the game's y-up frame is -120 here
            tube, core = render(field([-a for a in angles], (256.0, 255.5), 600), dist, prof, TRI_SCALE, hc)
            t, c = np.array(tube), np.array(core)
            t[~tmask, 3] = 0
            c[~tmask, 3] = 0
            Image.fromarray(t).save(os.path.join(BOARD, theme, name + ".png"))
            Image.fromarray(c).save(os.path.join(BOARD, theme, name + "_core.png"))


if __name__ == "__main__":
    main()
