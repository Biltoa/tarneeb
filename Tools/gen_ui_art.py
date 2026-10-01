#!/usr/bin/env python3
"""Generates the UI art that the Figma design does not contain, in the design's palette (CC0, original):
clean symmetric icons (gear, menu, chat, emote, crown, close, back, sound, music, check...), 9-sliced gold/emerald panels and
buttons, toggle and slider parts, soft glows and particle sprites. Output: Assets/Core Game Assets/Resources/UI/*.png
(sprites get their import settings from Assets/Core Game Assets/Editor/SpriteImportPostprocessor.cs; a `_9s<N>` suffix sets an N px border).
Usage: python3 Tools/gen_ui_art.py"""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageChops

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Core Game Assets', 'Resources', 'UI')
os.makedirs(OUT, exist_ok=True)
S = 4                                                           # supersample factor

GOLD_L, GOLD_M, GOLD_D, GOLD_DEEP = (255, 248, 216), (255, 228, 154), (231, 188, 93), (155, 104, 30)
MID, MID2 = (0, 22, 26), (0, 32, 36)
EM_TOP, EM_BOT = (0, 62, 60), (0, 22, 25)
CREAM = (255, 240, 190)

def save(img, name):
    img.save(os.path.join(OUT, name), optimize=True)

def lerp(a, b, t): return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))

def grad_v(w, h, stops):
    """Vertical gradient image; stops = [(pos, (r,g,b[,a]))]."""
    img = Image.new('RGBA', (w, h))
    px = img.load()
    for y in range(h):
        t = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            if stops[i][0] <= t <= stops[i + 1][0]:
                u = (t - stops[i][0]) / max(1e-6, stops[i + 1][0] - stops[i][0])
                c = lerp(stops[i][1], stops[i + 1][1], u)
                break
        else:
            c = stops[-1][1]
        c = c + (255,) if len(c) == 3 else c
        for x in range(w): px[x, y] = c
    return img

GOLD_STOPS = [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_D)]

def mask_from(draw_fn, w, h):
    m = Image.new('L', (w * S, h * S), 0)
    draw_fn(ImageDraw.Draw(m))
    return m.resize((w, h), Image.LANCZOS)

def rr_mask(w, h, r, inset=0):
    return mask_from(lambda d: d.rounded_rectangle([inset * S, inset * S, (w - inset) * S - 1, (h - inset) * S - 1], r * S, fill=255), w, h)

def ellipse_mask(w, h, inset=0):
    return mask_from(lambda d: d.ellipse([inset * S, inset * S, (w - inset) * S - 1, (h - inset) * S - 1], fill=255), w, h)

def paint(base, fill_img, mask):
    layer = Image.new('RGBA', base.size, (0, 0, 0, 0))
    layer.paste(fill_img, (0, 0), mask)
    base.alpha_composite(layer)

