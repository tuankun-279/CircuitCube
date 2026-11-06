"""Cut the v2 ChatGPT sheets (ArtRef/raw2_*.png) into game sprites.
usage: cut_v2.py <raw dir> <out dir>
Every pixel comes from the ChatGPT art; this only crops, scales, places pieces on the tile grid and splits the
bright core of a wire into its own layer (so the game can tint the tube and keep the core white)."""
import math, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

RAW, OUT = sys.argv[1], sys.argv[2]
T = 512                      # tile sprite size: one square tile = 2 board units
WIRE = int(T * 0.15)         # wire thickness on a square tile
JOINT = int(WIRE * 1.35)


def load(name):
    return Image.open(os.path.join(RAW, "raw2_" + name + ".png")).convert("RGBA")


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)


def clean(img, thr=6, min_px=40):
    """Drop faint noise and tiny specks ChatGPT leaves around shapes."""
    a = np.array(img)
    al = a[..., 3]
    al[al < thr] = 0
    lab, n = ndimage.label(al > 0)
    if n:
        sizes = ndimage.sum(al > 0, lab, range(1, n + 1))
        for i, s in enumerate(sizes):
            if s < min_px:
                al[lab == i + 1] = 0
    a[..., 3] = al
    return Image.fromarray(a)


def objects(img, thr=20, min_px=800, merge=0):
    """Separate objects (connected opaque areas, optionally merged when closer than `merge` px), with their glow."""
    a = np.array(img)
    m = a[..., 3] > thr
    if merge:
        m = ndimage.binary_dilation(m, iterations=merge)
    lab, n = ndimage.label(m)
    objs = ndimage.find_objects(lab)
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    out = []
    for i in range(n):
        if sizes[i] < min_px:
            continue
        sl = objs[i]
        pad = 24
        y0, y1 = max(0, sl[0].start - pad), min(a.shape[0], sl[0].stop + pad)
        x0, x1 = max(0, sl[1].start - pad), min(a.shape[1], sl[1].stop + pad)
        piece = a[y0:y1, x0:x1].copy()
        own = ndimage.binary_dilation(lab[y0:y1, x0:x1] == i + 1, iterations=pad)
        piece[~own, 3] = 0
        out.append(((y0 + y1) / 2, (x0 + x1) / 2, tight(Image.fromarray(piece))))
    return out


def in_rows(objs, rows):
    """Sort objects into `rows` rows (by centre y), each left to right."""
    objs = sorted(objs, key=lambda o: o[0])
    ys = np.array([o[0] for o in objs])
    cuts = sorted(np.argsort(np.diff(ys))[-(rows - 1):] + 1) if rows > 1 else []
    groups, start = [], 0
    for c in list(cuts) + [len(objs)]:
        groups.append(sorted(objs[start:c], key=lambda o: o[1]))
        start = c
    return [o[2] for g in groups for o in g]


