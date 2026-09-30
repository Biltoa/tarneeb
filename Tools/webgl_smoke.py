#!/usr/bin/env python3
"""Smoke-tests the WebGL build in headless Chromium (Playwright): loads it, plays through the menu, and saves screenshots.
Usage: python3 Tools/webgl_smoke.py [url] [width] [height]   (needs: pip install playwright; playwright install chromium)"""
import sys, time, json
from playwright.sync_api import sync_playwright
url = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:8090/index.html'
W = int(sys.argv[2]) if len(sys.argv) > 2 else 540
H = int(sys.argv[3]) if len(sys.argv) > 3 else 960
out = 'Logs/web/'
def tap(page, ax, ay):  # design coordinates of the 850x1850 Figma frame (origin top-left, y down; see Tools/build_scenes.py) -> viewport pixels
    s = min(W / 850.0, H / 1850.0)
    x, y = W / 2 + (ax - 425) * s, H / 2 + (ay - 925) * s
    page.mouse.move(x, y); time.sleep(0.12); page.mouse.down(); time.sleep(0.12); page.mouse.up()   # Unity needs real press timing
with sync_playwright() as p:
    b = p.chromium.launch(args=["--use-angle=swiftshader", "--use-gl=angle", "--enable-unsafe-swiftshader", "--ignore-gpu-blocklist", "--autoplay-policy=no-user-gesture-required"])
    ctx = b.new_context(viewport={'width': W, 'height': H}, device_scale_factor=1)
    page = ctx.new_page()
    logs = []
    page.on('console', lambda m: logs.append(m.type + ': ' + m.text))
    page.on('pageerror', lambda e: logs.append('PAGEERROR: ' + str(e)))
    t0 = time.time()
    page.goto(url)
    page.wait_for_function("document.getElementById('loading') && document.getElementById('loading').style.display === 'none'", timeout=240000)
    print('loaded in %.1fs' % (time.time() - t0))
    time.sleep(2)
    page.screenshot(path=out + 'menu.png')
    tap(page, 425, 1160)      # Play vs Bots
    # Play like a (dumb) human: always pass the bid (Bid7 may be disabled), pick spades, tap the middle card twice, dismiss popups.
    shots = 0
    sigs = []
    for i in range(int(sys.argv[4]) if len(sys.argv) > 4 else 60):
        tap(page, 645, 1208); time.sleep(0.3)      # BidPanel: Pass
        tap(page, 645, 1156); time.sleep(0.3)      # TrumpPicker: Spades
        for hx in range(40, 811, 60):              # scan the hand: a double tap plays the first legal card it lands on
            tap(page, hx, 1408); time.sleep(0.15); tap(page, hx, 1408); time.sleep(0.25)
        tap(page, 425, 1180); time.sleep(0.3)      # RoundSummary: Continue
        tap(page, 265, 1190); time.sleep(0.8)      # GameOver: Play again
        if True:
            page.screenshot(path=out + 'seq_%02d.png' % shots); shots += 1
    fps = page.evaluate("() => new Promise(r => { let n = 0; const t0 = performance.now(); const f = () => { n++; if (performance.now() - t0 > 3000) r(n / 3); else requestAnimationFrame(f); }; requestAnimationFrame(f); })")
    print('rAF fps ~', round(fps, 1))
    open(out + 'console.txt', 'w').write('\n'.join(logs))
    print('errors:', [l for l in logs if ('Exception' in l or 'PAGEERROR' in l or l.startswith('error'))][:10])
    b.close()