def rrect(w, h, r, fill_stops, border_stops=None, bw=0, shadow=0, inner_hl=True):
    """Rounded rectangle with gradient fill and gradient border; margin around it for an optional soft shadow."""
    m = shadow
    W, H = w + 2 * m, h + 2 * m
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    if shadow:
        sh = Image.new('L', (W, H), 0)
        ImageDraw.Draw(sh).rounded_rectangle([m, m + shadow // 2, m + w - 1, m + h - 1 + shadow // 2], r, fill=150)
        sh = sh.filter(ImageFilter.GaussianBlur(shadow / 2.2))
        img.paste((0, 0, 0, 255), (0, 0), sh)
    outer = Image.new('L', (W, H), 0); outer.paste(rr_mask(w, h, r), (m, m))
    if border_stops and bw:
        paint(img, grad_v(W, H, border_stops), outer)
        inner = Image.new('L', (W, H), 0); inner.paste(rr_mask(w, h, max(1, r - bw), inset=bw), (m, m))
        paint(img, grad_v(W, H, fill_stops), inner)
        mask = inner
    else:
        paint(img, grad_v(W, H, fill_stops), outer)
        mask = outer
    if inner_hl:                                           # soft sheen fading out over the top half (no hard edge: a step here read as a "line")
        import numpy as np
        hl = np.zeros((H, W), dtype=np.float32)
        top, hh = m + bw, max(2, int((h - 2 * bw) * 0.55))
        ramp = 40.0 * (1.0 - np.arange(hh) / hh) ** 2.2
        hl[top:top + hh, :] = ramp[:, None]
        hl_img = ImageChops.multiply(Image.fromarray(hl.astype(np.uint8), 'L'), mask)
        img.paste((255, 255, 255, 255), (0, 0), hl_img)
    return img

# ------------------------------------------------------------------ panels and buttons (9-sliced)
save(rrect(256, 256, 44, [(0, (2, 34, 34, 250)), (1, (0, 16, 20, 252))], [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_D)], 6, shadow=0, inner_hl=False), 'panel_dark_9s56.png')
save(rrect(256, 256, 44, [(0, (0, 40, 40, 245)), (1, (0, 18, 22, 245))], [(0, GOLD_M), (1, GOLD_DEEP)], 3, shadow=0, inner_hl=False), 'panel_soft_9s52.png')
save(rrect(192, 128, 40, GOLD_STOPS, [(0, GOLD_L), (1, GOLD_DEEP)], 4, inner_hl=True), 'button_gold_9s48.png')
save(rrect(192, 128, 40, [(0, EM_TOP + (255,)), (1, EM_BOT + (255,))], [(0, GOLD_L), (0.55, GOLD_M), (1, GOLD_D)], 4), 'button_emerald_9s48.png')
save(rrect(192, 128, 40, [(0, (170, 40, 44)), (1, (110, 16, 24))], [(0, (255, 200, 190)), (1, (190, 90, 80))], 4), 'button_red_9s48.png')
save(rrect(192, 128, 40, [(0, (60, 66, 66)), (1, (36, 42, 42))], [(0, (150, 156, 150)), (1, (90, 96, 92))], 4, inner_hl=False), 'button_disabled_9s48.png')
save(rrect(128, 128, 30, [(0, (255, 255, 255, 40)), (1, (255, 255, 255, 14))], [(0, GOLD_M + (200,)), (1, GOLD_DEEP + (200,))], 3, inner_hl=False), 'slot_9s36.png')
save(rrect(96, 96, 26, [(0, GOLD_L), (1, GOLD_D)], None, 0, inner_hl=False), 'plain_gold_9s30.png')

# text field (2x, used at pixelsPerUnitMultiplier 2): a dark inset with a gold rim, like the panels
def field():
    img = rrect(160, 96, 44, [(0, (0, 10, 13)), (0.5, (0, 22, 26)), (1, (2, 34, 38))], [(0, GOLD_DEEP), (1, GOLD_D)], 4, inner_hl=False)
    sh = Image.new('L', img.size, 0)
    ImageDraw.Draw(sh).rounded_rectangle([5, 5, img.width - 6, 34], 38, fill=110)                   # soft shadow under the top rim: the field looks recessed
    sh = sh.filter(ImageFilter.GaussianBlur(6))
    mask = rr_mask(img.width, img.height, 40, inset=4)
    shade = Image.new('RGBA', img.size, (0, 0, 0, 0)); shade.putalpha(ImageChops.multiply(sh, mask)); img.alpha_composite(shade)
    return img
save(field(), 'field_9s46.png')

# round buttons: emerald face + gold ring (like Figma 'Control / Chat')
def round_button(d=204):
    img = Image.new('RGBA', (d, d), (0, 0, 0, 0))
    paint(img, grad_v(d, d, [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_DEEP)]), ellipse_mask(d, d))
    inset = 9
    paint(img, grad_v(d, d, [(0, EM_TOP), (1, EM_BOT)]), ellipse_mask(d, d, inset))
    return img
save(round_button(), 'button_round.png')

