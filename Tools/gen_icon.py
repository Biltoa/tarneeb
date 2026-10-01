#!/usr/bin/env python3
"""Game icon: Assets/Core Game Assets/Art/Icon/icon_1024.png (+ WebGL template favicons). A gold crown over a gold spade on the table's
emerald felt, ringed in gold, with two cards fanned behind. Uses the project's own spade and crown sprites. Needs pillow."""
import os, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
RES = os.path.join(ROOT, 'Assets', 'Core Game Assets', 'Resources', 'UI')
OUT = os.path.join(ROOT, 'Assets', 'Core Game Assets', 'Art', 'Icon')
TPL = os.path.join(ROOT, 'Assets', 'WebGLTemplates', 'Tarneeb')
S = 1024

def gold_gradient(size, top=(255, 248, 216), mid=(255, 228, 154), bot=(220, 170, 70)):
    g = Image.new('RGB', size)
    px = g.load()
    for y in range(size[1]):
        t = y / (size[1] - 1)
        c = [int(top[i] + (mid[i] - top[i]) * (t / .5)) if t < .5 else int(mid[i] + (bot[i] - mid[i]) * ((t - .5) / .5)) for i in range(3)]
        for x in range(size[0]): px[x, y] = tuple(c)
    return g

def tint_gold(sprite):
    sprite = sprite.convert('RGBA')
    a = sprite.split()[3]
    g = gold_gradient(sprite.size).convert('RGBA'); g.putalpha(a)
    return g

def shadowed(layer, blur=18, dy=14, alpha=150):
    sh = Image.new('RGBA', layer.size, (0, 0, 0, 0))
    a = layer.split()[3].point(lambda v: v * alpha // 255)
    sh.putalpha(a); sh = ImageChops.offset(sh, 0, dy).filter(ImageFilter.GaussianBlur(blur))
    return Image.alpha_composite(sh, layer)

# felt background: radial emerald
bg = Image.new('RGB', (S, S))
px = bg.load()
for y in range(S):
    for x in range(S):
        d = math.hypot(x - S / 2, y - S * 0.46) / (S * 0.72)
        t = min(1, d)
        px[x, y] = (int(14 + (0 - 14) * t * .6), int(86 - 62 * t), int(70 - 40 * t))
img = bg.convert('RGBA')
d = ImageDraw.Draw(img)
# gold double ring
for r, w, col in ((470, 14, (231, 188, 93, 255)), (446, 5, (255, 240, 190, 200))):
    d.ellipse([S / 2 - r, S / 2 - r, S / 2 + r, S / 2 + r], outline=col, width=w)

def card(angle, face_symbol):
    w, h = 300, 430
    c = Image.new('RGBA', (w + 40, h + 40), (0, 0, 0, 0))
    cd = ImageDraw.Draw(c)
    cd.rounded_rectangle([20, 20, 20 + w, 20 + h], 34, fill=(253, 248, 232, 255), outline=(231, 188, 93, 255), width=6)
    sym = Image.open(os.path.join(RES, face_symbol)).convert('RGBA').resize((150, 150), Image.LANCZOS)
    c.alpha_composite(sym, (20 + (w - 150) // 2, 20 + (h - 150) // 2))
    return shadowed(c.rotate(angle, expand=True, resample=Image.BICUBIC), 14, 10, 130)

for ang, sym, cx in ((24, 'suit_heart.png', 700), (-24, 'suit_club.png', 324)):
    c = card(ang, sym)
    img.alpha_composite(c, (int(cx - c.width / 2), int(590 - c.height / 2)))

spade = tint_gold(Image.open(os.path.join(RES, 'suit_spade_w.png')).resize((520, 520), Image.LANCZOS))
img.alpha_composite(shadowed(spade, 20, 18, 170), (S // 2 - 260, 420))
crown = tint_gold(Image.open(os.path.join(RES, 'icon_crown.png')).resize((300, 300), Image.LANCZOS))
img.alpha_composite(shadowed(crown, 14, 10, 150), (S // 2 - 150, 170))

os.makedirs(OUT, exist_ok=True)
img.convert('RGB').save(os.path.join(OUT, 'icon_1024.png'), optimize=True)
# transparent-corner (rounded) copy for the favicon and apple-touch icon
for name, size in (('favicon.png', 64), ('apple-touch-icon.png', 180)):
    img.convert('RGB').resize((size, size), Image.LANCZOS).save(os.path.join(TPL, name), optimize=True)
print('wrote icons')
