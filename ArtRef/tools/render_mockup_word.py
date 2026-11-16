import asyncio, math
from playwright.async_api import async_playwright
S=2.803*1.35   # mockup px -> texture px (1.35 texels per canvas unit, the 1440 px phone)
PAD=55
FONT=""
S3="font:400 40px Audiowide,sans-serif;color:#3a2300;-webkit-text-stroke:2.6px #FFF1C2;paint-order:stroke fill;text-shadow:0 0 4px #FFE7A3,0 0 10px #FFC94D,0 0 22px #FF9F1C,0 0 40px #FF8A00aa"
items={}
for ch in "EXPRT":
    items['expert_'+ch]=(36+2*PAD,44+2*PAD,f'<div style="position:absolute;left:{PAD}px;top:{PAD}px;width:36px;height:44px;display:flex;align-items:center;justify-content:center;{S3}">{ch}</div>')
    items['expert_'+ch+'_hot']=(36+2*PAD,44+2*PAD,f'<div style="position:absolute;left:{PAD}px;top:{PAD}px;width:36px;height:44px;display:flex;align-items:center;justify-content:center;{S3};filter:brightness(3)">{ch}</div>')
# burst core (36px + 24px spread + 50px blur)
P2=80
items['fx_boom_core']=(36+2*P2,36+2*P2,f'<i style="position:absolute;left:{P2}px;top:{P2}px;width:36px;height:36px;border-radius:50%;background:radial-gradient(circle,#fff 0 35%,#FFF1C2 50%,#FFC94D 70%,transparent 75%);box-shadow:0 0 50px 24px #FFE7A3aa"></i>')
P3=24
items['fx_boom_ring']=(40+2*P3,40+2*P3,f'<i style="position:absolute;left:{P3}px;top:{P3}px;width:40px;height:40px;box-sizing:border-box;border-radius:50%;border:3px solid #FFE7A3;box-shadow:0 0 18px #FFC94D"></i>')
items['fx_boom_dot']=(6+2*20,6+2*20,'<i style="position:absolute;left:20px;top:20px;width:6px;height:6px;border-radius:50%;background:#FFF1C2;box-shadow:0 0 10px 3px #FFC94D"></i>')
items['fx_ping']=(48+2*4,56+2*4,'<i style="position:absolute;left:4px;top:4px;width:48px;height:56px;box-sizing:border-box;border-radius:50%;border:2px solid #FFE7A3"></i>')
items['fx_word_shine']=(60,56,'<i style="position:absolute;left:12px;top:0;width:36px;height:56px;background:linear-gradient(90deg,transparent,#fffbe8dd,transparent);transform:skewX(-20deg)"></i>')
async def main():
    async with async_playwright() as p:
        b=await p.chromium.launch(executable_path='/usr/bin/google-chrome',args=['--allow-file-access-from-files'])
        for name,(w,h,html) in items.items():
            pg=await b.new_page(viewport={'width':w,'height':h},device_scale_factor=S)
            await pg.set_content(f'<html><head><style>{FONT}html,body{{margin:0;background:transparent}}</style></head><body>{html}</body></html>')
            await pg.wait_for_timeout(300)
            await pg.screenshot(path=f'/tmp/wr/out/{name}.png',omit_background=True)
            await pg.close()
        # reference: the whole word on the card colour, as in the mockup
        pg=await b.new_page(viewport={'width':314,'height':120},device_scale_factor=2)
        sl=''.join(f'<div style="position:absolute;left:{157+(i-2.5)*40-18}px;top:38px;width:36px;height:44px;display:flex;align-items:center;justify-content:center;{S3}">{c}</div>' for i,c in enumerate("EXPERT"))
        await pg.set_content(f'<html><head><style>{FONT}body{{margin:0;background:#151a2b}}</style></head><body>{sl}</body></html>')
        await pg.wait_for_timeout(300); await pg.screenshot(path='/tmp/wr/out/ref_word.png'); await b.close()
asyncio.run(main())