# ------------------------------------------------------------------ icons (white-gold silhouettes, 256 px, centred, symmetric)
IC = 256
def icon(draw_fn, name, color_stops=None):
    """Draws the silhouette, then fits it (centred, aspect kept) into the canvas with a 12 px margin so no icon is ever cut at a sprite edge."""
    m = mask_from(draw_fn, IC, IC)
    bb = m.point(lambda v: 255 if v > 6 else 0).getbbox()
    if bb:
        crop = m.crop(bb)
        k = min((IC - 24) / crop.width, (IC - 24) / crop.height)
        crop = crop.resize((max(1, round(crop.width * k)), max(1, round(crop.height * k))), Image.LANCZOS)
        m = Image.new('L', (IC, IC), 0)
        m.paste(crop, ((IC - crop.width) // 2, (IC - crop.height) // 2))
    img = Image.new('RGBA', (IC, IC), (0, 0, 0, 0))
    paint(img, grad_v(IC, IC, color_stops or [(0, (255, 250, 226)), (1, (246, 208, 122))]), m)
    save(img, name)
    return m

def cut(d, fn):                                            # helper for holes: draw with fill 0
    fn(d)

def gear(d):
    cx = cy = IC * S / 2
    teeth, ro, rr_, ri = 8, 108 * S, 86 * S, 38 * S
    pts = []
    for i in range(teeth):
        a0 = 2 * math.pi * i / teeth
        w_out, w_in = math.radians(11.5), math.radians(17.5)   # half-widths of tooth tip and root
        for a, r in ((a0 - w_in, rr_), (a0 - w_out, ro), (a0 + w_out, ro), (a0 + w_in, rr_)):
            pts.append((cx + r * math.sin(a), cy - r * math.cos(a)))
        # root arc to next tooth
        a1 = 2 * math.pi * (i + 1) / teeth
        for k in range(1, 6):
            a = a0 + w_in + (a1 - w_in - (a0 + w_in) - w_in * 0 - (0)) * k / 6 if False else a0 + w_in + ((a1 - w_in) - (a0 + w_in)) * k / 6
            pts.append((cx + rr_ * math.sin(a), cy - rr_ * math.cos(a)))
    d.polygon(pts, fill=255)
    d.ellipse([cx - ri, cy - ri, cx + ri, cy + ri], fill=0)
icon(gear, 'icon_gear.png')

def menu(d):
    w, h, gap = 176 * S, 26 * S, 50 * S
    x0 = (IC * S - w) / 2
    for i in (-1, 0, 1):
        y = IC * S / 2 + i * gap
        d.rounded_rectangle([x0, y - h / 2, x0 + w, y + h / 2], h / 2, fill=255)
icon(menu, 'icon_menu.png')

def chat(d):
    x0, y0, x1, y1 = 28 * S, 40 * S, 228 * S, 168 * S
    d.rounded_rectangle([x0, y0, x1, y1], 56 * S, fill=255)
    d.polygon([(68 * S, 150 * S), (56 * S, 222 * S), (128 * S, 164 * S)], fill=255)
    for i in (-1, 0, 1):
        cx, cy, r = 128 * S + i * 46 * S, 104 * S, 14 * S
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=0)
icon(chat, 'icon_chat.png')

def smile(d):
    cx = cy = IC * S / 2
    r = 108 * S
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    for sx in (-1, 1):
        ex, ey = cx + sx * 40 * S, cy - 26 * S
        d.ellipse([ex - 15 * S, ey - 22 * S, ex + 15 * S, ey + 22 * S], fill=0)
    d.arc([cx - 62 * S, cy - 42 * S, cx + 62 * S, cy + 66 * S], 25, 155, fill=0, width=int(15 * S))
    for a in (25, 155):                                     # round caps
        x = cx + 62 * S * math.cos(math.radians(a)); y = cy + 12 * S + 54 * S * math.sin(math.radians(a))
        d.ellipse([x - 7.5 * S, y - 7.5 * S, x + 7.5 * S, y + 7.5 * S], fill=0)
icon(smile, 'icon_smile.png')

def close(d):
    c = IC * S / 2; a = 62 * S; w = 26 * S
    d.line([c - a, c - a, c + a, c + a], fill=255, width=int(w)); d.line([c - a, c + a, c + a, c - a], fill=255, width=int(w))
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = c + sx * a, c + sy * a
            d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=255)
icon(close, 'icon_close.png')

def back(d):
    c = IC * S / 2; w = 26 * S
    pts = [(c + 34 * S, c - 76 * S), (c - 42 * S, c), (c + 34 * S, c + 76 * S)]
    d.line(pts, fill=255, width=int(w), joint='curve')
    for x, y in (pts[0], pts[2]):
        d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=255)
icon(back, 'icon_back.png')

def check(d):
    c = IC * S / 2; w = 28 * S
    pts = [(c - 70 * S, c + 6 * S), (c - 22 * S, c + 54 * S), (c + 76 * S, c - 52 * S)]
    d.line(pts, fill=255, width=int(w), joint='curve')
    for x, y in (pts[0], pts[2]):
        d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=255)
icon(check, 'icon_check.png')

def speaker(d, cx_off=-22):
    c = IC * S / 2
    x = c + cx_off * S
    d.polygon([(x - 62 * S, c - 34 * S), (x - 22 * S, c - 34 * S), (x + 32 * S, c - 80 * S), (x + 32 * S, c + 80 * S), (x - 22 * S, c + 34 * S), (x - 62 * S, c + 34 * S)], fill=255)
def sound_on(d):
    speaker(d)
    c = IC * S / 2
    for i, r in enumerate((44, 76)):
        bx = c + 22 * S
        d.arc([bx - r * S, c - r * S, bx + r * S, c + r * S], -48, 48, fill=255, width=int(17 * S))
icon(sound_on, 'icon_sound_on.png')
def sound_off(d):
    speaker(d)
    c = IC * S / 2
    for s in (1, -1):
        d.line([c + 40 * S, c - 46 * S * s, c + 104 * S, c + 46 * S * s], fill=255, width=int(17 * S))
    for x, y in ((c + 40 * S, c - 46 * S), (c + 104 * S, c + 46 * S), (c + 40 * S, c + 46 * S), (c + 104 * S, c - 46 * S)):
        d.ellipse([x - 8.5 * S, y - 8.5 * S, x + 8.5 * S, y + 8.5 * S], fill=255)
icon(sound_off, 'icon_sound_off.png')

