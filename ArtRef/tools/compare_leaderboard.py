"""Side-by-side sheets of the leaderboard mockup and the Unity screenshots.

Usage: py -3.12 ArtRef/tools/compare_leaderboard.py [theme]   (theme: dark or light, default dark)

Reads ArtRef/mockup/leaderboard/lb_<Screen>.png and unity/lb_<Screen>_<theme>.png, writes
unity/compare_<theme>.png: one column per screen, the mockup on top and the game under it, both at the
same width.
"""
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "mockup", "leaderboard"))
SCREENS = ["Main", "Weekly", "WeeklyBoard", "ShapeBoard", "Result", "Name"]
W = 360


def fit(img):
    h = round(img.height * W / img.width)
    return img.convert("RGB").resize((W, h), Image.LANCZOS)


def main():
    theme = sys.argv[1] if len(sys.argv) > 1 else "dark"
    cols = []
    for s in SCREENS:
        mock = os.path.join(ROOT, "lb_" + s + ".png")
        game = os.path.join(ROOT, "unity", "lb_" + s + "_" + theme + ".png")
        if not os.path.exists(game):
            continue
        cols.append((s, fit(Image.open(mock)), fit(Image.open(game))))
    if not cols:
        print("no screenshots")
        return
    h = max(m.height + g.height for _, m, g in cols) + 40
    sheet = Image.new("RGB", (len(cols) * (W + 16) + 16, h), "#202020")
    d = ImageDraw.Draw(sheet)
    for i, (s, m, g) in enumerate(cols):
        x = 16 + i * (W + 16)
        d.text((x, 6), s, fill="#FFFFFF")
        sheet.paste(m, (x, 24))
        sheet.paste(g, (x, 24 + m.height + 8))
    out = os.path.join(ROOT, "unity", "compare_" + theme + ".png")
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
