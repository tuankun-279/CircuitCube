"""The Vietnamese Expert word XUẤT SẮC as letter sprites, from the approved mockup (canvas board FT4_2).
Audiowide has no stacked tone marks, so Ấ and Ắ are Â and Ă with the acute drawn over the hat, as in the mockup.
Prints the letter centres (canvas units from the word centre) for the prefab builder."""
import asyncio, os, json
from playwright.async_api import async_playwright
import importlib.util
spec = importlib.util.spec_from_file_location('w', os.path.join(os.path.dirname(__file__), 'render_mockup_word.py'))
S = 2.803 * 1.35
PAD = 90
SIZE = 34   # mockup font size of the Vietnamese word (it is one letter longer than EXPERT)
k = SIZE / 40
STYLE = ("font:400 %dpx Audiowide,sans-serif;color:#3a2300;-webkit-text-stroke:%.2fpx #FFF1C2;paint-order:stroke fill;"
         "text-shadow:0 0 %.1fpx #FFE7A3,0 0 %.1fpx #FFC94D,0 0 %.1fpx #FF9F1C,0 0 %.1fpx #FF8A00aa") % (SIZE, 2.6 * k, 4 * k, 10 * k, 22 * k, 40 * k)
ACC = '<span style="position:relative;display:inline-block">%s<i style="position:absolute;font-style:normal;left:62%%;top:-.42em;font-size:.62em">&#180;</i></span>'
LETTERS = ['X', 'U', ACC % 'Â', 'T', 'S', ACC % 'Ă', 'C']
GAP = 10   # mockup px between the two words
BG = os.environ.get('BG', '23,22,38')
SUFFIX = os.environ.get('SUFFIX', '')

def linear_fix(path):
    src = open(os.path.join(os.path.dirname(__file__), 'render_mockup_word.py')).read()
    ns = {}
    exec(src[src.index('import numpy'):src.index('items[\'fx_star_dot\']')], ns)
    ns['linear_fix'](path)

async def main():
    out = '/tmp/wr/out'
    async with async_playwright() as p:
        b = await p.chromium.launch(executable_path='/usr/bin/google-chrome')
        # where each letter sits in the word, as the mockup lays it out
        pg = await b.new_page(viewport={'width': 314, 'height': 120})
        spans = ''.join('<span class="l" style="display:inline-block%s">%s</span>' % (';margin-left:%dpx' % GAP if i == 4 else '', l) for i, l in enumerate(LETTERS))
        await pg.set_content('<html><body style="margin:0"><div id="w" style="position:absolute;left:0;right:0;top:38px;text-align:center;letter-spacing:3px;%s">%s</div></body></html>' % (STYLE, spans))
        await pg.wait_for_timeout(300)
        xs = await pg.evaluate('()=>[...document.querySelectorAll(".l")].map(e=>{const r=e.getBoundingClientRect();return r.x+r.width/2})')
        mid = (xs[0] + xs[-1]) / 2
        print(json.dumps([round((x - mid) * 2.803, 1) for x in xs]))
        await pg.close()
        for i, l in enumerate(LETTERS):
            for hot in (False, True):
                name = 'expert_vi_%d%s%s' % (i, '_hot' if hot else '', SUFFIX)
                pg = await b.new_page(viewport={'width': 36 + 2 * PAD, 'height': 44 + 2 * PAD}, device_scale_factor=S)
                html = '<div style="position:absolute;left:%dpx;top:%dpx;width:36px;height:44px;display:flex;align-items:center;justify-content:center;%s%s">%s</div>' % (PAD, PAD, STYLE, ';filter:brightness(3)' if hot else '', l)
                await pg.set_content('<html><head><style>html,body{margin:0;background:rgb(%s)}</style></head><body>%s</body></html>' % (BG, html))
                await pg.wait_for_timeout(200)
                await pg.screenshot(path=os.path.join(out, name + '.png'))
                await pg.close()
                linear_fix(os.path.join(out, name + '.png'))
        await b.close()
asyncio.run(main())