def note(d, off=0):
    c = IC * S / 2
    d.ellipse([c - 66 * S, c + 34 * S, c - 6 * S, c + 88 * S], fill=255)
    d.ellipse([c + 20 * S, c + 12 * S, c + 80 * S, c + 66 * S], fill=255)
    d.rectangle([c - 22 * S, c - 84 * S, c - 6 * S, c + 62 * S], fill=255)
    d.rectangle([c + 64 * S, c - 106 * S, c + 80 * S, c + 40 * S], fill=255)
    d.polygon([(c - 22 * S, c - 84 * S), (c + 80 * S, c - 106 * S), (c + 80 * S, c - 62 * S), (c - 22 * S, c - 40 * S)], fill=255)
icon(note, 'icon_music_on.png')
def music_off(d):
    note(d)
    c = IC * S / 2
    d.line([c - 90 * S, c - 90 * S, c + 90 * S, c + 100 * S], fill=0, width=int(40 * S))
    d.line([c - 90 * S, c - 90 * S, c + 90 * S, c + 100 * S], fill=255, width=int(17 * S))
icon(music_off, 'icon_music_off.png')

def vibe(d):        # speed / fast-forward
    c = IC * S / 2
    for dx in (-50, 30):
        d.polygon([(c + dx * S - 34 * S, c - 70 * S), (c + dx * S + 46 * S, c), (c + dx * S - 34 * S, c + 70 * S)], fill=255)
icon(vibe, 'icon_speed.png')

def info(d):
    c = IC * S / 2
    d.ellipse([c - 108 * S, c - 108 * S, c + 108 * S, c + 108 * S], fill=255)
    d.ellipse([c - 15 * S, c - 68 * S, c + 15 * S, c - 38 * S], fill=0)
    d.rounded_rectangle([c - 14 * S, c - 20 * S, c + 14 * S, c + 70 * S], 12 * S, fill=0)
icon(info, 'icon_info.png')

def bot(d):         # difficulty: simple robot head
    c = IC * S / 2
    d.rounded_rectangle([c - 84 * S, c - 58 * S, c + 84 * S, c + 76 * S], 34 * S, fill=255)
    d.rounded_rectangle([c - 6 * S, c - 100 * S, c + 6 * S, c - 58 * S], 6 * S, fill=255)
    d.ellipse([c - 16 * S, c - 116 * S, c + 16 * S, c - 84 * S], fill=255)
    for sx in (-1, 1):
        d.ellipse([c + sx * 40 * S - 17 * S, c - 22 * S, c + sx * 40 * S + 17 * S, c + 12 * S], fill=0)
    d.rounded_rectangle([c - 40 * S, c + 32 * S, c + 40 * S, c + 46 * S], 7 * S, fill=0)
icon(bot, 'icon_bot.png')

def crown(d):
    # symmetric 5-point crown with base band
    c = IC * S / 2
    pts = [(28, 196), (18, 78), (82, 128), (128, 44), (174, 128), (238, 78), (228, 196)]
    d.polygon([(x * S, y * S) for x, y in pts], fill=255)
    for x, y in ((18, 78), (128, 44), (238, 78)):
        d.ellipse([(x - 17) * S, (y - 17) * S, (x + 17) * S, (y + 17) * S], fill=255)
    d.rounded_rectangle([28 * S, 178 * S, 228 * S, 214 * S], 10 * S, fill=255)
icon(crown, 'icon_crown.png', [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_D)])

# ------------------------------------------------------------------ glows and particles
def radial(size, stops):
    img = Image.new('RGBA', (size, size)); px = img.load(); c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - c, y - c) / c)
            for i in range(len(stops) - 1):
                if stops[i][0] <= t <= stops[i + 1][0]:
                    u = (t - stops[i][0]) / max(1e-6, stops[i + 1][0] - stops[i][0]); px[x, y] = lerp(stops[i][1], stops[i + 1][1], u); break
            else: px[x, y] = stops[-1][1]
    return img
save(radial(256, [(0, (255, 235, 170, 255)), (0.35, (255, 214, 120, 150)), (1, (255, 200, 90, 0))]), 'glow_gold.png')
save(radial(256, [(0, (255, 255, 255, 255)), (0.3, (255, 255, 255, 120)), (1, (255, 255, 255, 0))]), 'glow_white.png')
save(radial(256, [(0, (0, 0, 0, 0)), (0.62, (0, 0, 0, 0)), (1, (0, 0, 0, 200))]), 'vignette.png')

def sparkle(size=128):
    m = Image.new('L', (size * S, size * S), 0); d = ImageDraw.Draw(m); c = size * S / 2
    pts = []
    for i in range(8):
        r = c * (0.98 if i % 2 == 0 else 0.16); a = math.pi * i / 4
        pts.append((c + r * math.sin(a), c - r * math.cos(a)))
    d.polygon(pts, fill=255)
    m = m.resize((size, size), Image.LANCZOS)
    img = Image.new('RGBA', (size, size), (255, 244, 200, 0)); img.putalpha(m); return img
