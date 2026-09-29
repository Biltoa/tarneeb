#!/usr/bin/env python3
"""Generates the original placeholder-free art (suit icons, card face/back, panel) and SFX for the prototype.
All output is original and CC0. Run: python3 Tools/gen_art.py"""
import math, os, struct, wave, random
from PIL import Image, ImageDraw

ROOT = os.path.join(os.path.dirname(__file__), '..', 'Assets', '_Project')
SPR = os.path.join(ROOT, 'Art', 'Sprites')
SFX = os.path.join(ROOT, 'Audio', 'Sfx')
S = 4  # supersample

def save(img, name, size=None):
    if size: img = img.resize(size, Image.LANCZOS)
    img.save(os.path.join(SPR, name))

def heart(d, cx, cy, r, fill):
    W = cx * 2
    rr = W * 0.25
    d.ellipse([cx - W * 0.22 - rr, W * 0.33 - rr, cx - W * 0.22 + rr, W * 0.33 + rr], fill=fill)
    d.ellipse([cx + W * 0.22 - rr, W * 0.33 - rr, cx + W * 0.22 + rr, W * 0.33 + rr], fill=fill)
    d.polygon([(cx - W * 0.455, W * 0.42), (cx + W * 0.455, W * 0.42), (cx, W * 0.93)], fill=fill)

def suit_icon(kind, n=128):
    W = n * S
    img = Image.new('RGBA', (W, W), (255, 255, 255, 0))
    d = ImageDraw.Draw(img)
    c = W / 2
    f = (255, 255, 255, 255)
    if kind == 'heart':
        heart(d, c, c - W * 0.02, W * 0.26, f)
    elif kind == 'diamond':
        d.polygon([(c, W * 0.06), (W * 0.86, c), (c, W * 0.94), (W * 0.14, c)], fill=f)
    elif kind == 'spade':
        # inverted heart + stem
        d.ellipse([c - W * 0.40, c - W * 0.08, c, c + W * 0.30], fill=f)
        d.ellipse([c, c - W * 0.08, c + W * 0.40, c + W * 0.30], fill=f)
        d.polygon([(c - W * 0.38, c + W * 0.06), (c + W * 0.38, c + W * 0.06), (c, W * 0.06)], fill=f)
        d.polygon([(c, W * 0.04), (c - W * 0.36, c + W * 0.08), (c + W * 0.36, c + W * 0.08)], fill=f)
        d.polygon([(c, c + W * 0.05), (c - W * 0.16, W * 0.94), (c + W * 0.16, W * 0.94)], fill=f)
    elif kind == 'club':
        r = W * 0.20
        d.ellipse([c - r, W * 0.06, c + r, W * 0.06 + 2 * r], fill=f)
        d.ellipse([W * 0.10, W * 0.42, W * 0.10 + 2 * r, W * 0.42 + 2 * r], fill=f)
        d.ellipse([W * 0.90 - 2 * r, W * 0.42, W * 0.90, W * 0.42 + 2 * r], fill=f)
        d.ellipse([c - W * 0.12, W * 0.30, c + W * 0.12, W * 0.62], fill=f)
        d.polygon([(c, W * 0.5), (c - W * 0.15, W * 0.94), (c + W * 0.15, W * 0.94)], fill=f)
    return img.resize((n, n), Image.LANCZOS)

def rounded(w, h, r, fill, outline=None, ow=0):
    img = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, w * S - 1, h * S - 1], r * S, fill=fill)
    if outline:
        d.rounded_rectangle([ow * S, ow * S, w * S - 1 - ow * S, h * S - 1 - ow * S], (r - ow) * S, outline=outline, width=ow * S)
    return img.resize((w, h), Image.LANCZOS)

