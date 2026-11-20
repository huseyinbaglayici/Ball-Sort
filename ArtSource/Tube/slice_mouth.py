"""Cuts the mouth's front rim to draw over the balls. Usage: slice_mouth.py <src.png> <dst.png> [debug.png]"""
import sys

import numpy as np
from PIL import Image

# Opening of the 384x1024 cap, measured from the art; the lip highlight sits just below this arc.
CX, CY = 193.5, 41.0
A, B = 135.0, 20.0
FEATHER = 1.0  # px of soft edge along the cut


def cut_line(width):
    dx = np.arange(width) + 0.5 - CX
    inside = np.clip(1.0 - (dx / A) ** 2, 0.0, None)
    return CY + B * np.sqrt(inside)


def main(src, dst, debug=None):
    im = np.array(Image.open(src).convert("RGBA")).astype(np.float32)
    h, w = im.shape[:2]
    y = np.arange(h)[:, None] + 0.5
    mask = np.clip((y - cut_line(w)[None, :]) / FEATHER + 0.5, 0.0, 1.0)
    out = im.copy()
    out[..., 3] *= mask
    out[out[..., 3] < 0.5] = 0  # no stray color in fully transparent pixels
    Image.fromarray(out.round().astype(np.uint8), "RGBA").save(dst, optimize=True)

    if debug:
        bg = Image.new("RGBA", (w, h), (255, 0, 255, 255))
        bg.alpha_composite(Image.fromarray(im.astype(np.uint8), "RGBA"))
        dbg = np.array(bg)
        line = cut_line(w).round().astype(int)
        dbg[line.clip(0, h - 1), np.arange(w)] = (0, 255, 0, 255)
        Image.fromarray(dbg).crop((0, 0, w, 170)).resize((w * 3, 510), Image.NEAREST).save(debug)


if __name__ == "__main__":
    main(*sys.argv[1:])
