"""Cuts the mascot into rig parts and packs them with the ribbon and burst pieces into one Spine atlas page.

Usage: python make_win_atlas.py <project root>
Writes Assets/Art/Spine/WinFx/WinFx.png, WinFx.atlas.txt and WinFx.parts.json (source rects of the mascot parts).
The skeleton itself (bones, pivots, animations) is written by the editor builder, Tools/Project/Create Board Prefabs.
"""
import json
import os
import sys

from PIL import Image, ImageDraw

SUPER = 4
PAD = 2

# Mascot parts as polygons in Luno_512 pixel space, in draw order from back to front.
PARTS = {
    "body": [(118, 268), (150, 290), (200, 300), (260, 305), (310, 290), (338, 268), (345, 245), (348, 150), (372, 140),
             (398, 150), (398, 240), (392, 250), (380, 300), (372, 330), (365, 400), (340, 440), (300, 470), (250, 480),
             (200, 470), (150, 440), (125, 400), (112, 330)],
    "armL": [(88, 228), (134, 234), (138, 296), (118, 318), (92, 355), (60, 398), (28, 414), (2, 392), (0, 350), (40, 305),
             (70, 268)],
    "head": [(104, 250), (96, 200), (110, 150), (140, 110), (190, 88), (240, 80), (290, 84), (330, 100), (350, 125),
             (350, 160), (352, 200), (346, 240), (336, 270), (310, 292), (270, 305), (230, 308), (180, 300), (140, 285),
             (115, 268)],
    "antenna": [(228, 20), (276, 20), (276, 84), (228, 84)],
    "armR": [(330, 236), (380, 236), (392, 250), (394, 236), (390, 150), (412, 138), (430, 148), (470, 140), (500, 160),
             (508, 200), (490, 250), (470, 268), (466, 300), (430, 312), (400, 318), (372, 310), (340, 300), (326, 270)],
    "orb": [(447 + 30, 180), (447 + 21, 180 + 21), (447, 180 + 30), (447 - 21, 180 + 21), (447 - 30, 180), (447 - 21, 180 - 21),
            (447, 180 - 30), (447 + 21, 180 - 21)],
}


def cut(image, polygon):
    """The image inside the polygon, cropped to its bounds; the edge is antialiased."""
    w, h = image.size
    mask = Image.new("L", (w * SUPER, h * SUPER), 0)
    ImageDraw.Draw(mask).polygon([(x * SUPER, y * SUPER) for x, y in polygon], fill=255)
    mask = mask.resize((w, h), Image.LANCZOS)
    xs = [p[0] for p in polygon]
    ys = [p[1] for p in polygon]
    box = (max(min(xs), 0), max(min(ys), 0), min(max(xs), w), min(max(ys), h))
    part = image.crop(box)
    alpha = Image.eval(part.getchannel("A"), lambda a: a)
    cut_mask = mask.crop(box)
    alpha = Image.composite(alpha, Image.new("L", part.size, 0), cut_mask)
    part.putalpha(alpha)
    return part, box


def pack(regions, width):
    """Shelf packing; returns (page height, placements)."""
    x = y = shelf = PAD
    placed = {}
    for name, img in regions:
        if x + img.width + PAD > width:
            x = PAD
            y += shelf + PAD
            shelf = 0
        placed[name] = (x, y)
        x += img.width + PAD
        shelf = max(shelf, img.height)
    return y + shelf + PAD, placed


def main(root):
    sprites = os.path.join(root, "Assets", "Art", "Sprites")
    out_dir = os.path.join(root, "Assets", "Art", "Spine", "WinFx")
    os.makedirs(out_dir, exist_ok=True)
    luno = Image.open(os.path.join(sprites, "UI", "Journey", "Luno_512.png")).convert("RGBA")
    regions = []
    parts = {}
    for name, polygon in PARTS.items():
        part, box = cut(luno, polygon)
        regions.append((name, part))
        parts[name] = {"x": box[0], "y": box[1], "w": part.width, "h": part.height}
    regions.append(("ribbon", Image.open(os.path.join(sprites, "UI", "Popup", "UI_Popup_Ribbon_Win.png")).convert("RGBA")))
    regions.append(("halo", Image.open(os.path.join(sprites, "Fx", "Fx_Halo.png")).convert("RGBA")))
    regions.append(("spark", Image.open(os.path.join(sprites, "Fx", "Fx_Trail.png")).convert("RGBA")))
    regions.sort(key=lambda r: -r[1].height)

    width = 1024
    height, placed = pack(regions, width)
    page = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    lines = ["WinFx.png", f"size: {width},{height}", "filter: Linear,Linear", "pma: false"]
    for name, img in regions:
        x, y = placed[name]
        page.paste(img, (x, y))
        lines += [name, f"bounds: {x},{y},{img.width},{img.height}"]
    page.save(os.path.join(out_dir, "WinFx.png"), optimize=True)
    with open(os.path.join(out_dir, "WinFx.atlas.txt"), "w", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    with open(os.path.join(out_dir, "WinFx.parts.json"), "w", newline="\n") as f:
        json.dump(parts, f, indent=2)
    print(f"{width}x{height}, {len(regions)} regions")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", ".."))
