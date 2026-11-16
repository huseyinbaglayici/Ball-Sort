"""Slices the front half of the tube mouth out of Empty_Tube_Cap.png into Tube_RimFront.png.

The whole cap is drawn below the balls; this slice is drawn again over them, so a ball passing through the mouth
is half inside: the opening's back rim and its inside stay behind the ball, the front lip and the gold rings cover it.
The cut follows the lower arc of the opening and, outside the opening, the rim's center line.

Usage: python slice_mouth.py <Empty_Tube_Cap.png> <Tube_RimFront.png> [debug_overlay.png]
"""
import sys

import numpy as np
from PIL import Image

# The opening of Empty_Tube_Cap (384x1024, pixels from the top-left), measured from the art: the lip highlight sits
# just below this arc, so it stays in the front slice.
CX, CY = 192.0, 36.0
A, B = 136.0, 16.0
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