save(sparkle(), 'particle_star.png')
def soft_dot(size=64):
    return radial(size, [(0, (255, 255, 255, 255)), (0.5, (255, 255, 255, 190)), (1, (255, 255, 255, 0))])
save(soft_dot(), 'particle_dot.png')
def confetti(size=32):
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0)); ImageDraw.Draw(img).rounded_rectangle([4, 8, size - 5, size - 9], 3, fill=(255, 255, 255, 255)); return img
save(confetti(), 'particle_confetti.png')
def ring_pulse(size=256, w=10):
    m = Image.new('L', (size * S, size * S), 0); d = ImageDraw.Draw(m)
    d.ellipse([0, 0, size * S - 1, size * S - 1], fill=255); d.ellipse([w * S, w * S, (size - w) * S - 1, (size - w) * S - 1], fill=0)
    m = m.resize((size, size), Image.LANCZOS); img = Image.new('RGBA', (size, size), (255, 236, 170, 255)); img.putalpha(m); return img.filter(ImageFilter.GaussianBlur(0.6))
save(ring_pulse(), 'ring_pulse.png')

# ------------------------------------------------------------------ toggle + slider parts
# toggle: a rounded-rectangle track (corner radius 18 of 64, not a full pill: a pill read as an egg) with a rounded-square knob
save(rrect(160, 80, 22, [(0, (0, 18, 22)), (1, (0, 30, 34))], [(0, GOLD_DEEP), (1, GOLD_D)], 4, inner_hl=False), 'toggle_track_9s24.png')
save(rrect(160, 80, 22, [(0, (40, 150, 120)), (1, (0, 100, 84))], [(0, GOLD_L), (1, GOLD_D)], 4, inner_hl=True), 'toggle_track_on_9s24.png')
def toggle_knob(d=128):
    img = Image.new('RGBA', (d, d), (0, 0, 0, 0))
    paint(img, grad_v(d, d, [(0, GOLD_D), (1, GOLD_DEEP)]), rr_mask(d, d, 34, 3))
    paint(img, grad_v(d, d, [(0, GOLD_L), (0.55, GOLD_M), (1, GOLD_D)]), rr_mask(d, d, 26, 12))
    return img
save(toggle_knob(), 'toggle_knob.png')
def knob(d=128):
    """Round slider/toggle knob: dark-gold rim, lit face, small engraved centre. Perfectly circular (used at 46 to 50 px)."""
    img = Image.new('RGBA', (d, d), (0, 0, 0, 0))
    paint(img, grad_v(d, d, [(0, GOLD_D), (1, GOLD_DEEP)]), ellipse_mask(d, d, 3))
    paint(img, grad_v(d, d, [(0, GOLD_L), (0.55, GOLD_M), (1, GOLD_D)]), ellipse_mask(d, d, 12))
    paint(img, grad_v(d, d, [(0, GOLD_D), (1, GOLD_L)]), ellipse_mask(d, d, int(d * 0.36)))
    paint(img, grad_v(d, d, [(0, GOLD_M), (1, GOLD_D)]), ellipse_mask(d, d, int(d * 0.40)))
    return img
save(knob(), 'knob.png')
# slider: a pill track with a 3 px gold rim, and a gold fill that is a pill of exactly the track's inner size (no gap at the left end)
save(rrect(96, 32, 16, [(0, (0, 10, 13)), (1, (0, 30, 34))], [(0, GOLD_DEEP), (1, GOLD_D)], 3, inner_hl=False), 'slider_track_9s16.png')
save(rrect(96, 26, 13, [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_D)], None, 0, inner_hl=False), 'slider_fill_9s13.png')
# ------------------------------------------------------------------ table jewel (perfectly symmetric): gold setting + turquoise facets
def jewel(w=128, h=144):
    W, H = w * S, h * S
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    def poly(pts, scale=1.0):
        m = Image.new('L', (W, H), 0)
        cx, cy = W / 2, H / 2
        ImageDraw.Draw(m).polygon([(cx + (x - 0.5) * W * scale, cy + (y - 0.5) * H * scale) for x, y in pts], fill=255)
        return m.resize((w, h), Image.LANCZOS)
    diamond = [(0.5, 0.0), (1.0, 0.5), (0.5, 1.0), (0.0, 0.5)]
    paint(img, grad_v(w, h, [(0, GOLD_DEEP), (1, GOLD_DEEP)]), poly(diamond, 0.96))
    paint(img, grad_v(w, h, [(0, GOLD_L), (0.5, GOLD_M), (1, GOLD_D)]), poly(diamond, 0.87))
    paint(img, grad_v(w, h, [(0, (0, 96, 106)), (1, (0, 40, 52))]), poly(diamond, 0.60))
    # four facets of the stone, lit from the top-left
    cx, cy = w / 2, h / 2
    def tri(p):
        m = Image.new('L', (w * S, h * S), 0)
        ImageDraw.Draw(m).polygon([(x * S, y * S) for x, y in p], fill=255)
        return m.resize((w, h), Image.LANCZOS)
    r_x, r_y = w * 0.60 / 2 * 0.98, h * 0.60 / 2 * 0.98
    top, right, bot, left = (cx, cy - r_y), (cx + r_x, cy), (cx, cy + r_y), (cx - r_x, cy)
    c = (cx, cy)
    for pts, col in (([top, c, left], (120, 236, 244)), ([top, right, c], (0, 200, 214)), ([left, c, bot], (0, 150, 168)), ([c, right, bot], (0, 96, 116))):
        paint(img, grad_v(w, h, [(0, col), (1, col)]), tri(pts))
    save(img, 'jewel.png')