def card_back(w=256, h=372):
    base = rounded(w, h, 26, (245, 245, 245, 255))
    inner = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(inner)
    m = 14 * S
    d.rounded_rectangle([m, m, w * S - m, h * S - m], 16 * S, fill=(150, 30, 45, 255))
    # diamond lattice
    step = 34 * S
    for i in range(-20, 40):
        d.line([(i * step, m), (i * step + h * S, h * S - m)], fill=(215, 170, 90, 255), width=3 * S)
        d.line([(i * step + h * S, m), (i * step, h * S - m)], fill=(215, 170, 90, 255), width=3 * S)
    mask = Image.new('L', (w * S, h * S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([m, m, w * S - m, h * S - m], 16 * S, fill=255)
    inner2 = Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
    inner2.paste(inner, (0, 0), mask)
    d2 = ImageDraw.Draw(inner2)
    d2.rounded_rectangle([m, m, w * S - m, h * S - m], 16 * S, outline=(245, 230, 190, 255), width=5 * S)
    d2.ellipse([w * S / 2 - 46 * S, h * S / 2 - 46 * S, w * S / 2 + 46 * S, h * S / 2 + 46 * S], fill=(150, 30, 45, 255), outline=(245, 230, 190, 255), width=5 * S)
    d2.polygon([(w * S / 2, h * S / 2 - 30 * S), (w * S / 2 + 30 * S, h * S / 2), (w * S / 2, h * S / 2 + 30 * S), (w * S / 2 - 30 * S, h * S / 2)], fill=(245, 230, 190, 255))
    base = base.resize((w * S, h * S))
    base.alpha_composite(inner2)
    return base.resize((w, h), Image.LANCZOS)

# --- sprites
for k in ('heart', 'diamond', 'spade', 'club'):
    save(suit_icon(k), 'suit_%s.png' % k)
save(rounded(256, 372, 26, (250, 250, 248, 255), (40, 40, 40, 255), 3), 'card_face.png')
save(card_back(), 'card_back.png')
save(rounded(64, 64, 20, (255, 255, 255, 255)), 'panel.png')
c = Image.new('RGBA', (128 * S, 128 * S), (0, 0, 0, 0))
ImageDraw.Draw(c).ellipse([0, 0, 128 * S - 1, 128 * S - 1], fill=(255, 255, 255, 255))
save(c, 'circle.png', (128, 128))
# soft glow ring for turn indicator
g = Image.new('RGBA', (128 * S, 128 * S), (0, 0, 0, 0))
ImageDraw.Draw(g).ellipse([6 * S, 6 * S, 122 * S, 122 * S], outline=(255, 255, 255, 255), width=10 * S)
save(g, 'ring.png', (128, 128))

# --- sfx (mono 16-bit 44.1k)
SR = 44100
def write(name, samples):
    with wave.open(os.path.join(SFX, name), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b''.join(struct.pack('<h', max(-32767, min(32767, int(s * 32767)))) for s in samples))

def env(i, n, a=0.005, r=0.9):
    t = i / n
    return min(1, t / a) * math.exp(-t * r * 6)

def tone(freq, dur, vol=0.5, wave_='sine', decay=0.9):
    n = int(SR * dur); out = []
    for i in range(n):
        p = 2 * math.pi * freq * i / SR
        s = math.sin(p) if wave_ == 'sine' else (1 if math.sin(p) > 0 else -1) * 0.5
        out.append(s * vol * env(i, n, 0.01, decay))
    return out

def noise(dur, vol=0.5, decay=1.6, lp=0.3):
    random.seed(7); n = int(SR * dur); out = []; y = 0
    for i in range(n):
        y += lp * (random.uniform(-1, 1) - y)
        out.append(y * vol * env(i, n, 0.002, decay))
    return out

def mix(*parts):
    n = max(len(p) for p in parts); out = [0.0] * n
    for p in parts:
        for i, s in enumerate(p): out[i] += s
    return out

def seq(*parts):
    out = []
    for p in parts: out += p
    return out

write('card_flip.wav', noise(0.09, 0.9, 1.4, 0.55))
write('card_place.wav', mix(noise(0.08, 0.8, 1.8, 0.2), tone(120, 0.1, 0.5, 'sine', 1.6)))
write('trick_win.wav', seq(tone(660, 0.09, 0.4), tone(880, 0.16, 0.4)))
write('round_win.wav', seq(tone(523, 0.11, 0.4), tone(659, 0.11, 0.4), tone(784, 0.11, 0.4), tone(1047, 0.28, 0.45)))
write('round_lose.wav', seq(tone(392, 0.16, 0.4), tone(311, 0.32, 0.4)))
write('click.wav', tone(1000, 0.04, 0.35, 'sine', 1.5))
print('generated')
