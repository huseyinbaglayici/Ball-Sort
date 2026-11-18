"""Warp portal ring, drawn as seen from below and then flipped to match the board camera looking down."""
import os
import numpy as np
from PIL import Image, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Sprites", "Hole")

FLATTEN = 0.45        # BoardView.RingFlatten
OPENING = 0.78        # opening width / ring width
THICK = 0.085         # side wall height above the face, share of the canvas width
DEPTH = 0.75          # how far up the visible inner wall reaches, share of the opening's half height

SS = 2
FW = 1024
PADF = 10
AL = FW * 0.5 - PADF                       # ring half width (final px)
BL = AL * FLATTEN
T = FW * THICK
FH = int(np.ceil(PADF + T + BL + BL + PADF))
W, H = FW * SS, FH * SS
CX, CY = W * 0.5, (PADF + T + BL) * SS     # face center: the portal plane

al, bl = AL * SS, BL * SS
ao, bo = al * OPENING, bl * OPENING
t_px = T * SS
d_px = bo * DEPTH
BG = np.array([27, 21, 69]) / 255.0       # board background, preview only

y, x = np.mgrid[0:H, 0:W].astype(np.float64) + 0.5


def ellipse(cx, cy, a, b):
    u, v = (x - cx) / a, (y - cy) / b
    r = np.sqrt(u * u + v * v) + 1e-9
    grad = np.sqrt((u / a) ** 2 + (v / b) ** 2) / r
    return u, v, (r - 1.0) / grad / SS     # signed distance in final pixels


