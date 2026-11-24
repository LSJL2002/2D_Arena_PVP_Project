"""
Procedural chunky pixel-art sprite sheet generator.

Each character is a set of 3D capsules/spheres (in pixel units) defined in the
character's local space (x = right, y = forward, z = up). For every direction
the model is rotated, projected with a top-down 3/4 camera, depth sorted and
rasterised straight onto the pixel grid (no anti-aliasing), with per-part dark
outlines and 3-tone shading.

Sheet layout: rows = directions (S, SE, E, NE, N, NW, W, SW, top to bottom),
columns = walk frames.

Usage (from the project root):
    python Tools/SpriteGen/spritegen.py Tools/SpriteGen/out
then copy the *_Walk.png files over the ones in Assets/03-Assets/Characters/.
Unity re-slices them automatically (Assets/00-Scripts/Editor/CharacterSpriteImporter.cs)
and keeps the same sprite IDs, so existing references don't break.
"""
import math
import os
import sys
import numpy as np
from PIL import Image, ImageDraw

OUTLINE = (22, 15, 24)
DIRS = [("S", (0, -1)), ("SE", (1, -1)), ("E", (1, 0)), ("NE", (1, 1)),
        ("N", (0, 1)), ("NW", (-1, 1)), ("W", (-1, 0)), ("SW", (-1, -1))]
FRAMES = 4
TILT = math.radians(40)


