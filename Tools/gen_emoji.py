#!/usr/bin/env python3
"""Generates the original chat emoji atlas (4x4 grid of 128px cells, 12 used, CC0) into Resources/Emoji/emoji_atlas.png.
Order matches ChatText.EmojiCount / EmojiArt: grin, laugh, wink, cool, sad, angry, wow, think, heart, star, GG, crown.
Run: python3 Tools/gen_emoji.py"""
import math, os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Project', 'Resources', 'Emoji', 'emoji_atlas.png')
S, N = 4, 128                       # supersample, cell size
W = N * S
YEL, YEL_D = (255, 205, 60, 255), (214, 150, 20, 255)
INK, WHITE, RED = (60, 35, 20, 255), (255, 255, 255, 255), (225, 50, 60, 255)

def face(d, base=YEL, edge=YEL_D):
    d.ellipse([6 * S, 6 * S, W - 6 * S, W - 6 * S], fill=base, outline=edge, width=5 * S)

def eyes(d, dy=-14, dx=24, r=8, fill=INK):
    for sx in (-1, 1):
        cx, cy = W / 2 + sx * dx * S, W / 2 + dy * S
        d.ellipse([cx - r * S, cy - r * 1.35 * S, cx + r * S, cy + r * 1.35 * S], fill=fill)

def smile(d, w=44, top=6, h=40, width=7, fill=INK):
    d.arc([W / 2 - w * S, W / 2 + top * S, W / 2 + w * S, W / 2 + (top + h) * S], 15, 165, fill=fill, width=width * S)

def frown(d, w=34, top=34, h=30, width=7):
    d.arc([W / 2 - w * S, W / 2 + top * S, W / 2 + w * S, W / 2 + (top + h) * S], 195, 345, fill=INK, width=width * S)

def brow(d, cx, cy, ang, ln=20, width=6):
    dx, dy = math.cos(math.radians(ang)) * ln * S, math.sin(math.radians(ang)) * ln * S
    d.line([cx - dx, cy - dy, cx + dx, cy + dy], fill=INK, width=width * S)

def star_pts(cx, cy, ro, ri):
    pts = []
    for i in range(10):
        r = ro if i % 2 == 0 else ri
        a = math.radians(-90 + i * 36)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts

