#!/usr/bin/env python3
"""Builds clean circular player portraits: Resources/UI/portrait_<name>.png (288 px).
The Figma export has neighbouring design layers burnt into the portrait art (Sara: the turn-timer bezel, Ahmad: a piece of the
VIP crown), so those two are retouched with inpainting. Omar and Khalid are AI-generated (Tools/design/portraits/*.jpg, made with the free
Pollinations image service, FLUX model; prompts in DEVLOG.md): modern clothes, no headscarf. They are cropped square and masked to a circle
(the service's corner watermark falls outside the crop). Needs opencv-python-headless."""
import os
import numpy as np
import cv2
from PIL import Image
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SRC = os.path.join(ROOT, 'Assets', 'FigmaImports', 'Tarneeb VIP _ Unity Gameplay UI')
OUT = os.path.join(ROOT, 'Assets', 'Core Game Assets', 'Resources', 'UI')
SOURCES = {}                                         # all four are AI-generated now (same style); the Figma retouching path is kept for reference
GENERATED = ['ahmad', 'omar', 'khalid', 'sara']

def ellipse_mask(shape, cx, cy, rx, ry):
    m = np.zeros(shape[:2], np.uint8)
    cv2.ellipse(m, (cx, cy), (rx, ry), 0, 0, 360, 255, -1)
    return m

for name, node in SOURCES.items():
    im = Image.open(os.path.join(SRC, node + '.png')).convert('RGBA')
    rgba = np.array(im)
    rgb = cv2.cvtColor(rgba[:, :, :3], cv2.COLOR_RGB2BGR)
    mask = None
    if name == 'sara':                                     # timer bezel + digits, top-left of the circle
        mask = ellipse_mask(rgb.shape, 56, 62, 74, 70)
        mask[:, 140:] = 0
    elif name == 'ahmad':                                  # gold VIP crown fragment at the right edge
        mask = np.zeros(rgb.shape[:2], np.uint8)
        cv2.rectangle(mask, (228, 176), (287, 262), 255, -1)
        mask = cv2.GaussianBlur(mask, (0, 0), 1.2)
        mask = (mask > 20).astype(np.uint8) * 255
    if mask is not None:
        mask = cv2.dilate(mask, np.ones((5, 5), np.uint8))
        rgb = cv2.inpaint(rgb, mask, 6, cv2.INPAINT_TELEA)
        # soften the retouched area a little so the fill does not look stamped
        blur = cv2.GaussianBlur(rgb, (0, 0), 2.0)
        soft = cv2.GaussianBlur(mask, (0, 0), 3.0).astype(np.float32)[:, :, None] / 255.0
        rgb = (rgb * (1 - 0.6 * soft) + blur * (0.6 * soft)).astype(np.uint8)
    out = np.dstack([cv2.cvtColor(rgb, cv2.COLOR_BGR2RGB), rgba[:, :, 3]])
    Image.fromarray(out, 'RGBA').save(os.path.join(OUT, 'portrait_%s.png' % name), optimize=True)
    print('wrote portrait', name)

from PIL import ImageDraw
for name in GENERATED:
    im = Image.open(os.path.join(ROOT, 'Tools', 'design', 'portraits', name + '.jpg')).convert('RGB')
    w, h = im.size
    box = (int(w * 0.08), int(h * 0.03), int(w * 0.08) + int(w * 0.82), int(h * 0.03) + int(w * 0.82))
    im = im.crop(box).resize((288, 288), Image.LANCZOS).convert('RGBA')
    m = Image.new('L', (288 * 4, 288 * 4), 0); ImageDraw.Draw(m).ellipse([0, 0, 288 * 4 - 1, 288 * 4 - 1], fill=255)
    im.putalpha(m.resize((288, 288), Image.LANCZOS))
    im.save(os.path.join(OUT, 'portrait_%s.png' % name), optimize=True)
    print('wrote generated portrait', name)
