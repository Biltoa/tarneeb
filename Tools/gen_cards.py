#!/usr/bin/env python3
"""Draws the whole 52-card deck (jumbo-index style) and the four suit icons. Original artwork, no third-party card art: rank letters are
Noto Serif Bold (SIL OFL, Assets/FigmaImports/Fonts), suits are drawn here as shapes.
Output: Assets/Core Game Assets/Resources/Cards/card_atlas.png (9 x 6 cells of 224 x 313 px, cell index = Card.ToByte() = suit*13 + rank-2, suits:
clubs, diamonds, hearts, spades, ranks 2..14) and Assets/Core Game Assets/Resources/UI/suit_*.png / suit_*_w.png (colour + white, 256 px, margin 12).
Layout: a large rank with a large suit under it in the top-left and (turned 180 degrees) bottom-right corner; pips in the classic
layouts for 2..10, one big pip for the ace, and a plain framed monogram for J, Q and K (no pictures). The "10" is squeezed to the same height.
Usage: python3 Tools/gen_cards.py   (needs pillow)"""
import math, os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
FONT = os.path.join(HERE, '..', 'Assets', 'FigmaImports', 'Fonts', 'Noto Serif-700.ttf')
OUT = os.path.join(HERE, '..', 'Assets', 'Core Game Assets', 'Resources', 'Cards', 'card_atlas.png')
UI_DIR = os.path.join(HERE, '..', 'Assets', 'Core Game Assets', 'Resources', 'UI')
CW, CH, COLS, SS = 224, 313, 9, 3
SUITS = ['club', 'diamond', 'heart', 'spade']
RED, BLACK = (208, 16, 40), (22, 22, 28)
PAPER = (255, 254, 250)

def suit_color(i): return RED if SUITS[i] in ('diamond', 'heart') else BLACK

