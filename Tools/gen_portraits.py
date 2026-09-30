#!/usr/bin/env python3
"""Builds clean circular player portraits: Resources/UI/portrait_<name>.png (288 px).
The Figma export has neighbouring design layers burnt into the portrait art (Sara: the turn-timer bezel, Ahmad: a piece of the
VIP crown), so those two are retouched with inpainting. Omar and Khalid are used as exported. Needs opencv-python-headless."""
import os
import numpy as np
import cv2
from PIL import Image
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SRC = os.path.join(ROOT, 'Assets', 'FigmaImports', 'Tarneeb VIP _ Unity Gameplay UI')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'UI')
SOURCES = {'omar': '23_806', 'sara': '23_817', 'khalid': '23_828', 'ahmad': '23_839'}

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
