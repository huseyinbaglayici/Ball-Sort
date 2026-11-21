"""Splits the space background into a star-free nebula and a sheet of cut-out stars.

Usage: split_stars.py <source.png> <out_dir>
Writes BG_Space_Nebula.png, BG_Space_StarSheet.png and BG_Space_Stars.json (star centres on the source,
pixels from the top-left, and each star's rect on the sheet, pixels from the bottom-left as Unity reads it).
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

# A pixel belongs to a star when it is this much brighter than its local background (0..1 luma).
THRESHOLD = 0.07
# Local background window; wider than the biggest star.
WINDOW = 15
# Extra pixels kept around each star so its glow is cut out with it.
PAD = 3
SHEET_WIDTH = 512


def luma(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def median(image, size):
    return np.asarray(image.filter(ImageFilter.MedianFilter(size)), np.float32) / 255.0


def components(mask):
    """4-connected regions as lists of (y, x)."""
    seen = np.zeros_like(mask, bool)
    h, w = mask.shape
    regions = []
    for y0, x0 in zip(*np.nonzero(mask)):
        if seen[y0, x0]:
            continue
        stack, region = [(y0, x0)], []
        seen[y0, x0] = True
        while stack:
            y, x = stack.pop()
            region.append((y, x))
            for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        regions.append(region)
    return regions


def main(source, out_dir):
    image = Image.open(source).convert("RGB")
    rgb = np.asarray(image, np.float32) / 255.0
    background = median(image, WINDOW)
    lift = luma(rgb) - luma(background)

    core = lift > THRESHOLD
    grown = np.asarray(Image.fromarray(core.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(2 * PAD + 1))) > 0

    # Nebula: star pixels replaced by the local background, softened so no outline is left.
    soft = np.asarray(Image.fromarray((background * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32) / 255.0
    nebula = np.where(grown[..., None], soft, rgb)
    Image.fromarray(np.round(nebula * 255).astype(np.uint8), "RGB").save(os.path.join(out_dir, "BG_Space_Nebula.png"), optimize=True)

    # Star colour and coverage: what the star adds over the nebula.
    alpha = np.clip(lift / np.maximum(1e-4, 1.0 - luma(soft)), 0.0, 1.0)
    alpha = np.where(grown, alpha, 0.0)
    colour = np.clip((rgb - soft * (1.0 - alpha[..., None])) / np.maximum(alpha[..., None], 1e-4), 0.0, 1.0)
    stars_rgba = np.dstack([colour, alpha])

    regions = [r for r in components(grown) if any(core[y, x] for y, x in r)]
    crops = []
    for region in regions:
        ys = [p[0] for p in region]
        xs = [p[1] for p in region]
        y0, y1, x0, x1 = min(ys), max(ys) + 1, min(xs), max(xs) + 1
        crop = stars_rgba[y0:y1, x0:x1].copy()
        weight = crop[..., 3]
        if weight.sum() <= 0:
            continue
        cy = (np.arange(y0, y1)[:, None] * weight).sum() / weight.sum()
        cx = (np.arange(x0, x1)[None, :] * weight).sum() / weight.sum()
        crops.append((crop, float(cx) + 0.5, float(cy) + 0.5, x0, y0))

    # Shelf-pack the cut-outs into one sheet.
    crops.sort(key=lambda c: -c[0].shape[0])
    x = y = shelf = 0
    placed = []
    for crop, cx, cy, x0, y0 in crops:
        h, w = crop.shape[:2]
        if x + w + 1 > SHEET_WIDTH:
            x, y, shelf = 0, y + shelf + 1, 0
        placed.append((crop, cx, cy, x0, y0, x, y))
        x += w + 1
        shelf = max(shelf, h)
    sheet_h = 1
    while sheet_h < y + shelf + 1:
        sheet_h *= 2
    sheet = np.zeros((sheet_h, SHEET_WIDTH, 4), np.float32)
    stars = []
    for crop, cx, cy, x0, y0, sx, sy in placed:
        h, w = crop.shape[:2]
        sheet[sy:sy + h, sx:sx + w] = crop
        stars.append({
            "x": round(cx, 2), "y": round(cy, 2),
            # Rect on the sheet from its bottom-left; pivot is the star centre inside that rect.
            "rect": [sx, sheet_h - sy - h, w, h],
            "pivot": [round((cx - x0) / w, 4), round(1.0 - (cy - y0) / h, 4)],
        })
    Image.fromarray(np.round(sheet * 255).astype(np.uint8), "RGBA").save(os.path.join(out_dir, "BG_Space_StarSheet.png"), optimize=True)
    with open(os.path.join(out_dir, "BG_Space_Stars.json"), "w", encoding="utf-8") as f:
        json.dump({"width": image.width, "height": image.height, "stars": stars}, f, separators=(",", ":"))
    print(f"{len(stars)} stars, sheet {SHEET_WIDTH}x{sheet_h}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
