"""Draws the completion finish textures (white, shape in alpha). Usage: make_fx.py <out_dir>"""
import os
import sys

import numpy as np
from PIL import Image


def grid(size):
    c = (np.arange(size) + 0.5) / size * 2.0 - 1.0
    return np.meshgrid(c, c)


def save(alpha, path):
    a = np.clip(alpha, 0.0, 1.0)
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.round(a * 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path, optimize=True)


def dot(size=64):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return np.exp(-(r / 0.42) ** 2) * (r < 1.0)


def glow(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return np.clip(1.0 - r, 0.0, 1.0) ** 2.2


# Trail strip: length along x, soft across y.
def trail(width=64, height=16):
    y = (np.arange(height) + 0.5) / height * 2.0 - 1.0
    across = np.exp(-(y / 0.5) ** 2)
    return np.tile(across[:, None], (1, width))


# Wide soft glow that falls off smoothly to the edge.
def halo(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return np.exp(-(r / 0.45) ** 2) * np.clip(1.0 - r, 0.0, 1.0)


# Disk bead and gem glint in the look of the orb glow layers: a bright rim ring round a hot core, inside a soft halo.
def bead(size=64):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    core = np.exp(-(r / 0.2) ** 2)
    rim = 0.75 * np.exp(-((r - 0.42) / 0.09) ** 2)
    halo = 0.3 * np.exp(-(r / 0.62) ** 2)
    return (core + rim + halo) * np.clip((1.0 - r) * 6.0, 0.0, 1.0)


# Bead trail, wispy like the portal ring's: soft across, with thin strands that drift along its length.
def wisp(width=128, height=32, seed=7):
    rng = np.random.default_rng(seed)
    x = (np.arange(width) + 0.5) / width
    y = (np.arange(height) + 0.5) / height * 2.0 - 1.0
    xx, yy = np.meshgrid(x, y)
    strands = np.zeros_like(xx)
    for _ in range(7):
        offset = rng.uniform(-0.55, 0.55)
        drift = rng.uniform(-0.25, 0.25)
        weight = rng.uniform(0.5, 1.0)
        strands += weight * np.exp(-((yy - offset - drift * xx) / 0.09) ** 2)
    strands /= strands.max()
    across = np.exp(-(yy / 0.6) ** 2)
    return across * (0.45 + 0.55 * strands)


# Fresnel rim of the singularity sphere, in the orbs' look: light along the whole edge, brighter toward the lower right.
def fresnel(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    rim = np.clip((r - 0.45) / 0.47, 0.0, 1.0) ** 3
    edge = np.clip((0.97 - r) / 0.05, 0.0, 1.0)
    # Rows run top to bottom, so +y is down: lower right is (+x, +y).
    toward = (x + y) / np.sqrt(2.0) / np.maximum(r, 1e-3)
    bias = 0.55 + 0.45 * np.clip(toward, -1.0, 1.0)
    return rim * edge * bias


# Gloss cap of the singularity sphere: a broad soft reflection toward the upper left with a small hot point, like the orbs.
def gloss(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    u = (x + 0.22) * np.cos(np.pi / 4) + (y + 0.3) * np.sin(np.pi / 4)
    v = -(x + 0.22) * np.sin(np.pi / 4) + (y + 0.3) * np.cos(np.pi / 4)
    cap = np.exp(-((u / 0.42) ** 2 + (v / 0.26) ** 2) ** 1.5) * 0.55
    point = np.exp(-(((x + 0.36) ** 2 + (y + 0.4) ** 2) / 0.07 ** 2))
    return (cap + point) * np.clip((0.9 - r) / 0.1, 0.0, 1.0)


# Thin photon ring hugging the singularity, one side brighter as if its light were beamed toward us.
def photon_ring(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    theta = np.arctan2(y, x)
    line = np.exp(-((r - 0.84) / 0.03) ** 2)
    glow = 0.3 * np.exp(-((r - 0.84) / 0.1) ** 2)
    beaming = 0.5 + 0.5 * np.cos(theta)
    return (line + glow) * (0.35 + 0.65 * beaming) * np.clip((1.0 - r) * 8.0, 0.0, 1.0)


# Lensing halo: a soft wide ring of bent light round the core, empty in the middle.
def lens(size=128):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return np.exp(-((r - 0.55) / 0.2) ** 2) * np.clip((1.0 - r) * 4.0, 0.0, 1.0)


# The singularity's dark core: solid, with a short soft edge.
def core(size=64):
    x, y = grid(size)
    r = np.sqrt(x * x + y * y)
    return np.clip((0.95 - r) / 0.2, 0.0, 1.0)


def main(out):
    os.makedirs(out, exist_ok=True)
    save(dot(), os.path.join(out, "Fx_Dot.png"))
    save(glow(), os.path.join(out, "Fx_Glow.png"))
    save(trail(), os.path.join(out, "Fx_Trail.png"))
    save(halo(), os.path.join(out, "Fx_Halo.png"))
    save(bead(), os.path.join(out, "Fx_Bead.png"))
    save(wisp(), os.path.join(out, "Fx_Wisp.png"))
    save(fresnel(), os.path.join(out, "Fx_Fresnel.png"))
    save(gloss(), os.path.join(out, "Fx_Gloss.png"))
    save(photon_ring(), os.path.join(out, "Fx_PhotonRing.png"))
    save(lens(), os.path.join(out, "Fx_Lens.png"))
    save(core(), os.path.join(out, "Fx_Core.png"))


if __name__ == "__main__":
    main(sys.argv[1])