def cell(i):
    img = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = W / 2
    if i == 0:      # grin
        face(d); eyes(d)
        d.chord([c - 42 * S, c - 2 * S, c + 42 * S, c + 52 * S], 0, 180, fill=INK)
        d.chord([c - 34 * S, c - 2 * S, c + 34 * S, c + 20 * S], 0, 180, fill=WHITE)
    elif i == 1:    # laugh (closed happy eyes, tears)
        face(d)
        for sx in (-1, 1):
            d.arc([c + sx * 24 * S - 13 * S, c - 24 * S, c + sx * 24 * S + 13 * S, c - 2 * S], 200, 340, fill=INK, width=6 * S)
            d.ellipse([c + sx * 44 * S - 7 * S, c - 6 * S, c + sx * 44 * S + 7 * S, c + 18 * S], fill=(120, 200, 255, 255))
        d.chord([c - 40 * S, c + 2 * S, c + 40 * S, c + 54 * S], 0, 180, fill=INK)
        d.chord([c - 22 * S, c + 28 * S, c + 22 * S, c + 52 * S], 0, 180, fill=(240, 90, 110, 255))
    elif i == 2:    # wink
        face(d)
        cx, cy = c + 24 * S, c - 14 * S
        d.ellipse([cx - 8 * S, cy - 11 * S, cx + 8 * S, cy + 11 * S], fill=INK)
        d.line([c - 38 * S, c - 14 * S, c - 12 * S, c - 14 * S], fill=INK, width=6 * S)
        smile(d)
    elif i == 3:    # cool
        face(d)
        d.rounded_rectangle([c - 50 * S, c - 28 * S, c - 6 * S, c + 2 * S], 10 * S, fill=INK)
        d.rounded_rectangle([c + 6 * S, c - 28 * S, c + 50 * S, c + 2 * S], 10 * S, fill=INK)
        d.line([c - 8 * S, c - 20 * S, c + 8 * S, c - 20 * S], fill=INK, width=5 * S)
        d.line([c - 44 * S, c - 22 * S, c - 32 * S, c - 16 * S], fill=(255, 255, 255, 160), width=3 * S)
        smile(d, w=40, top=10, h=36)
    elif i == 4:    # sad
        face(d); eyes(d, dy=-12)
        brow(d, c - 24 * S, c - 34 * S, -20, 14, 5); brow(d, c + 24 * S, c - 34 * S, 20, 14, 5)
        frown(d)
        d.ellipse([c + 30 * S, c + 2 * S, c + 44 * S, c + 28 * S], fill=(120, 200, 255, 255))
    elif i == 5:    # angry
        face(d, base=(255, 120, 60, 255), edge=(190, 60, 20, 255)); eyes(d, dy=-8, r=7)
        brow(d, c - 24 * S, c - 28 * S, 28, 20, 8); brow(d, c + 24 * S, c - 28 * S, -28, 20, 8)
        frown(d, w=30, top=30, h=26, width=8)
    elif i == 6:    # wow
        face(d); eyes(d, dy=-10, r=9)
        brow(d, c - 24 * S, c - 40 * S, 0, 14, 5); brow(d, c + 24 * S, c - 40 * S, 0, 14, 5)
        d.ellipse([c - 15 * S, c + 16 * S, c + 15 * S, c + 52 * S], fill=INK)
    elif i == 7:    # thinking
        face(d); eyes(d, dy=-10)
        brow(d, c - 24 * S, c - 36 * S, 0, 14, 5); brow(d, c + 24 * S, c - 40 * S, -18, 16, 5)
        d.line([c - 22 * S, c + 34 * S, c + 24 * S, c + 28 * S], fill=INK, width=7 * S)
    elif i == 8:    # heart
        r = W * 0.25
        d.ellipse([c - W * 0.24 - r, W * 0.34 - r, c - W * 0.24 + r, W * 0.34 + r], fill=RED)
        d.ellipse([c + W * 0.24 - r, W * 0.34 - r, c + W * 0.24 + r, W * 0.34 + r], fill=RED)
        d.polygon([(c - W * 0.475, W * 0.43), (c + W * 0.475, W * 0.43), (c, W * 0.92)], fill=RED)
        d.ellipse([c - 36 * S, c - 34 * S, c - 18 * S, c - 20 * S], fill=(255, 255, 255, 150))
    elif i == 9:    # star
        d.polygon(star_pts(c, c + 4 * S, 58 * S, 26 * S), fill=(255, 205, 40, 255), outline=(220, 140, 10, 255))
        d.line(star_pts(c, c + 4 * S, 58 * S, 26 * S) + [star_pts(c, c + 4 * S, 58 * S, 26 * S)[0]], fill=(220, 140, 10, 255), width=4 * S)
    elif i == 10:   # GG
        d.rounded_rectangle([8 * S, 26 * S, W - 8 * S, W - 26 * S], 22 * S, fill=(30, 140, 90, 255), outline=(250, 205, 90, 255), width=5 * S)
        font = None
        for p in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/Library/Fonts/Arial Bold.ttf'):
            if os.path.exists(p):
                font = ImageFont.truetype(p, 62 * S); break
        if font is None: font = ImageFont.load_default(62 * S)
        d.text((c, c), 'GG', font=font, fill=WHITE, anchor='mm')
    elif i == 11:   # crown
        gold, edge = (255, 205, 40, 255), (200, 130, 10, 255)
        d.polygon([(22 * S, 98 * S), (14 * S, 40 * S), (44 * S, 66 * S), (64 * S, 28 * S), (84 * S, 66 * S), (114 * S, 40 * S), (106 * S, 98 * S)], fill=gold, outline=edge)
        d.rectangle([22 * S, 92 * S, 106 * S, 106 * S], fill=edge)
        for x, y in ((14, 40), (64, 28), (114, 40)):
            d.ellipse([(x - 7) * S, (y - 7) * S, (x + 7) * S, (y + 7) * S], fill=(255, 240, 150, 255), outline=edge, width=2 * S)
        d.ellipse([56 * S, 78 * S, 72 * S, 94 * S], fill=RED)
    return img.resize((N, N), Image.LANCZOS)

atlas = Image.new('RGBA', (N * 4, N * 4), (0, 0, 0, 0))     # 512x512: Unity rescales non-power-of-two textures, so keep 4 rows (12 used)
for i in range(12):
    col, row = i % 4, i // 4
    atlas.paste(cell(i), (col * N, row * N))     # emoji i sits at column i%4, row i//4 counted from the top
os.makedirs(os.path.dirname(OUT), exist_ok=True)
atlas.save(OUT)
print('wrote', OUT)
