"""FTUE sprites rendered from the approved mockup CSS (canvas boards FT0-FTEnd, FTZoom), 1.35 texels per canvas unit.
Run: python3 render_ftue.py [out_dir]. Glow sprites get a _light copy baked for the light theme."""
import asyncio, os, sys
import numpy as np
from PIL import Image
from playwright.async_api import async_playwright

S = 2.803 * 1.35
OUT = sys.argv[1] if len(sys.argv) > 1 else '/tmp/ftue/out'
DARK, LIGHT = (5, 7, 15), (240, 246, 250)

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

# name: (w, h, html, mode)  mode 'clear' = transparent page, 'glow' = baked over the theme backgrounds
ITEMS = {}
def add(name, w, h, html, mode='clear'): ITEMS[name] = (w, h, html, mode)

# note card (9-slice; the top-right corner is the small one, towards the mascot)
add('ftue_note', 172, 152, '<i style="position:absolute;left:36px;top:28px;width:100px;height:80px;border-radius:16px 4px 16px 16px;background:#e9f7fa;box-shadow:0 8px 24px #000a"></i>')
# dim with a hole round the target (9-slice frame; the screen outside is dimmed by plain quads of the same colour)
add('ftue_hole', 180, 180, '<svg style="position:absolute;left:0;top:0" width="180" height="180" viewBox="0 0 180 180"><path fill-rule="evenodd" fill="rgba(0,0,0,.733)" d="M0 0H180V180H0Z M74 60H106A14 14 0 0 1 120 74V106A14 14 0 0 1 106 120H74A14 14 0 0 1 60 106V74A14 14 0 0 1 74 60Z"/><rect x="61" y="61" width="58" height="58" rx="13" fill="none" stroke="#46e0f0" stroke-width="2"/></svg>')
add('ftue_ring', 110, 110, '<i style="position:absolute;left:24px;top:24px;width:62px;height:62px;box-sizing:border-box;border-radius:50%;border:3px solid #46e0f0;box-shadow:0 0 16px #46e0f0,inset 0 0 10px #46e0f066"></i>', 'glow')
add('ftue_hand', 56, 64, '<svg style="position:absolute;left:6px;top:6px" width="44" height="52" viewBox="0 0 44 52"><circle cx="10" cy="8" r="9" fill="none" stroke="#dffbff" stroke-width="2" opacity=".7"/><path d="M8 8v24l-4-4c-3-3-7 1-4 4l11 13c2 3 5 4 9 4h8c6 0 10-4 10-10V24c0-3-4-3-4 0v-2c0-3-4-3-4 0v-2c0-3-4-3-4 0v-1c0-3-4-3-4 0V8c0-4-6-4-6 0z" fill="#e9f7fa" stroke="#0b1c22" stroke-width="2" stroke-linejoin="round"/></svg>')
add('ftue_swipe', 180, 50, '<svg style="position:absolute;left:5px;top:5px" width="170" height="40" viewBox="0 0 170 40"><defs><linearGradient id="sg"><stop offset="0" stop-color="#46e0f0" stop-opacity="0"/><stop offset="1" stop-color="#46e0f0"/></linearGradient></defs><path d="M6 20H150" stroke="url(#sg)" stroke-width="5" stroke-linecap="round"/><path d="M140 8l18 12-18 12" fill="none" stroke="#46e0f0" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/></svg>')
# welcome
add('ftue_halo', 280, 280, '<i style="position:absolute;left:0;top:0;width:280px;height:280px;border-radius:50%;background:radial-gradient(circle,#46e0f055,#46e0f000 65%)"></i>', 'glow')
add('ftue_ray', 8, 48, '<i style="position:absolute;left:2px;top:2px;width:4px;height:44px;border-radius:2px;background:linear-gradient(#dffbff,#46e0f000)"></i>')
add('ftue_frame', 280, 280, '<i style="position:absolute;left:60px;top:60px;width:160px;height:160px;box-sizing:border-box;border-radius:42px;background:#06080f;border:3px solid #46B8C6;box-shadow:0 0 50px #46B8C688"></i>', 'glow')
add('ftue_bubble', 140, 140, '<i style="position:absolute;left:40px;top:30px;width:60px;height:60px;border-radius:20px;background:#e9f7fa;box-shadow:0 10px 30px #000a"></i>')
add('ftue_tail', 24, 14, '<i style="position:absolute;left:2px;top:2px;width:0;height:0;border:10px solid transparent;border-top:0;border-bottom-color:#e9f7fa"></i>')
# the welcome title (Audiowide); the light theme gets teal letters
LOGO = '<div style="position:absolute;left:0;top:0;width:300px;height:80px;display:flex;align-items:center;justify-content:center;font:400 30px Audiowide,sans-serif;letter-spacing:2px;color:%s;text-shadow:0 0 12px #46e0f0">CIRCUIT CUBE</div>'
add('ftue_logo', 300, 80, LOGO % '#dffbff', 'dark')
add('ftue_logo_light', 300, 80, LOGO % '#1E9AAA', 'light')
# end
add('ftue_dash', 46, 30, '<i style="position:absolute;left:12px;top:12px;width:22px;height:6px;border-radius:3px;background:#3DFFA2;box-shadow:0 0 6px #3DFFA2"></i>', 'glow')
# zoom buttons (white, tinted by the theme)
add('icon_minus', 26, 26, '<svg style="position:absolute;left:1px;top:1px" width="24" height="24" viewBox="0 0 24 24"><path d="M5 12h14" stroke="#fff" stroke-width="2.8" stroke-linecap="round"/></svg>')
add('icon_fit', 26, 26, '<svg style="position:absolute;left:1px;top:1px" width="24" height="24" viewBox="0 0 24 24"><path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/></svg>')

async def render(b, name, w, h, html, bg, suffix):
    pg = await b.new_page(viewport={'width': w, 'height': h}, device_scale_factor=S)
    body = 'transparent' if bg is None else 'rgb(%d,%d,%d)' % bg
    await pg.set_content('<html><head><style>html,body{margin:0;background:%s}</style></head><body>%s</body></html>' % (body, html))
    await pg.wait_for_timeout(200)
    path = os.path.join(OUT, name + suffix + '.png')
    await pg.screenshot(path=path, omit_background=bg is None)
    if bg is not None: linear_fix(path, bg)
    await pg.close()

async def main():
    os.makedirs(OUT, exist_ok=True)
    async with async_playwright() as p:
        b = await p.chromium.launch(executable_path='/usr/bin/google-chrome')
        for name, (w, h, html, mode) in ITEMS.items():
            if mode == 'clear': await render(b, name, w, h, html, None, '')
            elif mode == 'dark': await render(b, name, w, h, html, DARK, '')
            elif mode == 'light': await render(b, name, w, h, html, LIGHT, '')
            else:
                await render(b, name, w, h, html, DARK, '')
                await render(b, name, w, h, html, LIGHT, '_light')
        await b.close()
asyncio.run(main())
