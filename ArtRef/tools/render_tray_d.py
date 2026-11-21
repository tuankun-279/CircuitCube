"""Lamp tray option D (approved mockup, canvas row LampColD) and the play clock, rendered from the mockup CSS.
White sprites, tinted per colour in the game. 1.35 texels per canvas unit (2.77 units per mockup px).
Run: python3 render_tray_d.py [out_dir]. The done glow gets a _light copy (plain alpha) for the light theme."""
import asyncio, os, sys
import numpy as np
from PIL import Image
from playwright.async_api import async_playwright

S = 2.77 * 1.35
OUT = sys.argv[1] if len(sys.argv) > 1 else '/tmp/trayd/out'
DARK = (5, 7, 15)

def to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, 1), 1 / 2.4) - 0.055)

def linear_fix(path, bg):
    """Drawn over bg in sRGB; find per pixel the alpha and colour that give the same result blended linearly."""
    D = to_lin(np.asarray(Image.open(path).convert('RGB')).astype(float) / 255.0)
    B = to_lin(np.array(bg) / 255.0)[None, None, :]
    a1 = np.max(np.where(B > 0, (B - D) / np.maximum(B, 1e-6), 0), axis=2)
    a2 = np.max(np.where(B < 0.999, (D - B) / np.maximum(1 - B, 1e-6), 0), axis=2)
    a = np.clip(np.maximum(np.maximum(a1, a2), 0), 0, 1)
    a[:2, :] = 0; a[-2:, :] = 0; a[:, :2] = 0; a[:, -2:] = 0
    X = D - B + a[..., None] * B
    C = np.where(a[..., None] > 1e-4, X / np.maximum(a[..., None], 1e-4), 0)
    Image.fromarray((np.dstack([to_srgb(np.clip(C, 0, 1)), a]) * 255 + 0.5).astype(np.uint8), 'RGBA').save(path)


ITEMS = {}
def add(name, w, h, html, mode='clear', scale=S): ITEMS[name] = (w, h, html, mode, scale)

# card fill and 1.5 px border, 13 px corners (9-slice, 14 px borders)
add('fx_tray_card', 40, 40, '<i style="position:absolute;left:0;top:0;width:40px;height:40px;border-radius:13px;background:#fff"></i>')
add('fx_tray_border', 40, 40, '<i style="position:absolute;left:0;top:0;width:40px;height:40px;box-sizing:border-box;border-radius:13px;border:1.5px solid #fff"></i>')
# done: a second 1.5 px ring outside the border, 16 px glow out, 10 px glow in (9-slice, 24 + 14 px borders)
DONE = '<i style="position:absolute;left:24px;top:24px;width:40px;height:40px;box-sizing:border-box;border-radius:13px;box-shadow:0 0 0 1.5px #fff,0 0 16px rgba(255,255,255,.8),inset 0 0 10px rgba(255,255,255,.35)"></i>'
add('fx_tray_done', 88, 88, DONE, 'glow')
add('fx_tray_done_light', 88, 88, DONE, 'clear')
# colour symbols, 10 px glyphs as the mockup draws them (Inter, then the system fallback)
for key, ch in (('r', '●'), ('b', '■'), ('y', '▲'), ('p', '◆'), ('o', '⬟'), ('g', '✚'), ('w', '★')):
    add('fx_sym_' + key, 14, 14, '<div style="position:absolute;left:0;top:0;width:14px;height:14px;display:flex;align-items:center;justify-content:center;font:10px Inter,sans-serif;color:#fff">%s</div>' % ch)
# the clock in the HUD line and the result card (a TextMeshPro sprite, 24 px art drawn big)
add('text_clock', 24, 24, '<svg style="position:absolute;left:0;top:0" width="24" height="24" viewBox="0 0 24 24"><circle cx="12" cy="13" r="8" fill="none" stroke="#fff" stroke-width="2.4"/><path d="M12 9v4.5l3 2M9.5 2.5h5" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/></svg>', 'clear', 4)


async def render(b, name, w, h, html, mode, scale):
    pg = await b.new_page(viewport={'width': w, 'height': h}, device_scale_factor=scale)
    body = 'rgb(%d,%d,%d)' % DARK if mode == 'glow' else 'transparent'
    await pg.set_content('<html><head><link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;800&display=swap" rel="stylesheet"><style>html,body{margin:0;background:%s}</style></head><body>%s</body></html>' % (body, html))
    await pg.wait_for_timeout(300)
    path = os.path.join(OUT, name + '.png')
    await pg.screenshot(path=path, omit_background=mode != 'glow')
    if mode == 'glow': linear_fix(path, DARK)
    await pg.close()


async def main():
    os.makedirs(OUT, exist_ok=True)
    async with async_playwright() as p:
        b = await p.chromium.launch(executable_path='/usr/bin/google-chrome')
        for name, (w, h, html, mode, scale) in ITEMS.items(): await render(b, name, w, h, html, mode, scale)
        await b.close()

if __name__ == '__main__':
    asyncio.run(main())
