# Lamp tray (option A) parts, from the mockup CSS. Glows are white so the game tints them per colour.
import asyncio, os, numpy as np
from PIL import Image
from playwright.async_api import async_playwright
S=2.77*1.35
def to_lin(c): return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def to_srgb(c): return np.where(c<=0.0031308,c*12.92,1.055*np.power(np.clip(c,0,1),1/2.4)-0.055)
def linear_fix(path,BG):
    D=to_lin(np.asarray(Image.open(path).convert('RGB')).astype(float)/255.0); B=to_lin(np.array(BG)/255.0)[None,None,:]
    a1=np.max(np.where(B>0,(B-D)/np.maximum(B,1e-6),0),axis=2); a2=np.max(np.where(B<0.999,(D-B)/np.maximum(1-B,1e-6),0),axis=2)
    a=np.clip(np.maximum(np.maximum(a1,a2),0),0,1); a[:1,:]=0;a[-1:,:]=0;a[:,:1]=0;a[:,-1:]=0
    X=D-B+a[...,None]*B; C=np.where(a[...,None]>1e-4,X/np.maximum(a[...,None],1e-4),0)
    Image.fromarray((np.dstack([to_srgb(np.clip(C,0,1)),a])*255+0.5).astype(np.uint8),'RGBA').save(path)
# name: (w,h,html,bg for the linear fix or None for plain transparent)
items={
 'fx_seg':(24,11,'<i style="position:absolute;left:2px;top:2px;width:20px;height:7px;border-radius:2px;background:#fff"></i>',None),
 'fx_seg_glow':(32,25,'<i style="position:absolute;left:6px;top:9px;width:20px;height:7px;border-radius:2px;box-shadow:0 0 6px #ffffffaa"></i>','dark'),
 'fx_bar_glow':(40,27,'<i style="position:absolute;left:8px;top:10px;width:24px;height:7px;border-radius:4px;box-shadow:0 0 8px #ffffff"></i>','dark'),
 'fx_tag':(40,20,'<i style="position:absolute;left:2px;top:2px;width:36px;height:16px;box-sizing:border-box;border-radius:8px;background:#1b1430;border:1px solid #2c3550"></i>',None),
 'fx_tag_light':(40,20,'<i style="position:absolute;left:2px;top:2px;width:36px;height:16px;box-sizing:border-box;border-radius:8px;background:#eef0f6;border:1px solid #d5d9e5"></i>',None),
}
BGS={'dark':(23,22,38),'light':(255,255,255)}
async def main():
    async with async_playwright() as p:
        b=await p.chromium.launch(executable_path='/usr/bin/google-chrome')
        for name,(w,h,html,bg) in items.items():
            for theme in (['dark','light'] if bg else [None]):
                pg=await b.new_page(viewport={'width':w,'height':h},device_scale_factor=S)
                back='transparent' if theme is None else 'rgb(%d,%d,%d)'%BGS[theme]
                await pg.set_content('<html><body style="margin:0;background:%s">%s</body></html>'%(back,html))
                out='/tmp/wr/out/%s%s.png'%(name,'_light' if theme=='light' else '')
                await pg.screenshot(path=out,omit_background=theme is None)
                if theme: linear_fix(out,BGS[theme])
                await pg.close()
        await b.close()
asyncio.run(main())