jewel()

# ------------------------------------------------------------------ card shadow + glow (9-sliced, card is ~145 x 203)
def soft_rr(size, inset, r, blur, fill, stroke=0, stroke_col=None):
    img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    m = Image.new('L', (size, size), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, size - inset - 1, size - inset - 1], r, fill=255)
    if stroke:
        inner = Image.new('L', (size, size), 0)
        ImageDraw.Draw(inner).rounded_rectangle([inset + stroke, inset + stroke, size - inset - stroke - 1, size - inset - stroke - 1], max(1, r - stroke), fill=255)
        m = ImageChops.subtract(m, inner)
    m = m.filter(ImageFilter.GaussianBlur(blur))
    img.paste(fill, (0, 0), m)
    return img
save(soft_rr(128, 36, 14, 10, (0, 0, 0, 255)).point(lambda v: v), 'card_shadow_9s50.png')
save(soft_rr(128, 32, 12, 4.5, (255, 226, 130, 255), stroke=5), 'card_glow_9s50.png')

# ------------------------------------------------------------------ plates (2x, used with Image.pixelsPerUnitMultiplier = 2 so the corner radius stays 22 units)
# Nameplates, score capsules and HUD chips used to be near-black on a near-black background. These have a lit body and a bright rim
# (per team colour for the seats), so they read against both the dark frame and the green felt.
def plate(name, top, bot, rim_a, rim_b):
    save(rrect(160, 96, 44, [(0, top), (1, bot)], [(0, rim_a), (1, rim_b)], 4, inner_hl=True), name)
plate('plate_us_9s46.png', (14, 92, 132), (6, 56, 92), (120, 236, 246), (0, 150, 176))          # team A (you + partner): blue-teal
plate('plate_them_9s46.png', (150, 74, 26), (104, 46, 14), (255, 190, 120), (214, 120, 48))     # team B: copper
plate('plate_bronze_9s46.png', (142, 104, 34), (92, 62, 16), (255, 232, 160), (214, 160, 66))    # bid capsule: warm bronze with a bright gold rim, clearly different from the teal scoreboard
plate('plate_gold_9s46.png', (12, 76, 84), (4, 46, 54), GOLD_M, GOLD_DEEP)                      # neutral HUD chips: lit teal with a gold rim

# ------------------------------------------------------------------ chat bubble: a dialogue balloon (cream, gold rim) + a tail that points at the player
def bubble_body():
    w, h, r, m = 192, 128, 52, 16                                       # 2x px; m = transparent margin that holds the drop shadow
    W, H = w + 2 * m, h + 2 * m
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    sh = Image.new('L', (W, H), 0)
    ImageDraw.Draw(sh).rounded_rectangle([m, m + 5, m + w - 1, m + h - 1 + 5], r, fill=110)
    img.paste((0, 0, 0, 255), (0, 0), sh.filter(ImageFilter.GaussianBlur(5)))
    outer = Image.new('L', (W, H), 0); outer.paste(rr_mask(w, h, r), (m, m))
    paint(img, grad_v(W, H, [(0, GOLD_DEEP), (1, GOLD_DEEP)]), outer)
    inner = Image.new('L', (W, H), 0); inner.paste(rr_mask(w, h, r - 5, inset=5), (m, m))
    paint(img, grad_v(W, H, BUBBLE_STOPS), inner)
    return img
BUBBLE_STOPS = [(0, (255, 251, 236)), (1, (248, 232, 190))]
save(bubble_body(), 'bubble_9s72.png')

