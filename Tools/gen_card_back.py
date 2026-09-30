#!/usr/bin/env python3
"""Composes the card back from the Figma design layers (red gradient, cream edge, inner gold border, lattice vector) into one
sprite at 6x design size: Assets/_Project/Resources/Cards/card_back.png.."""
import os
from PIL import Image, ImageDraw
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards', 'card_back.png')
K = 6; SS = 3
W, H = 62 * K, 94 * K
def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
def grad(w, h):
    stops = [(0, (189, 9, 14)), (0.5, (244, 33, 33)), (1, (150, 10, 13))]
    img = Image.new('RGB', (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / (h - 1)
        c = lerp(stops[0][1], stops[1][1], t / 0.5) if t < 0.5 else lerp(stops[1][1], stops[2][1], (t - 0.5) / 0.5)
        d.line([0, y, w, y], fill=c)
    return img
def rr(size, box, r, fill=None, outline=None, width=0):
    m = Image.new('L', (size[0] * SS, size[1] * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([v * SS for v in box], r * SS, fill=255)
    return m.resize(size, Image.LANCZOS)
card = Image.new('RGBA', (W, H), (0, 0, 0, 0))
outer = rr((W, H), [0, 0, W - 1, H - 1], 5 * K)
card.paste(Image.new('RGB', (W, H), (255, 245, 212)), (0, 0), outer)                     # cream edge (1.5 px stroke)
inner = rr((W, H), [1.5 * K, 1.5 * K, W - 1.5 * K - 1, H - 1.5 * K - 1], 4 * K)
card.paste(grad(W, H), (0, 0), inner)
b0 = 5 * K
gold = rr((W, H), [b0, b0, b0 + 52 * K - 1, b0 + 84 * K - 1], 3 * K)                       # inner gold border 1 px
card.paste(Image.new('RGB', (W, H), (255, 176, 136)), (0, 0), gold)
fill = rr((W, H), [b0 + K, b0 + K, b0 + 52 * K - 1 - K, b0 + 84 * K - 1 - K], 2 * K)
card.paste(Image.new('RGB', (W, H), (183, 21, 24)), (0, 0), fill)
# Lattice redrawn procedurally: the Figma vector is off-centre and soft when enlarged. 4 x 6 diamonds fill the inner border exactly.
lm = Image.new('L', (W * SS, H * SS), 0)
ld = ImageDraw.Draw(lm)
x0, y0 = 5 * K + K, 5 * K + K                                # inside the gold border (1 px in)
cw, ch = (52 - 2) * K / 4.0, (84 - 2) * K / 6.0
def diamond(cx, cy, hw, hh, fill):
    ld.polygon([(cx * SS, (cy - hh) * SS), ((cx + hw) * SS, cy * SS), (cx * SS, (cy + hh) * SS), ((cx - hw) * SS, cy * SS)], fill=fill)
for i in range(4):
    for j in range(6):
        cx, cy = x0 + cw * (i + 0.5), y0 + ch * (j + 0.5)
        hw, hh, t = cw * 0.40, ch * 0.40, 0.8 * K * 1.25       # outline thickness measured along the diagonal
        diamond(cx, cy, hw, hh, 255)
        diamond(cx, cy, hw - t, hh - t * ch / cw, 0)
        r = 0.9 * K * SS
        ld.ellipse([cx * SS - r, cy * SS - r, cx * SS + r, cy * SS + r], fill=255)
lat = Image.new('RGBA', (W * SS, H * SS), (255, 176, 136, 0))
lat.putalpha(lm.point(lambda v: int(v * 0.85)))
card.alpha_composite(lat.resize((W, H), Image.LANCZOS))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
card.save(OUT, optimize=True)
print('wrote', OUT, card.size)