def hexc(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mix(c, t, k):
    return tuple(int(round(a + (b - a) * k)) for a, b in zip(c, t))


def tones(base):
    base = hexc(base) if isinstance(base, str) else base
    light = mix(base, (255, 245, 225), 0.28)
    dark = mix(base, (20, 10, 30), 0.38)
    return light, base, dark


class Part:
    def __init__(self, a, b, r, color, outline=True, shade=True, bias=0.0):
        self.a, self.b, self.r = a, b, r
        self.tones = tones(color)
        self.outline, self.shade, self.bias = outline, shade, bias


def cap(a, b, r, color, **kw):
    return Part(a, b, r, color, **kw)


def ball(c, r, color, **kw):
    return Part(c, c, r, color, **kw)


# --------------------------------------------------------------- renderer

def to_world(p, f):
    fx, fy = f
    n = math.hypot(fx, fy)
    fx, fy = fx / n, fy / n
    rx, ry = fy, -fx                      # character's right-hand side
    lx, ly, lz = p
    return (lx * rx + ly * fx, lx * ry + ly * fy, lz)


def project(w, origin):
    X, Y, Z = w
    sx = origin[0] + X
    sy = origin[1] - (Z * math.cos(TILT) + Y * math.sin(TILT))
    depth = -Y * math.cos(TILT) + Z * math.sin(TILT)
    return sx, sy, depth


def render(parts, size, origin, facing):
    img = np.zeros((size, size, 4), dtype=np.uint8)
    ys, xs = np.mgrid[0:size, 0:size]
    px = xs + 0.5
    py = ys + 0.5
    light_dir = np.array([-0.55, -0.83])

    items = []
    for i, part in enumerate(parts):
        a = project(to_world(part.a, facing), origin)
        b = project(to_world(part.b, facing), origin)
        items.append(((a[2] + b[2]) / 2 + part.bias, i, part, a, b))
    items.sort(key=lambda t: (t[0], t[1]))

    for _, _, part, a, b in items:
        ax, ay, bx, by = a[0], a[1], b[0], b[1]
        dx, dy = bx - ax, by - ay
        L2 = dx * dx + dy * dy
        if L2 < 1e-9:
            t = np.zeros_like(px)
        else:
            t = np.clip(((px - ax) * dx + (py - ay) * dy) / L2, 0, 1)
        qx, qy = ax + t * dx, ay + t * dy
        ox, oy = px - qx, py - qy
        d = np.sqrt(ox * ox + oy * oy)
        r = part.r
        if part.outline:
            ring = d <= r + 1.0
            img[ring] = (*OUTLINE, 255)
        fill = d <= r
        light, base, dark = part.tones
        if part.shade and r >= 1.2:
            nd = (ox * light_dir[0] + oy * light_dir[1]) / max(r, 1e-6)
            col = np.empty((size, size, 3), dtype=np.uint8)
            col[:] = base
            col[nd > 0.42] = light
            col[nd < -0.38] = dark
            img[fill, :3] = col[fill]
        else:
            img[fill, :3] = base
        img[fill, 3] = 255

    # final silhouette outline
    alpha = img[:, :, 3] > 0
    grown = alpha.copy()
    grown[1:, :] |= alpha[:-1, :]
    grown[:-1, :] |= alpha[1:, :]
    grown[:, 1:] |= alpha[:, :-1]
    grown[:, :-1] |= alpha[:, 1:]
    edge = grown & ~alpha
    img[edge] = (*OUTLINE, 255)
    return img


def sheet(model, size, origin):
    out = np.zeros((size * len(DIRS), size * FRAMES, 4), dtype=np.uint8)
    for row, (_, f) in enumerate(DIRS):
        for col in range(FRAMES):
            phase = col / FRAMES * 2 * math.pi
            parts = model(phase)
            fr = render(parts, size, origin, f)
            out[row * size:(row + 1) * size, col * size:(col + 1) * size] = fr
    return Image.fromarray(out, "RGBA")


# ----------------------------------------------------------------- models

def humanoid(phase, c, stride=3.0, arm_mode="swing", lean=0.0, head_tilt=(0, 0),
             sway=0.0, scale=1.0, extra=None):
    s, co = math.sin(phase), math.cos(phase)
    bob = abs(s) * 0.7
    k = scale

    def P(x, y, z):
        return (x * k, y * k, z * k)

    parts = []
    # legs
    for side, sgn in ((-1, 1), (1, -1)):
        foot_y = sgn * s * stride
        foot_z = 1.4 + max(0.0, sgn * co) * 1.6
        parts.append(cap(P(side * 2.0, 0, 8 + bob), P(side * 2.1, foot_y, foot_z),
                         2.0 * k, c["legs"]))
        parts.append(ball(P(side * 2.1, foot_y + 0.6, foot_z - 0.2), 1.9 * k, c["boots"]))
    # torso
    sx = sway * s
    parts.append(cap(P(sx, lean * 0.3, 9 + bob), P(sx, lean, 14 + bob), 4.1 * k, c["body"]))
    if "belt" in c:
        parts.append(cap(P(-3.6 + sx, lean * 0.4, 9.6 + bob), P(3.6 + sx, lean * 0.4, 9.6 + bob),
                         1.2 * k, c["belt"], outline=False))
    # arms
    for side, sgn in ((-1, -1), (1, 1)):
        sh = P(side * 5.2 + sx, lean, 14 + bob)
        if arm_mode == "forward":
            hand = P(side * 3.6 + sx, 6.5 + lean, 13.2 + bob + sgn * s * 0.6)
        else:
            hand = P(side * 5.6 + sx, sgn * s * 2.8 + lean, 9.4 + bob)
        parts.append(cap(sh, hand, 1.7 * k, c["arms"]))
        parts.append(ball(hand, 1.8 * k, c["hands"]))
    # head
    hx, hy = head_tilt
    head = P(hx + sx, 0.4 + lean + hy, 20 + bob)
    parts.append(ball(head, 5.2 * k, c["head"]))
    if extra:
        parts += extra(P, head, bob, s, co, sx)
    return parts


# --- Player 1: dark armour, red visor band, gold gauntlets (matches reference)
P1 = dict(legs="#33333f", boots="#1f1f27", body="#40404f", belt="#d9a441",
          arms="#4b4b5e", hands="#e0b04c", head="#3d3d4c")


def p1_extra(P, head, bob, s, co, sx):
    hx, hy, hz = head
    return [
        # visor band wraps round the helmet (only the front shows)
        cap((hx - 4.0, hy + 3.3, hz + 0.6), (hx + 4.0, hy + 3.3, hz + 0.6), 1.25, "#d8312f",
            outline=False, bias=0.05),
        ball((hx, hy + 4.6, hz + 0.6), 1.2, "#ff6b4a", outline=False, shade=False, bias=0.1),
        # red scarf tail at the back
        cap((hx, hy - 4.0, hz - 3.5), (hx + 1.5, hy - 7.0, hz - 6.5), 1.2, "#c22a2a",
            outline=True, bias=-0.1),
        # gold crest
        cap((hx, hy + 1.5, hz + 5.0), (hx, hy - 3.0, hz + 4.2), 1.1, "#e0b04c", bias=0.05),
    ]


# --- Player 2: ivory armour, cyan visor, steel blue trim
P2 = dict(legs="#3b4f7a", boots="#25304d", body="#d9d4c4", belt="#4fc3e8",
          arms="#c9c3b2", hands="#8fa3c2", head="#e3ded0")


def p2_extra(P, head, bob, s, co, sx):
    hx, hy, hz = head
    return [
        cap((hx - 3.6, hy + 3.6, hz + 0.3), (hx + 3.6, hy + 3.6, hz + 0.3), 1.2, "#36d1f0",
            outline=False, bias=0.05),
        # side "ears" of the helmet
        ball((hx - 5.0, hy, hz), 1.5, "#3b4f7a", bias=0.0),
        ball((hx + 5.0, hy, hz), 1.5, "#3b4f7a", bias=0.0),
        # blue plume
        cap((hx, hy + 0.5, hz + 5.2), (hx, hy - 4.5, hz + 3.5), 1.4, "#3b6fd1", bias=0.05),
    ]


def eyes(head, color, fwd=4.3, spread=1.9, z=0.4, r=0.9):
    hx, hy, hz = head
    return [ball((hx - spread, hy + fwd, hz + z), r, color, outline=False, shade=False, bias=0.1),
            ball((hx + spread, hy + fwd, hz + z), r, color, outline=False, shade=False, bias=0.1)]


# --- Zombie
ZB = dict(legs="#3f4c6b", boots="#2c2b2b", body="#6e5236", arms="#7ea35b",
          hands="#8fb36a", head="#86ad63")


def zombie_extra(P, head, bob, s, co, sx):
    hx, hy, hz = head
    return eyes(head, "#f3e37a", r=0.85) + [
        # torn shirt patches / wounds
        ball((sx - 1.5, 3.6, 12.5 + bob), 1.1, "#9b2b2b", outline=False, shade=False, bias=0.05),
        ball((sx + 2.0, 3.4, 10.4 + bob), 0.9, "#7ea35b", outline=False, shade=False, bias=0.05),
        # messy dark hair tuft
        cap((hx - 2.5, hy - 1, hz + 4.2), (hx + 2.0, hy - 2, hz + 4.6), 1.4, "#2f3a24", bias=0.02),
        # open mouth
        ball((hx + 0.5, hy + 4.4, hz - 2.0), 0.8, "#3a1414", outline=False, shade=False, bias=0.1),
    ]


# --- Demon (bigger)
DM = dict(legs="#8f2620", boots="#241414", body="#b5342b", arms="#a92f27",
          hands="#c8443a", head="#bd3a30")


def demon_extra(P, head, bob, s, co, sx):
    hx, hy, hz = head
    flap = 1.5 * math.sin(2 * math.atan2(s, co) if False else (s * 2))
    parts = eyes(head, "#ffd23f", fwd=5.6, spread=2.4, z=0.6, r=1.0)
    # horns
    for side in (-1, 1):
        parts.append(cap((hx + side * 3.5, hy + 1, hz + 4.5), (hx + side * 6.5, hy + 0.5, hz + 8.5),
                         1.5, "#2b1d1d", bias=0.02))
        parts.append(cap((hx + side * 6.5, hy + 0.5, hz + 8.5), (hx + side * 6.0, hy + 1.5, hz + 11.5),
                         1.0, "#3b2a2a", bias=0.02))
    # wings: bones + membrane, behind the body
    for side in (-1, 1):
        root = (side * 4.0 + sx, -3.0, 18 + bob)
        tip = (side * 15.0 + sx, -6.0, 26 + bob + flap)
        parts.append(cap(root, tip, 1.3, "#2a1a2e", bias=-0.5))
        for j, (dz, dx) in enumerate(((-6, -2), (-10, -5), (-12, -8))):
            p = (tip[0] + side * dx, -5.5, tip[2] + dz)
            parts.append(cap(root, p, 2.6 - j * 0.4, "#5a2448", bias=-0.6 - j * 0.01))
    # tail
    parts += [cap((sx, -3.5, 10 + bob), (sx + 1, -8.5, 6 + bob), 1.3, "#a92f27", bias=-0.2),
              cap((sx + 1, -8.5, 6 + bob), (sx + 3.5 + s, -11.5, 8.5), 1.1, "#a92f27", bias=-0.2),
              ball((sx + 3.8 + s, -12.3, 9.0), 1.6, "#2b1d1d", bias=-0.2)]
    # fanged grin
    parts.append(cap((hx - 2.0, hy + 5.6, hz - 2.4), (hx + 2.0, hy + 5.6, hz - 2.4), 0.6, "#fff4dc",
                     outline=False, shade=False, bias=0.1))
    return parts


# --- Zombie wolf (quadruped)
def wolf(phase):
    s, co = math.sin(phase), math.cos(phase)
    bob = abs(s) * 0.5
    fur, dark, bone, flesh = "#6f7a63", "#4c5544", "#e3d9bf", "#8c2f2f"
    parts = []
    # legs: diagonal pairs move together
    for (x, y, sg) in ((-3.4, 4.5, 1), (3.4, 4.5, -1), (-3.4, -4.5, -1), (3.4, -4.5, 1)):
        fy = y + sg * s * 2.6
        fz = 1.0 + max(0.0, sg * co) * 1.5
        parts.append(cap((x, y, 8 + bob), (x * 1.05, fy, fz), 1.6, fur if y > 0 else dark))
        parts.append(ball((x * 1.05, fy + 0.5, fz - 0.2), 1.5, "#3a3a32"))
    # body
    parts.append(cap((0, -5.5, 9.5 + bob), (0, 4.5, 10.5 + bob), 5.2, fur))
    # exposed ribs on both flanks
    for side in (-1, 1):
        for i, ry in enumerate((-1.5, 0.5, 2.5)):
            parts.append(cap((side * 4.9, ry, 12.4 + bob), (side * 5.2, ry - 0.3, 8.4 + bob), 0.6, bone,
                             outline=False, shade=False, bias=0.02))
        parts.append(ball((side * 4.8, -3.5, 10 + bob), 1.2, flesh, outline=False, shade=False, bias=0.02))
    # spine ridge
    for i, y in enumerate((-4, -1.5, 1, 3.5)):
        parts.append(ball((0, y, 15.2 + bob), 0.9, bone, outline=False, bias=0.03))
    # neck + head
    parts.append(cap((0, 4.5, 11 + bob), (0, 8.5, 13 + bob), 3.0, fur))
    head = (0, 9.6, 13.2 + bob)
    parts.append(ball(head, 4.2, fur))
    parts.append(cap((0, 10.5, 12.0 + bob), (0, 14.8, 11.2 + bob), 2.0, dark))
    parts.append(ball((0, 15.2, 11.6 + bob), 0.9, "#1a1414", outline=False, shade=False, bias=0.2))
    for side in (-1, 1):
        parts.append(cap((side * 1.9, 8.6, 15.6 + bob), (side * 2.6, 7.6, 19.0 + bob), 1.1, dark))
    parts += [ball((-1.7, 12.0, 14.2 + bob), 0.85, "#ff3b2f", outline=False, shade=False, bias=0.15),
              ball((1.7, 12.0, 14.2 + bob), 0.85, "#ff3b2f", outline=False, shade=False, bias=0.15)]
    # teeth
    parts.append(cap((-1.4, 14.2, 10.1 + bob), (1.4, 14.2, 10.1 + bob), 0.55, bone,
                     outline=False, shade=False, bias=0.2))
    # bony tail
    parts.append(cap((0, -9.5, 11 + bob), (0.8 + s, -14, 14 + bob), 1.2, dark, bias=-0.1))
    return parts


CHARACTERS = {
    "Player1": (lambda ph: humanoid(ph, P1, extra=p1_extra), 32, (16, 29)),
    "Player2": (lambda ph: humanoid(ph, P2, extra=p2_extra), 32, (16, 29)),
    "Zombie": (lambda ph: humanoid(ph, ZB, stride=2.2, arm_mode="forward", lean=1.0,
                                   head_tilt=(1.0, 0.8), sway=0.6, extra=zombie_extra), 32, (16, 29)),
    "ZombieWolf": (wolf, 40, (20, 32)),
    "Demon": (lambda ph: humanoid(ph, DM, stride=3.5, scale=1.3, extra=demon_extra), 48, (24, 42)),
}


def preview(sheets, out_path, scale=4):
    pad, label_h = 16, 22
    widths = [im.width * scale for im in sheets.values()]
    w = sum(widths) + pad * (len(sheets) + 1)
    h = max(im.height * scale for im in sheets.values()) + pad * 2 + label_h
    canvas = Image.new("RGBA", (w, h), (58, 92, 140, 255))
    d = ImageDraw.Draw(canvas)
    x = pad
    for name, im in sheets.items():
        big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        d.text((x, pad // 2), f"{name}  ({im.width // FRAMES}px)", fill=(255, 255, 255, 255))
        canvas.alpha_composite(big, (x, pad + label_h))
        x += big.width + pad
    canvas.save(out_path)


def anim_gif(sheets, out_path, scale=4):
    """One row per character, 8 directions side by side, looping walk."""
    cell = 48
    w = cell * 8 * scale
    h = cell * len(sheets) * scale
    frames = []
    for fi in range(FRAMES):
        canvas = Image.new("RGBA", (cell * 8, cell * len(sheets)), (58, 92, 140, 255))
        for ri, (name, im) in enumerate(sheets.items()):
            size = im.width // FRAMES
            for di in range(8):
                fr = im.crop((fi * size, di * size, (fi + 1) * size, (di + 1) * size))
                ox = di * cell + (cell - size) // 2
                oy = ri * cell + (cell - size)
                canvas.alpha_composite(fr, (ox, oy))
        frames.append(canvas.resize((w, h), Image.NEAREST).convert("P", palette=Image.ADAPTIVE))
    frames[0].save(out_path, save_all=True, append_images=frames[1:], duration=160, loop=0)


if __name__ == "__main__":
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    sheets = {}
    for name, (model, size, origin) in CHARACTERS.items():
        im = sheet(model, size, origin)
        im.save(os.path.join(out_dir, f"{name}_Walk.png"))
        sheets[name] = im
    preview(sheets, os.path.join(out_dir, "_preview_sheets.png"))
    anim_gif(sheets, os.path.join(out_dir, "_preview_walk.gif"))
    print("done")
