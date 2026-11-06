"""Contact sheet + a small board assembled from the V2 sprites the way the game will layer and tint them."""
import os, glob
import numpy as np
from PIL import Image, ImageDraw

V = "V2"
COL = {"red": (255, 77, 109), "blue": (61, 169, 255), "yellow": (255, 225, 77), "purple": (199, 125, 255), "green": (61, 255, 162)}
COL_L = {"red": (240, 67, 95), "blue": (47, 143, 232), "yellow": (239, 163, 0), "purple": (161, 91, 235), "green": (19, 176, 122)}


def tint(img, rgb, alpha=1.0):
    a = np.array(img).astype(np.float32)
    a[..., 0] *= rgb[0] / 255; a[..., 1] *= rgb[1] / 255; a[..., 2] *= rgb[2] / 255; a[..., 3] *= alpha
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def sprite(theme, name):
    return Image.open(os.path.join(V, "Board", theme, name + ".png")).convert("RGBA")


def board(theme, bg):
    cols = COL if theme == "Dark" else COL_L
    S = 160
    W = S * 4
    img = Image.new("RGBA", (W, W), bg)
    tile = sprite(theme, "board_tile").resize((S, S), Image.LANCZOS)
    for y in range(4):
        for x in range(4):
            img.alpha_composite(tile, (x * S, y * S))
    # a red path: source (0,0) -> right -> down -> lamp; a blue straight; a yellow tee
    def put(name, x, y, rot, c, core=True):
        w = sprite(theme, name).rotate(rot).resize((S, S), Image.LANCZOS)
        img.alpha_composite(tint(w, c), (x * S, y * S))
        if core:
            k = sprite(theme, name + "_core").rotate(rot).resize((S, S), Image.LANCZOS)
            img.alpha_composite(k, (x * S, y * S))

    def node(kind, x, y, c, on=True):
        def layer(n, t=None, al=1.0, k=0.62):
            s = sprite(theme, n)
            s = s.resize((int(S * k / 0.6 * 0.6), int(S * k / 0.6 * 0.6)), Image.LANCZOS)
            s = tint(s, t, al) if t else s
            img.alpha_composite(s, (x * S + (S - s.size[0]) // 2, y * S + (S - s.size[1]) // 2))
        if kind == "src":
            layer("glow_halo", c, 0.35 if theme == "Dark" else 0.2)
            layer("source_outline", c)
            layer("source_center")
        else:
            if on: layer("glow_halo", c, 0.4 if theme == "Dark" else 0.2)
            layer("lamp_ring", c)
            layer("lamp_core_on" if on else "lamp_core_off", None if theme == "Dark" or not on else None)
    put("wire_end", 0, 0, 0, cols["red"]); node("src", 0, 0, cols["red"])
    put("wire_corner", 1, 0, 180 + 90, cols["red"])
    put("wire_straight", 1, 1, 90, cols["red"])
    put("wire_end", 1, 2, 90, cols["red"]); node("lamp", 1, 2, cols["red"], True)
    put("wire_end", 3, 0, 270, cols["yellow"]); node("src", 3, 0, cols["yellow"])
    put("wire_tee", 3, 1, 90, cols["yellow"])
    put("wire_end", 2, 1, 0, cols["yellow"]); node("lamp", 2, 1, cols["yellow"], True)
    put("wire_end", 3, 2, 90, cols["yellow"]); node("lamp", 3, 2, cols["yellow"], False)
    put("wire_end", 0, 3, 0, cols["blue"]); node("src", 0, 3, cols["blue"])
    put("wire_straight", 1, 3, 0, cols["blue"])
    put("wire_straight", 2, 3, 0, cols["blue"])
    put("wire_end", 3, 3, 180, cols["blue"]); node("lamp", 3, 3, cols["blue"], False)
    return img


def tri_board(theme, bg):
    S = 220
    img = Image.new("RGBA", (S * 3, S), bg)
    t = sprite(theme, "board_tri").resize((S, S), Image.LANCZOS)
    for i, (n, c) in enumerate([("tri_wire_end", COL["purple"]), ("tri_wire_bend", COL["green"]), ("tri_wire_fork", COL["blue"])]):
        img.alpha_composite(t, (i * S, 0))
        w = sprite(theme, n).resize((S, S), Image.LANCZOS)
        img.alpha_composite(tint(w, c), (i * S, 0))
        img.alpha_composite(sprite(theme, n + "_core").resize((S, S), Image.LANCZOS), (i * S, 0))
    return img


def contact(paths, bg, cell=150, cols=10):
    rows = (len(paths) + cols - 1) // cols
    img = Image.new("RGBA", (cols * cell, rows * (cell + 18)), bg)
    d = ImageDraw.Draw(img)
    for i, p in enumerate(paths):
        s = Image.open(p).convert("RGBA")
        s.thumbnail((cell - 10, cell - 10))
        x, y = (i % cols) * cell, (i // cols) * (cell + 18)
        img.alpha_composite(s, (x + (cell - s.size[0]) // 2, y + (cell - s.size[1]) // 2))
        d.text((x + 4, y + cell + 2), os.path.basename(p)[:-4][:22], fill=(150, 150, 150, 255))
    return img


if __name__ == "__main__":
    dark_bg, light_bg = (10, 12, 20, 255), (238, 241, 247, 255)
    out = Image.new("RGBA", (1340, 900), (60, 60, 70, 255))
    out.alpha_composite(board("Dark", dark_bg), (10, 10))
    out.alpha_composite(board("Light", light_bg), (690, 10))
    out.alpha_composite(tri_board("Dark", dark_bg).resize((440, 147)), (10, 680))
    out.alpha_composite(tri_board("Light", light_bg).resize((440, 147)), (690, 680))
    out.save("pv_board.png")
    ui = sorted(glob.glob(V + "/UI/*.png")) + sorted(glob.glob(V + "/UI/Icons/*.png"))
    contact(ui, (20, 24, 38, 255)).save("pv_ui_dark.png")
    shapes = sorted(glob.glob(V + "/UI/Dark/*.png")) + sorted(glob.glob(V + "/UI/Light/*.png"))
    contact(shapes, (128, 134, 150, 255), cols=7).save("pv_shapes.png")
    nodes = [p for p in sorted(glob.glob(V + "/Board/*/*.png")) if "wire" not in p]
    contact(nodes, (90, 96, 112, 255), cols=8).save("pv_nodes.png")
