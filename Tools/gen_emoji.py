#!/usr/bin/env python3
"""Builds the chat emoji atlas (4x4 grid of 128 px cells, 12 used) from a third-party CC0 set, not from anything drawn here:
Tools/design/emoji_openclipart_334187.svg = "Emoji Set" by SunKing2 on Openclipart (https://openclipart.org/detail/334187/emoji-set,
public domain / CC0). The sheet is rasterised with headless Chromium (Playwright) and each face is cut out and resized.
Cell order (ChatText.EmojiCount = 12; BotChatter uses 0..6 as its friendly replies):
 0 smile, 1 grin, 2 content, 3 big smile, 4 laugh, 5 tongue, 6 wow, 7 queasy, 8 grimace, 9 sad, 10 down, 11 knocked out
Usage: python3 Tools/gen_emoji.py   (needs playwright + pillow)"""
import os, io, tempfile
from PIL import Image
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'design', 'emoji_openclipart_334187.svg')
OUT = os.path.join(HERE, '..', 'Assets', 'Core Game Assets', 'Resources', 'Emoji', 'emoji_atlas.png')
SW, SH, PITCH, N = 1920, 1444, 480, 128
ORDER = [0, 2, 3, 4, 5, 11, 9, 1, 6, 7, 10, 8]          # source cell (row-major, 4 x 3) for each atlas cell

with sync_playwright() as p:
    b = p.chromium.launch(); pg = b.new_page(viewport={'width': SW, 'height': SH})
    html = os.path.join(tempfile.mkdtemp(), 'sheet.html')
    open(html, 'w').write('<body style="margin:0;background:transparent"><img src="file://%s" style="width:%dpx;height:%dpx">' % (os.path.abspath(SRC), SW, SH))
    pg.goto('file://' + html)
    pg.wait_for_timeout(500)
    png = pg.screenshot(omit_background=True); b.close()
sheet = Image.open(io.BytesIO(png)).convert('RGBA')
atlas = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
for i, src in enumerate(ORDER):
    c, r = src % 4, src // 4
    cell = sheet.crop((c * PITCH, r * PITCH, c * PITCH + PITCH, r * PITCH + PITCH)).resize((N, N), Image.LANCZOS)
    atlas.alpha_composite(cell, ((i % 4) * N, (i // 4) * N))
os.makedirs(os.path.dirname(OUT), exist_ok=True)
atlas.save(OUT, optimize=True)
print('wrote', os.path.abspath(OUT))
