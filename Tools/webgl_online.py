#!/usr/bin/env python3
"""Two headless Chromium tabs play each other through the real Photon cloud: A and B both press Quick match, A starts,
A sends an emoji, both screenshot. Usage: python3 Tools/webgl_online.py [url]   (needs playwright, see webgl_smoke.py)
Design coordinates come from Tools/build_scenes.py (origin center, y up, 1080x1920)."""
import sys, time
from playwright.sync_api import sync_playwright
url = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:8090/index.html'
W, H = 540, 960
out = 'Logs/web/'
def tap(page, nx, ny):
    s = min(W / 1080.0, H / 1920.0)
    x, y = W / 2 + nx * s, H / 2 - ny * s
    page.mouse.move(x, y); time.sleep(0.12); page.mouse.down(); time.sleep(0.12); page.mouse.up()
with sync_playwright() as p:
    b = p.chromium.launch(args=["--use-angle=swiftshader", "--use-gl=angle", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--autoplay-policy=no-user-gesture-required"])
    pages, logs = [], [[], []]
    for i in range(2):
        ctx = b.new_context(viewport={'width': W, 'height': H}, device_scale_factor=1)
        pg = ctx.new_page()
        pg.on('console', lambda m, i=i: logs[i].append(m.type + ': ' + m.text))
        pg.on('pageerror', lambda e, i=i: logs[i].append('PAGEERROR: ' + str(e)))
        pg.goto(url)
        pg.wait_for_function("document.getElementById('loading') && document.getElementById('loading').style.display === 'none'", timeout=240000)
        pages.append(pg)
    time.sleep(3)
    A, B = pages
    tap(A, 0, -110); time.sleep(1.5); tap(A, 0, 290)          # A: Play Online, Quick match (creates the table)
    time.sleep(12)
    A.screenshot(path=out + 'online_A_room.png')
    tap(B, 0, -110); time.sleep(1.5); tap(B, 0, 290)          # B: joins A's table
    time.sleep(12)
    A.screenshot(path=out + 'online_A_two.png'); B.screenshot(path=out + 'online_B_two.png')
    tap(A, 0, -30)                                             # A: Start game
    time.sleep(25)
    A.screenshot(path=out + 'online_A_game.png'); B.screenshot(path=out + 'online_B_game.png')
    tap(A, 290, -395); time.sleep(1.5); tap(A, 315, -150)     # A: emoji button, cool face
    time.sleep(2.5)
    A.screenshot(path=out + 'online_A_emoji.png'); B.screenshot(path=out + 'online_B_emoji.png')
    for i, l in enumerate(logs):
        bad = [x for x in l if x.startswith(('error', 'PAGEERROR'))]
        print('tab', 'AB'[i], 'errors:', bad[:6])
    b.close()