def smooth(e0, e1, t):
    t = np.clip((t - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def periodic_noise(s, t, ns, nt, seed):
    """Value noise, wrapping in s (0..1), clamped in t (0..1)."""
    g = np.random.default_rng(seed).random((nt + 2, ns))
    fs = (s % 1.0) * ns
    ft = np.clip(t, 0, 1) * nt
    i0, j0 = np.floor(fs).astype(int) % ns, np.floor(ft).astype(int)
    i1, j1 = (i0 + 1) % ns, np.minimum(j0 + 1, nt + 1)
    a, b = fs - np.floor(fs), ft - j0
    a, b = a * a * (3 - 2 * a), b * b * (3 - 2 * b)
    top = g[j0, i0] * (1 - a) + g[j0, i1] * a
    bot = g[j1, i0] * (1 - a) + g[j1, i1] * a
    return top * (1 - b) + bot * b


def fbm(s, t, seed, octaves=4, ns=8, nt=3):
    total, amp, norm = 0.0, 1.0, 0.0
    for o in range(octaves):
        total = total + amp * periodic_noise(s, t, ns * 2 ** o, nt * 2 ** o, seed + o)
        norm += amp
        amp *= 0.55
    return total / norm


RAMP_T = np.array([0.0, 0.18, 0.4, 0.62, 0.8, 0.93, 1.0])
RAMP_C = np.array([
    [8, 4, 22],
    [45, 22, 120],
    [120, 50, 220],
    [225, 70, 205],
    [255, 140, 70],
    [255, 205, 95],
    [255, 250, 235],
]) / 255.0


def ramp(i):
    i = np.clip(i, 0, 1)
    return np.stack([np.interp(i, RAMP_T, RAMP_C[:, c]) for c in range(3)], -1)


def over(dst_rgb, dst_a, src_rgb, src_a):
    out_a = src_a + dst_a * (1 - src_a)
    out = (src_rgb * src_a[..., None] + dst_rgb * (dst_a * (1 - src_a))[..., None]) / np.maximum(out_a, 1e-6)[..., None]
    return out, out_a


fu, fv, d_face = ellipse(CX, CY, al, bl)           # underside, outer edge
ou, ov, d_open = ellipse(CX, CY, ao, bo)
_, _, d_top = ellipse(CX, CY - t_px, al, bl)       # top edge of the side wall
_, _, d_deep = ellipse(CX, CY - d_px, ao, bo)      # where the visible inner wall meets the void

in_face = smooth(0.6, -0.6, d_face)
in_open = smooth(0.6, -0.6, d_open)
in_top = smooth(0.6, -0.6, d_top)
in_deep = smooth(0.6, -0.6, d_deep)
near_half = smooth(1.0 * SS, -1.0 * SS, y - CY)    # the near side: above the face's center line on screen

theta = np.arctan2(fv, fu)
doppler = 1.0 + 0.3 * np.cos(theta - np.deg2rad(-20))   # approaching side: right

# Underside, lit only by the hole; k is 0 at the opening's edge, 1 at the outer edge.
k = np.clip(d_open / np.maximum(d_open - d_face, 1e-6), 0, 1)
spiral = theta / (2 * np.pi) + 0.14 * k ** 1.2
swirl = fbm(spiral, k, seed=11, ns=7, nt=2)
streak = fbm(spiral + 0.37, k, seed=31, ns=48, nt=2, octaves=3)
matter = smooth(0.3, 0.8, swirl) * 0.55 + streak * 0.45
spill = np.exp(-k / 0.28)                                   # light spilling out of the void
heat = (0.16 + 0.62 * spill) * (0.55 + 0.7 * matter) * doppler
heat = np.maximum(heat, np.exp(-((d_open - 1.4) / 1.3) ** 2) * 0.98)    # white-hot photon ring on the lip
rim = np.exp(-((d_face + 1.3) / 1.1) ** 2) * 0.45                       # thin rim light on the outer edge
face_rgb = ramp(heat) + rim[..., None] * np.array([0.65, 0.55, 1.0])
face_a = in_face * (1 - in_open)

wall_a = in_top * (1 - in_face) * near_half
# 0 at the face's near edge, 1 at the top of the wall; shaded like a cylinder seen from below
face_top_y = CY - bl * np.sqrt(np.clip(1 - fu * fu, 0, 1))
wall_t = np.clip((face_top_y - y) / t_px, 0, 1)
side = np.sqrt(np.clip(1 - fu * fu, 0, 1))
lit = (0.3 + 0.7 * side) * (0.75 - 0.45 * wall_t) * (0.85 + 0.25 * np.clip(fu + 0.3, 0, 1))
wall_rgb = np.array([52, 28, 128]) / 255.0 * lit[..., None]
wall_rgb = wall_rgb + np.exp(-(wall_t / 0.18) ** 2)[..., None] * np.array([0.5, 0.25, 0.75]) * 0.35   # glow off the face
wall_rgb = wall_rgb + np.exp(-((d_top + 1.2) / 1.0) ** 2)[..., None] * np.array([0.5, 0.4, 0.95]) * 0.4  # top rim light

# The far inner wall: the band between the lip and the same ellipse moved up by the depth.
inner = in_open * (1 - in_deep)
lip_y = CY + bo * np.sqrt(np.clip(1 - ou * ou, 0, 1))      # lower (far) arc of the opening
depth_t = np.clip((lip_y - y) / d_px, 0, 1)                 # 0 at the lip, 1 deep up the tunnel
rise = fbm(ou * 0.5 + 0.5 - depth_t * 0.12, depth_t, seed=53, ns=40, nt=2, octaves=3)
glow = (1 - depth_t) ** 1.8 * (0.3 + 0.8 * rise) * doppler * 0.85
tunnel_rgb = ramp(glow) * smooth(0.0, 0.3, glow)[..., None]
# Fainter rings further up the tunnel: the same opening, smaller and higher each time.
rings = np.zeros_like(x)
for step, (s, lift, a) in enumerate([(0.74, 1.35, 0.30), (0.52, 1.95, 0.18), (0.34, 2.45, 0.10)]):
    _, _, d_ring = ellipse(CX, CY - bo * lift * DEPTH, ao * s, bo * s)
    rings = np.maximum(rings, np.exp(-(d_ring / 1.4) ** 2) * a)
rings = rings * in_deep
tunnel_rgb = tunnel_rgb * inner[..., None] + ramp(rings * 1.4) * rings[..., None]
open_rgb = tunnel_rgb                                       # everything else in the opening is pure black

back_rgb, back_a = over(open_rgb, in_open, face_rgb, face_a)
front_rgb, front_a = over(wall_rgb, wall_a, face_rgb, face_a * near_half)


def to_img(rgb, a):
    arr = np.concatenate([np.clip(rgb, 0, 1), np.clip(a, 0, 1)[..., None]], -1)
    im = Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")
    return im.resize((FW, FH), Image.LANCZOS)


os.makedirs(OUT, exist_ok=True)
# Built seen from below; turned upside down it is the ring seen from above.
back = ImageOps.flip(to_img(back_rgb, back_a))
front = ImageOps.flip(to_img(front_rgb, front_a))
PLANE_Y = FH - CY / SS                                      # face center (pivot), from the top, after the flip
back.save(os.path.join(OUT, "Hole_Back.png"))
front.save(os.path.join(OUT, "Hole_Front.png"))

# Preview only: the game cuts the ball at the ring's lowest edge and the ring hides the rest.


def ball_img(size, cut_y):
    """size: diameter px; cut_y: plane y in the ball image's pixel space."""
    s = size
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float64) + 0.5
    nx, ny = (xx - s / 2) / (s / 2), (yy - s / 2) / (s / 2)
    rr = nx * nx + ny * ny
    nz = np.sqrt(np.clip(1 - rr, 0, 1))
    light = np.clip(-0.45 * nx - 0.55 * ny + 0.7 * nz, 0, 1)
    base = np.array([0.15, 0.85, 0.95])
    rgb = base * (0.25 + 0.85 * light[..., None]) + (light ** 18)[..., None] * 0.9
    a = smooth(1.0, 0.96, rr)
    half = np.sqrt(max((s / 2) ** 2 - (cut_y - s / 2) ** 2, 0.0))
    arc = FLATTEN * np.sqrt(np.clip(half * half - (xx - s / 2) ** 2, 0, None))
    a = a * smooth(-0.8, 0.8, yy - (cut_y - arc))
    arr = np.concatenate([np.clip(rgb, 0, 1), a[..., None]], -1)
    return Image.fromarray((arr * 255 + 0.5).astype(np.uint8), "RGBA")


frames = []
ball_d = int(FW / 1.6)
plane_y = int(round(PLANE_Y))
for rise_by in (-0.15, 0.2, 0.5, 0.8):                     # share of the ball above the plane
    top = int(plane_y - rise_by * ball_d)
    canvas_h = FH + ball_d + 40
    frame = Image.new("RGBA", (FW, canvas_h), tuple(int(c * 255) for c in BG) + (255,))
    frame.alpha_composite(ball_img(ball_d, plane_y - top), ((FW - ball_d) // 2, top))
    frame.alpha_composite(back, (0, 0))
    frame.alpha_composite(front, (0, 0))
    frames.append(frame.resize((FW // 2, canvas_h // 2), Image.LANCZOS))
sheet = Image.new("RGB", (sum(f.width for f in frames), frames[0].height))
for i, f in enumerate(frames):
    sheet.paste(f.convert("RGB"), (i * f.width, 0))
sheet.save(os.path.join(HERE, "Hole_BallTest.png"))

preview = Image.new("RGBA", back.size, tuple(int(c * 255) for c in BG) + (255,))
preview.alpha_composite(back)
preview.alpha_composite(front)
preview.convert("RGB").save(os.path.join(HERE, "Hole_Comp.png"))
print("ok", back.size, "pivot (Unity, from bottom-left) = (0.5, %.4f)" % (1 - PLANE_Y / FH))