def bubble_tail():
    """Tail pointing left (drawn BEHIND the body, its base tucked under it; a small cover patch in the body colour hides the body rim at the join)."""
    w, h, Sx = 96, 84, 4
    def bez(p0, p1, p2, n=40):
        return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0], (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in [i / n for i in range(n + 1)]]
    top = bez((w + 6, 24), (w * 0.45, 44), (3, 62))                 # concave upper edge down to the tip
    bot = bez((3, 62), (w * 0.55, 64), (w + 6, 60))                # gently convex lower edge back to the base
    poly = top + bot[1:]
    m = Image.new('L', (w * Sx, h * Sx), 0)
    ImageDraw.Draw(m).polygon([(x * Sx, y * Sx) for x, y in poly], fill=255)
    outer = m.resize((w, h), Image.LANCZOS)
    inner = m.filter(ImageFilter.MinFilter(5 * Sx * 2 // 2 * 2 + 1)).resize((w, h), Image.LANCZOS)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    paint(img, grad_v(w, h, [(0, GOLD_DEEP), (1, GOLD_DEEP)]), outer)
    body_col = lerp(BUBBLE_STOPS[0][1], BUBBLE_STOPS[1][1], 0.62)
    paint(img, grad_v(w, h, [(0, body_col), (1, body_col)]), inner)
    save(img, 'bubble_tail.png')
bubble_tail()

# ------------------------------------------------------------------ turn effect for the seats: everything is generated from an exact n-fold symmetry
import numpy as np
def radial_field(size):
    c = (size - 1) / 2; y, x = np.mgrid[0:size, 0:size]
    return np.hypot(x - c, y - c) / c, np.arctan2(y - c, x - c)
def turn_rays(size=512, n=16):
    """n tapered spikes, alternating long and short, fading outwards; identical in every wedge (symmetric about the centre)."""
    r, ang = radial_field(size)
    a = np.zeros((size, size), np.float32)
    for k in range(n):
        centre = 2 * math.pi * k / n - math.pi / 2
        d = np.abs(((ang - centre + math.pi) % (2 * math.pi)) - math.pi)          # angular distance to this spike axis
        long_ = (k % 2 == 0)
        r0, r1 = (0.84, 1.0) if long_ else (0.84, 0.93)
        t = np.clip((r - r0) / (r1 - r0), 0, 1)
        half = (0.075 if long_ else 0.06) * (1 - t)                               # width narrows to a point
        m = (r >= r0) & (r <= r1) & (d <= half)
        fade = (1 - t) ** 0.8
        edge = np.clip(1 - d / np.maximum(half, 1e-4), 0, 1) ** 0.5
        a = np.maximum(a, m * fade * edge)
    img = np.zeros((size, size, 4), np.uint8)
    img[..., 0], img[..., 1], img[..., 2] = 255, 236, 170
    img[..., 3] = (np.clip(a * 1.35, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'turn_rays.png')
turn_rays()
def turn_orbit(size=512, n=24):
    """A ring of n glowing beads (alternating sizes) on a circle: rotates the other way from the rays."""
    r, ang = radial_field(size)
    a = np.zeros((size, size), np.float32)
    c = (size - 1) / 2; R = 0.925 * c
    for k in range(n):
        th = 2 * math.pi * k / n
        cx, cy = c + R * math.cos(th), c + R * math.sin(th)
        y, x = np.mgrid[0:size, 0:size]
        rad = (6.0 if k % 2 == 0 else 3.6)
        d = np.hypot(x - cx, y - cy)
        core = np.clip(1 - (d - rad * 0.55) / (rad * 0.45), 0, 1)
        halo = np.exp(-(d / (rad * 2.4)) ** 2) * 0.45
        a = np.maximum(a, np.maximum(core, halo))
    img = np.zeros((size, size, 4), np.uint8)
    img[..., 0], img[..., 1], img[..., 2] = 255, 244, 205
    img[..., 3] = (np.clip(a, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'turn_orbit.png')
turn_orbit()
def turn_glow(size=512):
    """Soft annulus of gold light (peak just outside the portrait ring) and a crisp thin rim ring."""
    r, _ = radial_field(size)
    glow = np.exp(-((r - 0.87) / 0.085) ** 2) * 0.85 * (r < 1.0)
    img = np.zeros((size, size, 4), np.uint8); img[..., 0], img[..., 1], img[..., 2] = 255, 214, 110
    img[..., 3] = (np.clip(glow, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'turn_glow.png')
    rim = np.clip(1 - np.abs(r - 0.84) / 0.012, 0, 1)
    img2 = np.zeros((size, size, 4), np.uint8); img2[..., 0], img2[..., 1], img2[..., 2] = 255, 240, 190
    img2[..., 3] = (rim * 255).astype(np.uint8)
    save(Image.fromarray(img2, 'RGBA'), 'turn_rim.png')
turn_glow()

# ------------------------------------------------------------------ main menu ambience: soft bokeh embers and a symmetric ray fan
def bokeh(size=128):
    r, _ = radial_field(size)
    a = np.clip(1 - r, 0, 1) ** 1.6 * 0.55 + np.exp(-((r - 0.82) / 0.07) ** 2) * 0.35 * (r < 1)
    img = np.zeros((size, size, 4), np.uint8); img[..., 0], img[..., 1], img[..., 2] = 255, 222, 140
    img[..., 3] = (np.clip(a, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'particle_bokeh.png')
bokeh()
def glow_dot(size=64):
    """A small glowing dot: a bright core with a soft falloff (no rim, unlike bokeh discs)."""
    r, _ = radial_field(size)
    a = np.clip(np.exp(-(r / 0.30) ** 2) * 1.0 + np.exp(-(r / 0.75) ** 2) * 0.35, 0, 1) * (r < 1.0)
    img = np.zeros((size, size, 4), np.uint8); img[..., 0], img[..., 1], img[..., 2] = 255, 236, 176
    img[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'particle_glow.png')
glow_dot()
def ray_fan(size=1024, n=20):
    r, ang = radial_field(size)
    a = np.zeros((size, size), np.float32)
    for k in range(n):
        centre = 2 * math.pi * k / n
        d = np.abs(((ang - centre + math.pi) % (2 * math.pi)) - math.pi)
        a = np.maximum(a, np.clip(1 - d / (math.pi / n * 0.55), 0, 1) ** 1.5)
    a = a * np.clip(1 - r, 0, 1) ** 1.2 * np.clip(r / 0.12, 0, 1)
    img = np.zeros((size, size, 4), np.uint8); img[..., 0], img[..., 1], img[..., 2] = 255, 226, 150
    img[..., 3] = (np.clip(a, 0, 1) * 255).astype(np.uint8)
    save(Image.fromarray(img, 'RGBA'), 'menu_rays.png')
ray_fan()

# thin dark outline for the cards (2x, radius 15 px = 7.5 units like the deck's corners), so overlapping white cards can be told apart
def card_edge():
    n, r, st = 64, 15, 3
    m = Image.new('L', (n * S, n * S), 0); d = ImageDraw.Draw(m)
    d.rounded_rectangle([0, 0, n * S - 1, n * S - 1], r * S, fill=255)
    d.rounded_rectangle([st * S, st * S, n * S - 1 - st * S, n * S - 1 - st * S], (r - st) * S, fill=0)
    m = m.resize((n, n), Image.LANCZOS)
    img = Image.new('RGBA', (n, n), (14, 26, 30, 0)); img.putalpha(m.point(lambda v: int(v * 0.78)))
    save(img, 'card_edge_9s20.png')
card_edge()

# card highlight: a bright gold border with a soft inner glow, drawn INSIDE the card's own rectangle (so it never shows over a neighbouring card)
def card_hl():
    n, r = 64, 15
    m = Image.new('L', (n, n), 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, n - 1, n - 1], r, fill=255)
    inner = Image.new('L', (n * S, n * S), 0); d = ImageDraw.Draw(inner)
    d.rounded_rectangle([0, 0, n * S - 1, n * S - 1], r * S, fill=255)
    d.rounded_rectangle([5 * S, 5 * S, n * S - 1 - 5 * S, n * S - 1 - 5 * S], (r - 5) * S, fill=0)
    rim = inner.resize((n, n), Image.LANCZOS)                                    # 5 px (2.5 units) border
    glow = Image.new('L', (n, n), 0)
    ImageDraw.Draw(glow).rounded_rectangle([5, 5, n - 6, n - 6], r - 5, outline=255, width=9)
    glow = glow.filter(ImageFilter.GaussianBlur(4)).point(lambda v: int(v * 0.55))
    img = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    g = Image.new('RGBA', (n, n), (255, 214, 96, 0)); g.putalpha(ImageChops.multiply(glow, m)); img.alpha_composite(g)
    b = Image.new('RGBA', (n, n), (255, 232, 140, 0)); b.putalpha(ImageChops.multiply(rim, m)); img.alpha_composite(b)
    save(img, 'card_hl_9s20.png')
card_hl()

# icon for "tricks won": a fanned stack of three cards
def tricks_icon(d):
    W = IC * S
    base = Image.new('L', (W, W), 0)
    def card_layer(dx, ang):
        cx, cy, w, h = W / 2 + dx * S, W / 2 + 10 * S, 92 * S, 128 * S
        l = Image.new('L', (W, W), 0)
        ImageDraw.Draw(l).rounded_rectangle([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], 12 * S, fill=255)
        return l.rotate(ang, center=(W / 2, W / 2 + 62 * S), resample=Image.BICUBIC)       # fan about a point below the cards
    for dx, ang in ((0, 20), (0, -20), (0, 0)):
        cl = card_layer(dx, ang)
        base.paste(0, (0, 0), cl.filter(ImageFilter.MaxFilter(2 * 3 * S + 1)))            # a thin gap around each card painted over the ones behind
        base.paste(255, (0, 0), cl)
    d._image.paste(base, (0, 0))
icon(tricks_icon, 'icon_tricks.png')

print('wrote UI art to', os.path.abspath(OUT))
