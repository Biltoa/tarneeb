#!/usr/bin/env python3
"""Rasterizes the Vector Playing Cards 3.2 SVGs (Chris Aguilar, LGPL 3.0, see CREDITS.md) into one power-of-two atlas
Assets/_Project/Resources/Cards/card_atlas.png. Cell index = Card.ToByte() = suit*13 + rank-2 (suits: clubs, diamonds,
hearts, spades; rank 2..14), laid out 9 columns x 6 rows from the top-left, each cell CELL_W x CELL_H.
Usage: python3 Tools/gen_cards.py [svg_dir]   (needs playwright + its chromium, like Tools/webgl_smoke.py)"""
import os, sys, io
from PIL import Image
from playwright.sync_api import sync_playwright

SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(
    '~/Downloads/Vector Cards (Version 3.2)/FACES (BORDERED)/STANDARD BORDERED/Single Cards (One Per FIle)')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Project', 'Resources', 'Cards', 'card_atlas.png')
CELL_W, CELL_H, COLS = 224, 313, 9                     # svg aspect 238.11 : 332.60
SUITS = ['CLUB', 'DIAMOND', 'HEART', 'SPADE']
RANKS = {r: str(r) for r in range(2, 11)}
RANKS.update({11: '11-JACK', 12: '12-QUEEN', 13: '13-KING', 14: '1'})
SS = 3                                                    # supersample factor, downscaled with LANCZOS

os.makedirs(os.path.dirname(OUT), exist_ok=True)
atlas = Image.new('RGBA', (2048, 2048), (0, 0, 0, 0))
with sync_playwright() as p:
    b = p.chromium.launch()
    page = b.new_page(viewport={'width': CELL_W * SS, 'height': CELL_H * SS}, device_scale_factor=1)
    for s, suit in enumerate(SUITS):
        for rank in range(2, 15):
            path = os.path.join(SRC, '%s-%s.svg' % (suit, RANKS[rank]))
            svg = open(path, encoding='utf-8').read()
            page.set_content('<html><body style="margin:0;background:transparent"><div id=c style="width:%dpx;height:%dpx">%s</div>'
                             '<style>svg{width:%dpx;height:%dpx;display:block}</style></body></html>'
                             % (CELL_W * SS, CELL_H * SS, svg[svg.index('<svg'):], CELL_W * SS, CELL_H * SS))
            png = page.screenshot(omit_background=True, clip={'x': 0, 'y': 0, 'width': CELL_W * SS, 'height': CELL_H * SS})
            im = Image.open(io.BytesIO(png)).convert('RGBA').resize((CELL_W, CELL_H), Image.LANCZOS)
            idx = s * 13 + rank - 2
            atlas.paste(im, ((idx % COLS) * CELL_W, (idx // COLS) * CELL_H))
    b.close()
atlas.save(OUT, optimize=True)
print('wrote', OUT, os.path.getsize(OUT) // 1024, 'KB')

# ---- suit icons from the ace pips (same shapes as the deck): coloured and white-silhouette versions, 256 px
UI = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Project', 'Resources', 'UI')
os.makedirs(UI, exist_ok=True)
with sync_playwright() as p:
    b = p.chromium.launch()
    W4, H4 = 224 * 4, 313 * 4
    page = b.new_page(viewport={'width': W4, 'height': H4}, device_scale_factor=1)
    for suit, name in zip(SUITS, ['club', 'diamond', 'heart', 'spade']):
        svg = open(os.path.join(SRC, '%s-1.svg' % suit), encoding='utf-8').read()
        page.set_content('<html><body style="margin:0;background:transparent"><style>svg{width:%dpx;height:%dpx;display:block}</style>%s</body></html>'
                         % (W4, H4, svg[svg.index('<svg'):]))
        im = Image.open(io.BytesIO(page.screenshot(omit_background=True, clip={'x': 0, 'y': 0, 'width': W4, 'height': H4}))).convert('RGBA')
        centre = im.crop((int(W4 * 0.22), int(H4 * 0.25), int(W4 * 0.78), int(H4 * 0.75))).convert('RGB')   # the big centre pip only
        px = centre.load(); cw_, ch_ = centre.size
        col = px[cw_ // 2, ch_ // 2]                                    # pip colour (centre of the ace)
        k = max(range(3), key=lambda i: 255 - col[i])                   # channel with the most contrast against the white card
        alpha = Image.new('L', centre.size, 0); ap = alpha.load()
        for y in range(ch_):
            for x in range(cw_):
                ap[x, y] = max(0, min(255, int(255 * (255 - px[x, y][k]) / max(1, 255 - col[k]))))
        pip = Image.new('RGBA', centre.size, col + (0,)); pip.putalpha(alpha)
        pip = pip.crop(alpha.point(lambda v: 255 if v > 40 else 0).getbbox())
        side = max(pip.size)
        sq = Image.new('RGBA', (side, side), (0, 0, 0, 0)); sq.paste(pip, ((side - pip.width) // 2, (side - pip.height) // 2))
        sq = sq.resize((256, 256), Image.LANCZOS)
        sq.save(os.path.join(UI, 'suit_%s.png' % name), optimize=True)
        white = Image.new('RGBA', sq.size, (255, 255, 255, 0)); white.putalpha(sq.getchannel('A'))
        white.save(os.path.join(UI, 'suit_%s_w.png' % name), optimize=True)
    b.close()
print('wrote suit icons to', os.path.abspath(UI))