def tight(p, thr=6):
    a = np.array(p)[..., 3]
    ys, xs = np.where(a > thr)
    return p.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def square(p, size, fill=1.0):
    s = int(max(p.size) / fill)
    sq = Image.new("RGBA", (s, s))
    sq.alpha_composite(p, ((s - p.size[0]) // 2, (s - p.size[1]) // 2))
    return sq.resize((size, size), Image.LANCZOS)


def whiten(p):
    a = np.array(p)
    a[..., :3] = 255
    return Image.fromarray(a)


# ------------------------------------------------------------------ tiles
def backing(cap):
    arr = np.array(cap).reshape(-1, 4)
    opaque = arr[arr[:, 3] > 250][:, :3]
    lum = opaque.mean(axis=1)
    return tuple(int(c) for c in np.median(opaque[lum < np.percentile(lum, 4)], axis=0))


TQ = 3.0
TPX = T / TQ
TSIDE = 2.6
TR = TSIDE / 3 ** 0.5
TIN = TR / 2


def tri_points(r):
    return [(math.cos(math.radians(a)) * r * TPX + T / 2, T / 2 - math.sin(math.radians(a)) * r * TPX) for a in (60, 180, 300)]


def tiles(theme):
    sq_cap, tri_cap = in_rows(objects(load("board_" + theme)), 1)[:2]
    edge = backing(sq_cap)
    # square: the cap sits on a backing of its own darkest rim colour so neighbouring tiles leave a thin seam
    cap = sq_cap.resize((int(T * 0.93), int(T * 0.93)), Image.LANCZOS)
    base = Image.new("RGBA", (T, T), edge + (255,))
    base.alpha_composite(cap, ((T - cap.size[0]) // 2,) * 2)
    save(base, "Board", theme.capitalize(), "board_tile.png")
    # triangle: GPT draws it pointing up; the game's tile has a vertex pointing left (-X)
    cap = tri_cap.rotate(90, expand=True, resample=Image.BICUBIC)
    k = min(TR * 1.5 * TPX * 0.93 / cap.size[0], TSIDE * TPX * 0.93 / cap.size[1])
    cap = cap.resize((int(cap.size[0] * k), int(cap.size[1] * k)), Image.LANCZOS)
    base = Image.new("RGBA", (T, T))
    ImageDraw.Draw(base).polygon(tri_points(TR), fill=edge + (255,))
    ca = np.array(cap)[..., 3].astype(np.float64)
    yy, xx = np.mgrid[0:ca.shape[0], 0:ca.shape[1]]
    gx, gy = (xx * ca).sum() / ca.sum(), (yy * ca).sum() / ca.sum()
    base.alpha_composite(cap, (int(round(T / 2 - gx)), int(round(T / 2 - gy))))
    mask = Image.new("L", (T, T))
    ImageDraw.Draw(mask).polygon(tri_points(TR), fill=255)
    ba = np.array(base); ba[..., 3] = np.minimum(ba[..., 3], np.array(mask))
    save(Image.fromarray(ba), "Board", theme.capitalize(), "board_tri.png")
    return edge


# ------------------------------------------------------------------ wires
def core_of(img, lum=246):
    """The bright centre of a tube: pixels that are almost pure white and opaque."""
    a = np.array(img).astype(np.float32)
    bright = (a[..., :3].mean(axis=2) >= lum) & (a[..., 3] > 235)
    soft = ndimage.gaussian_filter(bright.astype(np.float32), 0.8)
    out = np.zeros_like(a)
    out[..., :3] = 255
    out[..., 3] = np.clip(soft * 255, 0, 255)
    return Image.fromarray(out.astype(np.uint8))


def wire_parts(theme):
    src = "wire_neon" if theme == "dark" else "wire_solid"
    tube, joint, cap = in_rows(objects(load(src)), 1)[:3]
    w, h = tube.size
    mid = tight(tube.crop((int(w * 0.2), 0, int(w * 0.8), h)))
    # rounded tip of the third piece (its right end), flipped so it points left: used where an arm starts at the centre
    cw, ch = cap.size
    tip = cap.crop((cw - int(ch * 1.1), 0, cw, ch)).transpose(Image.FLIP_LEFT_RIGHT)
    tip = tip.resize((tip.size[0], mid.size[1]), Image.LANCZOS)
    return mid, joint, tip


def arm_piece(mid, tip, length, thick):
    """An arm from the centre to the edge: GPT's rounded tip at the centre end, straight tube after it."""
    tl = max(1, int(tip.size[0] * thick / tip.size[1]))
    piece = Image.new("RGBA", (length, thick))
    piece.alpha_composite(mid.resize((max(1, length - tl // 2), thick), Image.LANCZOS), (tl // 2, 0))
    t = tip.resize((tl, thick), Image.LANCZOS)
    # the tip replaces the tube where they overlap
    pa = np.array(piece); ta = np.array(t)
    pa[:, :tl] = ta
    return Image.fromarray(pa)


def compose_square(mid, joint, dirs, with_joint, thick, jsize, tip=None):
    img = Image.new("RGBA", (T, T))
    jt = joint.resize((jsize, jsize), Image.LANCZOS)
    hpad = int(mid.size[1] * thick / max(1, mid.size[1]))

    def strip(full):
        layer = Image.new("RGBA", (T, T))
        if full:
            layer.alpha_composite(mid.resize((T, thick), Image.LANCZOS), (0, T // 2 - thick // 2))
        else:
            ln = T // 2 + thick // 2
            layer.alpha_composite(arm_piece(mid, tip, ln, thick), (T - ln, T // 2 - thick // 2))
        return layer

    horiz = [d for d in dirs if d in (0, 2)]
    vert = [d for d in dirs if d in (1, 3)]
    if len(horiz) == 2:
        img.alpha_composite(strip(True))
    elif horiz:
        l = strip(False)
        img.alpha_composite(l if horiz[0] == 0 else l.transpose(Image.FLIP_LEFT_RIGHT))
    if len(vert) == 2:
        img.alpha_composite(strip(True).transpose(Image.TRANSPOSE).transpose(Image.FLIP_LEFT_RIGHT))
    elif vert:
        l = strip(False).rotate(90, resample=Image.BICUBIC)
        img.alpha_composite(l if vert[0] == 1 else l.transpose(Image.FLIP_TOP_BOTTOM))
    if with_joint:
        img.alpha_composite(jt, ((T - jsize) // 2,) * 2)
    return img


def compose_tri(mid, joint, angles, thick, jsize, tip=None):
    arm_len = int(TIN * TPX) + 2
    arm = arm_piece(mid, tip, arm_len + thick // 2, thick)
    jt = joint.resize((jsize, jsize), Image.LANCZOS)
    img = Image.new("RGBA", (T, T))
    for ang in angles:
        layer = Image.new("RGBA", (T, T))
        layer.alpha_composite(arm, (T // 2 - thick // 2, T // 2 - thick // 2))
        img.alpha_composite(layer.rotate(ang, resample=Image.BICUBIC, center=(T / 2, T / 2)))
    img.alpha_composite(jt, ((T - jsize) // 2,) * 2)
    return img


def wires(theme):
    mid, joint, tip = wire_parts(theme)
    folder = theme.capitalize()
    # the GPT tube includes its glow: scale so the opaque body matches WIRE
    body = (np.array(mid)[..., 3] > 200).any(axis=1).sum()
    thick = int(round(WIRE * mid.size[1] / max(1, body)))
    jbody = (np.array(joint)[..., 3] > 200).any(axis=1).sum()
    jsize = int(round(JOINT * joint.size[1] / max(1, jbody)))
    sq = {"wire_end": ([0], True), "wire_straight": ([0, 2], False), "wire_corner": ([0, 1], True),
          "wire_tee": ([0, 1, 2], True), "wire_cross": ([0, 1, 2, 3], True)}
    for name, (dirs, j) in sq.items():
        img = compose_square(mid, joint, dirs, j, thick, jsize, tip)
        save(img, "Board", folder, name + ".png")
        save(core_of(img), "Board", folder, name + "_core.png")
    tthick = int(round(thick * 0.85)); tj = int(round(jsize * 0.85))
    for name, angles in {"tri_wire_end": [0], "tri_wire_bend": [0, 120], "tri_wire_fork": [0, 120, 240]}.items():
        img = compose_tri(mid, joint, angles, tthick, tj, tip)
        save(img, "Board", folder, name + ".png")
        save(core_of(img), "Board", folder, name + "_core.png")


# ------------------------------------------------------------------ sources and lamps
NODE_NAMES = ["source_outline", "source_center", "glow_halo", "lamp_ring", "lamp_core_on", "lamp_core_off"]


def nodes(theme):
    parts = in_rows(objects(load("nodes_" + theme)), 2)[:6]
    # one shared scale so the stacked layers keep their relative sizes; the ring fills 60% of a 256 sprite
    ref = max(parts[3].size)
    size = 256
    k = size * 0.6 / ref
    for p, name in zip(parts, NODE_NAMES):
        p = p.resize((max(1, int(p.size[0] * k)), max(1, int(p.size[1] * k))), Image.LANCZOS)
        c = Image.new("RGBA", (size, size))
        c.alpha_composite(p, ((size - p.size[0]) // 2, (size - p.size[1]) // 2))
        save(c, "Board", theme.capitalize(), name + ".png")


# ------------------------------------------------------------------ UI
UI_NAMES = ["ui_button", "ui_card", "ui_circle", "ui_pill", "ui_square", "ui_bar", "ui_card_shadow"]


def ui_shapes():
    img = clean(load("ui_shapes"))
    found = objects(img, thr=40)
    # match by aspect ratio rather than position
    want = {"ui_button": 4.0, "ui_card": 1.5, "ui_circle": 1.0, "ui_pill": 2.2, "ui_square": 1.0, "ui_bar": 12.0}
    pieces = [(o[2], o[2].size[0] / o[2].size[1]) for o in found]
    shadow = max(pieces, key=lambda p: (np.array(p[0])[..., 3] < 200).mean() if p[0].size[0] > 200 else 0)
    rest = [p for p in pieces if p is not shadow]
    # two near-square pieces: the larger one with the rounder outline is the circle
    squares = sorted([p for p in rest if abs(p[1] - 1) < 0.25], key=lambda p: -corner_fill(p[0]))
    named = {}
    if len(squares) >= 2:
        named["ui_square"], named["ui_circle"] = squares[0][0], squares[-1][0]
    for name in ("ui_button", "ui_card", "ui_pill", "ui_bar"):
        cands = [p for p in rest if abs(p[1] - 1) >= 0.25 and p[0] not in named.values()]
        best = min(cands, key=lambda p: abs(math.log(p[1] / want[name])))
        named[name] = best[0]
    named["ui_card_shadow"] = shadow[0]
    for name, p in named.items():
        p = p if name == "ui_card_shadow" else whiten(p)
        save(p, "UI", name + ".png")
    return named


def corner_fill(p):
    a = np.array(p)[..., 3]
    h, w = a.shape
    c = a[: h // 8, : w // 8]
    return (c > 128).mean()


ICON_NAMES = ["icon_back", "icon_settings", "icon_pause", "icon_help", "icon_close",
              "icon_hint", "icon_restart", "icon_undo", "icon_play", "icon_next",
              "icon_levels", "icon_home", "icon_lock", "icon_star_full", "icon_star_empty",
              "icon_trophy", "icon_sound", "icon_music", "icon_vibration", "icon_check"]


def icons():
    img = clean(load("icons"))
    a = np.array(img)
    m = a[..., 3] > 30
    # grid from the occupied columns/rows: 5 x 4 cells
    def bands(occ, n):
        runs, s = [], None
        for i, o in enumerate(list(occ) + [False]):
            if o and s is None: s = i
            if not o and s is not None: runs.append([s, i]); s = None
        while len(runs) > n:
            g = [runs[i + 1][0] - runs[i][1] for i in range(len(runs) - 1)]
            i = int(np.argmin(g)); runs[i][1] = runs[i + 1][1]; del runs[i + 1]
        return runs
    cols = bands(m.any(axis=0), 5)
    rows = bands(m.any(axis=1), 4)
    k = 0
    for r in rows:
        for c in cols:
            p = tight(img.crop((c[0], r[0], c[1], r[1])), thr=30)
            save(square(whiten(p), 128, fill=0.86), "UI", "Icons", ICON_NAMES[k] + ".png")
            k += 1


SHAPE_NAMES = ["shape_cube", "shape_tetra", "shape_octa", "shape_cylinder", "shape_dodeca", "shape_sphere", "shape_megamix"]


def shape_icons(theme):
    parts = in_rows(objects(clean(load("shapes_" + theme)), merge=6), 1)
    if len(parts) != 7:
        print("shapes_" + theme, "found", len(parts), "pieces")
    for p, n in zip(parts, SHAPE_NAMES):
        save(square(p, 384, fill=0.94), "UI", theme.capitalize(), n + ".png")


def mascot():
    parts = in_rows(objects(clean(load("mascot"))), 1)
    for p, n in zip(parts, ["mascot_idle", "mascot_win", "mascot_think", "mascot_sleep"]):
        save(square(p, 512, fill=0.98), "UI", n + ".png")


def logo():
    im = load("logo")
    a = np.array(im).astype(np.int32)
    # the logo sheet came back on a flat near-white page: flood the page from the borders and make it transparent
    bg = (a[..., :3].min(axis=2) > 228) & ((a[..., :3].max(axis=2) - a[..., :3].min(axis=2)) < 14)
    lab, _ = ndimage.label(bg)
    border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
    page = np.isin(lab, list(border))
    dist = ndimage.distance_transform_edt(~page)
    alpha = np.clip(dist / 2.0, 0, 1)            # 2 px soft edge where the page meets the art
    out = a.copy()
    out[..., 3] = (alpha * 255).astype(np.int32)
    img = Image.fromarray(out.astype(np.uint8))
    mark, icon = in_rows(objects(img), 1)[:2]
    save(square(mark, 1024, fill=0.96), "UI", "logo_mark.png")
    save(square(icon, 1024, fill=1.0), "UI", "app_icon.png")


if __name__ == "__main__":
    what = sys.argv[3:] or ["tiles", "wires", "nodes", "ui", "icons", "shapes", "mascot", "logo"]
    for theme in ("dark", "light"):
        if "tiles" in what: print(theme, "tile seam colour", tiles(theme))
        if "wires" in what: wires(theme)
        if "nodes" in what: nodes(theme)
        if "shapes" in what: shape_icons(theme)
    if "ui" in what: ui_shapes()
    if "icons" in what: icons()
    if "mascot" in what: mascot()
    if "logo" in what: logo()
    print("done")