def suit_mask(name, S=4, N=256, margin=12):
    """Suit silhouette, natural proportions, fitted (aspect kept) into an N x N mask with a margin."""
    W = N * S
    m = Image.new('L', (W, W), 0); d = ImageDraw.Draw(m)
    def P(pts): return [(x * S, y * S) for x, y in pts]
    def heart_pts(flip=False):
        pts = []
        for i in range(361):
            t = math.radians(i)
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((x, -y if not flip else y))
        return pts
    if name in ('heart', 'spade'):
        pts = heart_pts(flip=(name == 'spade'))
        d.polygon(P([(120 + x * 5.0, 120 + y * 5.0) for x, y in pts]), fill=255)
        if name == 'spade':
            d.polygon(P([(120 - 10, 120 + 4), (120 + 10, 120 + 4), (120 + 12, 120 + 52), (120 + 30, 120 + 106), (120 - 30, 120 + 106), (120 - 12, 120 + 52)]), fill=255)
    elif name == 'diamond':
        d.polygon(P([(120, 120 - 85), (120 + 62, 120), (120, 120 + 85), (120 - 62, 120)]), fill=255)
    else:
        r = 34
        for cx, cy in ((120, 70), (78, 128), (162, 128)):
            d.ellipse(P([(cx - r, cy - r), (cx + r, cy + r)]), fill=255)
        d.ellipse(P([(106, 100), (134, 156)]), fill=255)
        d.polygon(P([(112, 130), (128, 130), (146, 200), (94, 200)]), fill=255)
    m = m.crop(m.point(lambda v: 255 if v > 6 else 0).getbbox())
    box = N - 2 * margin
    k = min(box / m.width, box / m.height)
    m = m.resize((max(1, round(m.width * k)), max(1, round(m.height * k))), Image.LANCZOS)
    out = Image.new('L', (N, N), 0); out.paste(m, ((N - m.width) // 2, (N - m.height) // 2))
    return out

MASKS = {n: suit_mask(n) for n in SUITS}
TIGHT = {n: m.crop(m.point(lambda v: 255 if v > 6 else 0).getbbox()) for n, m in MASKS.items()}    # no margin, for drawing on cards

os.makedirs(UI_DIR, exist_ok=True)
for n in SUITS:
    col = (208, 16, 40) if n in ('diamond', 'heart') else (22, 22, 28)
    a = Image.new('RGBA', MASKS[n].size, col + (0,)); a.putalpha(MASKS[n]); a.save(os.path.join(UI_DIR, 'suit_%s.png' % n), optimize=True)
    w = Image.new('RGBA', MASKS[n].size, (255, 255, 255, 0)); w.putalpha(MASKS[n]); w.save(os.path.join(UI_DIR, 'suit_%s_w.png' % n), optimize=True)
print('wrote suit icons to', os.path.abspath(UI_DIR))

def pip(suit, box, color, flip=False):
    """RGBA pip whose larger side is `box` px (at supersampled scale), optionally upside down."""
    m = TIGHT[SUITS[suit]]
    k = box / max(m.size)
    m = m.resize((max(1, round(m.width * k)), max(1, round(m.height * k))), Image.LANCZOS)
    if flip: m = m.rotate(180)
    im = Image.new('RGBA', m.size, color + (0,)); im.putalpha(m)
    return im

def text_img(text, cap_px, color, max_w=None):
    """Text as RGBA with cap height cap_px (px), digits squeezed horizontally if wider than max_w: all ranks get the same height."""
    size = int(cap_px / 0.714)
    font = ImageFont.truetype(FONT, size)
    parts = []
    for ch in text:
        bb = font.getbbox(ch)
        g = Image.new('L', (bb[2] - bb[0] + 2, bb[3] - bb[1] + 2), 0)
        ImageDraw.Draw(g).text((-bb[0] + 1, -bb[1] + 1), ch, font=font, fill=255)
        g = g.crop(g.point(lambda v: 255 if v > 6 else 0).getbbox())
        k = cap_px / g.height                                   # every glyph gets the same ink height (J and Q have descenders and looked larger)
        parts.append(g.resize((max(1, round(g.width * k)), cap_px), Image.LANCZOS))
    gap = int(size * 0.02) if len(parts) > 1 else 0
    w = sum(p.width for p in parts) + gap * (len(parts) - 1); h = max(p.height for p in parts)
    m = Image.new('L', (w, h), 0); x = 0
    for p in parts:
        m.paste(p, (x, h - p.height)); x += p.width + gap              # bottoms aligned on the baseline (glyphs like O overshoot slightly)
    if max_w and m.width > max_w:
        m = m.resize((max_w, m.height), Image.LANCZOS)
    im = Image.new('RGBA', m.size, color + (0,)); im.putalpha(m)
    return im

RANK_TEXT = {11: 'J', 12: 'Q', 13: 'K', 14: 'A'}
L, C, R = 88, 112, 136                                   # pip columns (card px); the index column owns x < 72
TOP, BOT = 52, 261
def rows(n):
    ys = {2: [(C, TOP), (C, BOT)], 3: [(C, TOP), (C, 156.5), (C, BOT)]}
    lr = lambda *yy: [(x, y) for y in yy for x in (L, R)]
    ys[4] = lr(TOP, BOT)
    ys[5] = lr(TOP, BOT) + [(C, 156.5)]
    ys[6] = lr(TOP, 156.5, BOT)
    ys[7] = lr(TOP, 156.5, BOT) + [(C, 104)]
    ys[8] = lr(TOP, 156.5, BOT) + [(C, 104), (C, 209)]
    ys[9] = lr(TOP, 118, 195, BOT) + [(C, 156.5)]
    ys[10] = lr(TOP, 118, 195, BOT) + [(C, 85), (C, 228)]
    return ys[n]

def card(suit, rank):
    W, H = CW * SS, CH * SS
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, W - 1, H - 1], 12 * SS, fill=PAPER + (255,))
    col = suit_color(suit)
    label = RANK_TEXT.get(rank, str(rank))
    # ---- index: big rank + big suit, top-left; the same turned 180 degrees bottom-right
    idx = Image.new('RGBA', (70 * SS, 136 * SS), (0, 0, 0, 0))
    t = text_img(label, 54 * SS, col, max_w=60 * SS)
    idx.alpha_composite(t, ((70 * SS - t.width) // 2, 14 * SS + (54 * SS - t.height) // 2))
    p = pip(suit, 46 * SS, col)
    idx.alpha_composite(p, ((70 * SS - p.width) // 2, 82 * SS + (46 * SS - p.height) // 2))
    img.alpha_composite(idx, (3 * SS, 0))
    img.alpha_composite(idx.rotate(180), (W - 70 * SS - 3 * SS, H - 136 * SS))
    # ---- centre
    if rank == 14:
        big = pip(suit, (118 if SUITS[suit] == 'spade' else 104) * SS, col)
        img.alpha_composite(big, (W // 2 - big.width // 2 + 6 * SS, H // 2 - big.height // 2))
    elif rank <= 10:
        for (x, y) in rows(rank):
            flip = y > 156.5 + 1
            q = pip(suit, 38 * SS, col, flip=flip)
            img.alpha_composite(q, (int(x * SS) - q.width // 2, int(y * SS) - q.height // 2))
    else:                                               # J Q K: no pictures, the same big centre pip as the ace
        big = pip(suit, (118 if SUITS[suit] == 'spade' else 104) * SS, col)
        img.alpha_composite(big, (W // 2 - big.width // 2 + 6 * SS, H // 2 - big.height // 2))
    return img.resize((CW, CH), Image.LANCZOS)

atlas = Image.new('RGBA', (2048, 2048), (0, 0, 0, 0))
for s in range(4):
    for rank in range(2, 15):
        idx = s * 13 + rank - 2
        atlas.paste(card(s, rank), ((idx % COLS) * CW, (idx // COLS) * CH))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
atlas.save(OUT, optimize=True)
print('wrote', os.path.abspath(OUT), os.path.getsize(OUT) // 1024, 'KB')
