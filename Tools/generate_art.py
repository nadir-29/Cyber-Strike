"""Generates Cyber Strike's original pixel art into Assets/Art/.

Run from the project root:  py Tools/generate_art.py [preview_dir]

Outputs
  Assets/Art/Backdrop/backdrop_far.png   640x360, opaque  (teal haze, searchlights, tiered megacity)
  Assets/Art/Backdrop/backdrop_mid.png   640x360, alpha   (near towers, holo screens)
  Assets/Art/Backdrop/backdrop_near.png  640x360, alpha   (alley walls, pipes, dumpster, puddles)
  Assets/Art/Fighters/fighter_player.png 384x384 sheet of 64x64 frames
  Assets/Art/Fighters/fighter_ai.png     same layout, opponent colors
  Assets/Art/UI/hud_*.png                health bar frames (P1 / CPU) and greyscale fills
  Assets/Art/Fighters/Opponents/fighter_<id>.png, fighter_<id>_ko.png, portrait_<id>.png
                                         the CPU roster (rookie, enforcer, cyborg); same layout
  Assets/Art/UI/select_star.png, select_card.png   fighter select stars and 9-slice card frame

Backdrop is 16:9 and meant for 30 pixels per unit (12 x 21.3 units).
Fighter sheet rows, top to bottom: idle(4) walk(6) jump(2) punch(3) kick(3) hurt(2).
KO strips (fighter_<who>_ko.png, 192x64): reel(flash), flung, limp (visor off).
Block strips (fighter_<who>_block.png, 192x64): raise guard, hold guard, impact recoil.
Assets/Art/VFX/block_shield.png: cyan hex-energy shield shown in front of a fighter when a hit is blocked.
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "Assets" / "Art"

W, H = 640, 360
GROUND_Y = 255  # backdrop row that lines up with the top of the arena floor

PINK = (255, 60, 170)
CYAN = (40, 240, 255)
PURPLE = (180, 80, 255)
NEON = [PINK, CYAN, PURPLE]


# ---------------------------------------------------------------- helpers

def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def with_alpha(c, a):
    return (c[0], c[1], c[2], a)


def glow_rect(img, box, color, radius=3, strength=90):
    """Stepped pixel-art glow around a box, composited onto img."""
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0, y0, x1, y1 = box
    for r in range(radius, 0, -1):
        d.rectangle([x0 - r, y0 - r, x1 + r, y1 + r], fill=with_alpha(color, strength // (r + 1)))
    img.alpha_composite(layer)


# ---------------------------------------------------------------- backdrop

TEAL = (40, 220, 200)
WINDOW_COLORS = [(140, 240, 255), (255, 255, 255), (255, 190, 110), (255, 110, 200), (120, 255, 220)]


def overlay(img, paint):
    """Paint translucent shapes on a separate layer, then blend them onto img."""
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    paint(ImageDraw.Draw(layer))
    img.alpha_composite(layer)


def gradient_color(stops, t):
    for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
        if t <= t1:
            return lerp(c0, c1, (t - t0) / (t1 - t0))
    return stops[-1][1]


def tower(d, rng, x, top, bw, body, rim, colors, lit, step=(2, 3), win=(1, 1), strip_chance=0.4):
    """Skyscraper: crown, rim light, dense window grid and optional LED strips."""
    solid = body + (255,)
    d.rectangle([x, top, x + bw, H], fill=solid)
    cx = x + bw // 2
    kind = rng.randrange(5)
    if kind == 0:  # spire
        d.polygon([(x + bw // 4, top), (x + 3 * bw // 4, top), (cx, top - bw)], fill=solid)
        d.line([cx, top - bw - 10, cx, top - bw], fill=solid)
        d.point((cx, top - bw - 10), fill=(255, 50, 70, 255))
    elif kind == 1 and bw >= 16:  # stepped
        d.rectangle([x + 3, top - 8, x + bw - 3, top], fill=solid)
        d.rectangle([x + 7, top - 14, x + bw - 7, top - 8], fill=solid)
    elif kind == 2:  # antennas
        for ax in (x + 3, x + bw - 4):
            h = rng.randrange(8, 22)
            d.line([ax, top - h, ax, top], fill=solid)
            d.point((ax, top - h), fill=(255, 50, 70, 255))
    elif kind == 3:  # slanted roof
        d.polygon([(x, top), (x + bw, top), (x + bw, top - bw // 2)], fill=solid)
    d.line([x, top, x, H], fill=rim + (255,))
    d.line([x, top, x + bw, top], fill=rim + (255,))

    # Windows light up in office-like clusters rather than uniform noise.
    sx, sy = step
    ww, wh = win
    main = rng.choice(colors)
    blocks = {}
    for wy in range(top + 3, H, sy):
        for wx in range(x + 2, x + bw - ww, sx):
            key = ((wy - top) // (sy * 4), (wx - x) // (sx * 3))
            if key not in blocks:
                blocks[key] = rng.random() < lit
            if rng.random() < (0.75 if blocks[key] else 0.03):
                c = main if rng.random() < 0.7 else rng.choice(colors)
                c = lerp(c, body, rng.uniform(0, 0.4))
                d.rectangle([wx, wy, wx + ww - 1, wy + wh - 1], fill=c + (255,))

    if rng.random() < strip_chance:
        c = lerp(rng.choice([CYAN, PURPLE, PINK, TEAL]), body, 0.15)
        cols = (x + 1, x + bw - 1) if rng.random() < 0.5 else (x + bw // 3, x + 2 * bw // 3)
        for sxp in cols:
            d.line([sxp, top, sxp, H], fill=c + (255,))


def haze(img, y0, y1, color, max_alpha):
    """Fog that thickens toward y1."""
    def paint(d):
        for y in range(y0, y1, 2):
            a = int(max_alpha * (y - y0) / max(1, y1 - y0))
            d.rectangle([0, y, W, y + 1], fill=color + (a,))
    overlay(img, paint)


def make_far(rng):
    img = Image.new("RGBA", (W, H))
    px = img.load()
    stops = [(0.0, (2, 4, 12)), (0.35, (6, 20, 38)), (0.7, (14, 66, 80)), (1.0, (54, 156, 146))]
    for y in range(H):
        t = min(y / 250, 1.0)
        for x in range(W):
            tt = min(1.0, t + (0.02 if (x + y) % 2 else 0))  # checker dither between bands
            px[x, y] = gradient_color(stops, round(tt * 24) / 24) + (255,)

    # Magenta light pollution clouds.
    def clouds(d):
        for cx, cy, rx, ry in ((470, 70, 170, 50), (150, 110, 140, 36), (330, 30, 120, 26)):
            for k in range(4, 0, -1):
                d.ellipse([cx - rx * k // 4, cy - ry * k // 4, cx + rx * k // 4, cy + ry * k // 4],
                          fill=(230, 40, 170, 14))
    overlay(img, clouds)

    d = ImageDraw.Draw(img)
    for _ in range(40):
        d.point((rng.randrange(W), rng.randrange(90)), fill=(200, 230, 255, 255))

    # Searchlights sweeping up from the city.
    def beams(d):
        for bx, angle, color in ((150, -18, CYAN), (300, 10, PINK), (360, -6, (220, 255, 255)),
                                 (470, 22, CYAN), (230, 30, PURPLE)):
            a = math.radians(angle)
            tx = bx + math.tan(a) * 240
            d.polygon([(bx - 1, 240), (bx + 1, 240), (tx + 16, 0), (tx - 16, 0)], fill=color + (26,))
            d.polygon([(bx, 240), (tx + 4, 0), (tx - 4, 0)], fill=color + (30,))
    overlay(img, beams)

    # Three tiers of towers. Farther tiers are taller, paler and denser; teal fog
    # between tiers gives depth, and gaps in nearer tiers let the deep city show.
    tiers = [
        dict(body=(30, 96, 104), rim=(60, 140, 140), top=(20, 150), w=(8, 20), gap=(-3, 2), lit=0.25,
             colors=[(150, 235, 230), (200, 250, 245)], strip=0.2, fog=(60, 170, 160, 170)),
        dict(body=(16, 56, 72), rim=(34, 104, 120), top=(50, 180), w=(10, 24), gap=(0, 10), lit=0.3,
             colors=WINDOW_COLORS[:3], strip=0.35, fog=(40, 140, 140, 150)),
        dict(body=(6, 22, 36), rim=(24, 72, 98), top=(110, 215), w=(14, 30), gap=(6, 26), lit=0.35,
             colors=WINDOW_COLORS, strip=0.5, fog=None),
    ]
    tall = []
    for tier in tiers:
        d = ImageDraw.Draw(img)
        x = -rng.randrange(10)
        while x < W:
            bw = rng.randrange(*tier["w"])
            top = rng.randrange(*tier["top"])
            tower(d, rng, x, top, bw, tier["body"], tier["rim"], tier["colors"], tier["lit"],
                  strip_chance=tier["strip"])
            if top < 90 and bw > 14:
                tall.append((x, top, bw))
            x += bw + rng.randrange(*tier["gap"])
        if tier["fog"]:
            fog = tier["fog"]
            haze(img, 40, H, fog[:3], fog[3])
    haze(img, 200, H, (60, 190, 170), 110)  # street glow

    # Holographic rings around a few of the tallest towers.
    def rings(d):
        for i, (x, top, bw) in enumerate(tall[-3:]):
            c = (CYAN, PINK, TEAL)[i % 3]
            cx, ry = x + bw // 2, top + 24
            for k, a in ((6, 30), (3, 60)):
                d.ellipse([cx - bw // 2 - 8 - k, ry - 3 - k // 2, cx + bw // 2 + 8 + k, ry + 3 + k // 2],
                          outline=c + (a,))
            d.ellipse([cx - bw // 2 - 8, ry - 3, cx + bw // 2 + 8, ry + 3], outline=c + (220,))
    overlay(img, rings)

    # Air traffic: short streams of running lights.
    d = ImageDraw.Draw(img)
    for lane_y in (70, 104, 150, 186):
        for _ in range(3):
            x = rng.randrange(W - 80)
            end = x + rng.randrange(30, 90)
            while x < end:
                c = rng.choice([(255, 60, 60), (255, 240, 220), (255, 180, 80), (120, 240, 255)])
                d.rectangle([x, lane_y, x + 1, lane_y], fill=c + (255,))
                x += rng.randrange(4, 12)
    return img


def screen(img, box, kind, color, rng):
    """Glowing billboard: draws its graphic on a tile (for clipping), adds scanlines and glow."""
    x0, y0, x1, y1 = box
    w, h = x1 - x0 + 1, y1 - y0 + 1
    glow_rect(img, box, color, radius=5, strength=130)
    tile = Image.new("RGBA", (w, h), lerp(color, (0, 0, 0), 0.82) + (255,))
    td = ImageDraw.Draw(tile)
    hi = lerp(color, (255, 255, 255), 0.5)
    if kind == "hex":
        for row in range(-1, h // 8 + 2):
            for col in range(-1, w // 9 + 2):
                cx, cy = col * 9, row * 10 + (5 if col % 2 else 0)
                pts = [(cx + 5 * math.cos(math.radians(a)), cy + 5 * math.sin(math.radians(a))) for a in range(0, 360, 60)]
                td.polygon([ipt(p) for p in pts], outline=color + (255,))
        for _ in range(4):
            cx, cy = rng.randrange(w), rng.randrange(h)
            td.rectangle([cx, cy, cx + 3, cy + 2], fill=hi + (255,))
    elif kind == "glyphs":
        glyphs = [[rng.random() < 0.55 for _ in range(12)] for _ in range(10)]
        for gx in range(3, w - 3, 5):
            for gy in range(rng.randrange(-20, 4), h, 6):
                if rng.random() < 0.85:
                    glyph = glyphs[rng.randrange(len(glyphs))]
                    fade = max(0.0, min(1.0, gy / h))
                    c = lerp(hi if gy < 6 else color, (0, 0, 0), fade * 0.7)
                    for i, bit in enumerate(glyph):
                        if bit:
                            td.point((gx + i % 3, gy + i // 3), fill=c + (255,))
    elif kind == "target":
        cx, cy = w // 2, h // 2
        for r, c in ((min(w, h) // 2 - 2, color), (min(w, h) // 3, hi), (min(w, h) // 6, PINK)):
            td.ellipse([cx - r, cy - r, cx + r, cy + r], outline=c + (255,), width=2)
        td.line([0, cy, w, cy], fill=color + (255,))
        td.line([cx, 0, cx, h], fill=color + (255,))
    elif kind == "bars":
        for i in range(-h, w, 6):
            td.line([i, h, i + h, 0], fill=lerp(color, PURPLE, (i + h) / (w + h)) + (255,), width=3)
    elif kind == "wave":
        for k in range(3):
            prev = None
            for x in range(w):
                y = int(h / 2 + math.sin(x / 4 + k * 1.3) * (h / 2 - 3) * (0.5 + k * 0.2))
                if prev:
                    td.line([prev, (x, y)], fill=(color if k else hi) + (255,))
                prev = (x, y)
    sl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sl)
    for y in range(1, h, 2):
        sd.line([0, y, w, y], fill=(0, 0, 0, 70))
    tile.alpha_composite(sl)
    ImageDraw.Draw(tile).rectangle([0, 0, w - 1, h - 1], outline=hi + (255,))
    img.alpha_composite(tile, (x0, y0))


def make_mid(rng):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # City glow rising from the streets, seen through the gaps.
    haze(img, 150, GROUND_Y, (40, 170, 160), 120)
    haze(img, 200, GROUND_Y, (220, 60, 170), 60)

    d = ImageDraw.Draw(img)
    body = (5, 11, 22)
    # x, top, width -- laid out so the alley opening (x 96-544) frames a gap in the middle.
    layout = [(-10, 30, 72), (60, 60, 40), (100, -30, 62), (186, 110, 40), (236, 176, 30),
              (372, 24, 60), (452, 96, 40), (506, -10, 76), (590, 50, 60)]
    for i, (x, top, bw) in enumerate(layout):
        rim = lerp((CYAN, PINK)[i % 2], body, 0.45)
        tower(d, rng, x, top, bw, body, rim, WINDOW_COLORS, 0.2, step=(4, 5), win=(2, 2), strip_chance=0.6)

    # Skybridge across the central gap.
    d.rectangle([266, 190, 372, 196], fill=(8, 16, 30, 255))
    d.line([266, 190, 372, 190], fill=(60, 120, 140, 255))
    for x in range(270, 370, 5):
        d.point((x, 193), fill=(255, 220, 160, 255))
    overlay(img, lambda o: o.rectangle([266, 197, 372, 200], fill=TEAL + (60,)))

    # Big screens.
    screen(img, (106, 40, 156, 150), "glyphs", CYAN, rng)
    screen(img, (378, 60, 426, 160), "hex", PINK, rng)
    screen(img, (380, 172, 424, 206), "target", CYAN, rng)
    screen(img, (456, 104, 488, 130), "bars", PINK, rng)
    screen(img, (190, 124, 222, 146), "wave", TEAL, rng)

    # A hover car crossing the gap, with headlight beam.
    overlay(img, lambda o: o.polygon([(282, 112), (240, 104), (240, 126)], fill=(220, 255, 255, 40)))
    d = ImageDraw.Draw(img)
    d.polygon([(282, 110), (288, 106), (304, 106), (310, 110), (308, 114), (284, 114)], fill=(10, 14, 24, 255))
    d.line([284, 115, 306, 115], fill=CYAN + (255,))
    d.point((282, 111), fill=(255, 255, 230, 255))
    d.point((309, 111), fill=(255, 40, 60, 255))

    # Shadow the street level under the alley.
    overlay(img, lambda o: o.rectangle([0, GROUND_Y, W, H], fill=(4, 6, 12, 230)))
    return img


def make_near(rng):
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    # Wet asphalt floor.
    for y in range(GROUND_Y, H):
        t = (y - GROUND_Y) / (H - GROUND_Y)
        base = lerp((26, 20, 38), (12, 8, 20), t)
        for x in range(W):
            n = rng.randrange(-3, 4)
            d.point((x, y), fill=(max(0, base[0] + n), max(0, base[1] + n), max(0, base[2] + n), 255))
    for _ in range(40):
        x, y = rng.randrange(W), rng.randrange(GROUND_Y + 2, H)
        d.line([x, y, x + rng.randrange(3, 10), y], fill=(34, 28, 50, 255))

    # Puddles reflecting the neon above.
    for px, py, pw, ph, col in ((120, 300, 70, 8, PINK), (300, 315, 90, 9, CYAN),
                                 (470, 296, 60, 7, PURPLE), (200, 268, 40, 4, CYAN),
                                 (560, 322, 50, 6, PINK)):
        d.ellipse([px, py, px + pw, py + ph], fill=(18, 16, 40, 255))
        d.ellipse([px + 2, py + 1, px + pw - 2, py + ph - 1], fill=(28, 26, 64, 255))
        for k in range(3):
            sx = px + 8 + rng.randrange(max(1, pw - 30))
            sy = py + 2 + k * max(1, (ph - 3) // 3)
            d.line([sx, sy, sx + rng.randrange(6, 18), sy], fill=lerp(col, (28, 26, 64), 0.35) + (255,))
        d.point((px + pw // 2, py + 1), fill=(220, 230, 255, 255))

    # Brick facades framing both sides of the alley.
    def facade(x0, x1, inner_edge):
        d.rectangle([x0, 0, x1, GROUND_Y + 1], fill=(30, 20, 38, 255))
        for row, y in enumerate(range(0, GROUND_Y, 6)):
            d.line([x0, y, x1, y], fill=(20, 13, 26, 255))
            off = 0 if row % 2 else 7
            for bx in range(x0 + off, x1, 14):
                d.line([bx, y, bx, y + 5], fill=(20, 13, 26, 255))
                if rng.random() < 0.08:
                    d.rectangle([bx + 2, y + 1, bx + 10, y + 4], fill=(40, 26, 46, 255))
        # Neon spill on the inner edge.
        for i in range(6):
            x = inner_edge - i if inner_edge > x0 + 10 else inner_edge + i
            d.line([x, 0, x, GROUND_Y], fill=lerp((80, 40, 120), (30, 20, 38), i / 6) + (255,))

    facade(0, 96, 96)
    facade(544, W - 1, 544)

    # Pipes: a vertical drainpipe on each wall and one crossing overhead.
    def vpipe(x, y0, y1, w=6):
        d.rectangle([x, y0, x + w, y1], fill=(54, 50, 66, 255))
        d.line([x + 1, y0, x + 1, y1], fill=(96, 92, 116, 255))
        d.line([x + w, y0, x + w, y1], fill=(28, 24, 36, 255))
        for jy in range(y0 + 20, y1, 46):
            d.rectangle([x - 1, jy, x + w + 1, jy + 3], fill=(70, 66, 84, 255))

    def hpipe(x0, x1, y, h=5):
        d.rectangle([x0, y, x1, y + h], fill=(54, 50, 66, 255))
        d.line([x0, y + 1, x1, y + 1], fill=(96, 92, 116, 255))
        d.line([x0, y + h, x1, y + h], fill=(28, 24, 36, 255))
        for jx in range(x0 + 30, x1, 60):
            d.rectangle([jx, y - 1, jx + 3, y + h + 1], fill=(70, 66, 84, 255))

    vpipe(84, 20, GROUND_Y - 2)
    d.rectangle([80, GROUND_Y - 6, 96, GROUND_Y], fill=(54, 50, 66, 255))
    vpipe(552, 0, GROUND_Y - 2, w=8)
    hpipe(96, 300, 34)
    d.line([300, 34, 300, 52], fill=(54, 50, 66, 255), width=5)
    for y in (58, 66, 77):  # drip
        d.point((300, y), fill=(140, 220, 255, 255))

    # Sagging cables with little hanging lights.
    for y0, sag, col in ((18, 26, PINK), (54, 18, CYAN)):
        prev = None
        for x in range(96, 545):
            t = (x - 96) / (544 - 96)
            y = int(y0 + sag * 4 * t * (1 - t))
            if prev:
                d.line([prev, (x, y)], fill=(10, 8, 16, 255))
            prev = (x, y)
            if x % 48 == 20:
                d.line([x, y, x, y + 4], fill=(10, 8, 16, 255))
                glow_rect(img, (x - 1, y + 5, x + 1, y + 7), col, radius=2, strength=120)
                d.rectangle([x - 1, y + 5, x + 1, y + 7], fill=lerp(col, (255, 255, 255), 0.5) + (255,))

    # Dumpster against the right wall.
    dx0, dx1, dtop = 448, 530, GROUND_Y - 36
    d.rectangle([dx0, dtop, dx1, GROUND_Y - 4], fill=(28, 72, 66, 255))
    d.rectangle([dx0, dtop, dx1, dtop + 3], fill=(44, 104, 94, 255))
    d.polygon([(dx0 - 3, dtop), (dx1 + 3, dtop), (dx1 + 1, dtop - 6), (dx0 - 1, dtop - 8)], fill=(20, 54, 50, 255))
    d.line([dx0 - 1, dtop - 8, dx1 + 1, dtop - 6], fill=(50, 116, 104, 255))
    for rx in range(dx0 + 8, dx1 - 4, 12):
        d.line([rx, dtop + 6, rx, GROUND_Y - 8], fill=(22, 58, 54, 255))
    d.line([dx0, dtop + 1, dx0, GROUND_Y - 4], fill=(60, 130, 118, 255))
    d.rectangle([dx0 + 30, dtop + 10, dx0 + 46, dtop + 18], fill=(230, 60, 150, 255))  # sticker
    d.rectangle([dx0 + 33, dtop + 12, dx0 + 43, dtop + 16], fill=(255, 210, 240, 255))
    for wx in (dx0 + 4, dx1 - 10):
        d.ellipse([wx, GROUND_Y - 7, wx + 6, GROUND_Y - 1], fill=(14, 12, 18, 255))
    # Trash bags spilling beside it.
    for bx, bw, bh in ((424, 26, 16), (436, 18, 12), (532, 12, 10)):
        d.ellipse([bx, GROUND_Y - bh, bx + bw, GROUND_Y + 1], fill=(26, 22, 34, 255))
        d.arc([bx + 3, GROUND_Y - bh + 2, bx + bw - 4, GROUND_Y - 2], 200, 280, fill=(60, 54, 80, 255))

    # Cardboard boxes against the left wall.
    for bx, by, bw, bh in ((100, GROUND_Y - 22, 30, 22), (128, GROUND_Y - 14, 18, 14), (104, GROUND_Y - 36, 20, 14)):
        d.rectangle([bx, by, bx + bw, by + bh], fill=(92, 62, 44, 255))
        d.rectangle([bx, by, bx + bw, by + 2], fill=(118, 82, 58, 255))
        d.line([bx + bw // 2, by, bx + bw // 2, by + 5], fill=(64, 42, 30, 255))

    # Lamp over the right door with a light cone on the floor.
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.polygon([(560, 150), (568, 150), (600, GROUND_Y), (520, GROUND_Y)], fill=(255, 140, 220, 22))
    img.alpha_composite(layer)
    d.rectangle([558, 146, 570, 150], fill=(255, 190, 235, 255))
    return img


# ---------------------------------------------------------------- fighter

PLAYER_PALETTE = dict(
    style="rocker",
    jacket=(36, 36, 44), jacket_hi=(78, 78, 92), jacket_dark=(20, 20, 26),
    sleeve_dark=(176, 126, 98), back_glove=(196, 146, 114), tattoo=(60, 66, 80),
    cyber=(98, 106, 122), cyber_hi=(176, 186, 204), cyber_dark=(52, 56, 68),
    pants=(98, 60, 42), pants_dark=(68, 42, 30), boots=(26, 22, 24), belt=(70, 46, 34), stud=(200, 200, 210),
    skin=(216, 162, 128), skin_dark=(170, 120, 96), stubble=(146, 104, 88), hair=(22, 20, 28), hair_hi=(54, 50, 66),
    lens=(200, 40, 50), lens_hi=(255, 150, 140), frame=(150, 120, 60),
    glove=(98, 106, 122), accent=(40, 240, 255), accent_hi=(210, 255, 255),
)
AI_PALETTE = dict(
    jacket=(112, 26, 40), jacket_hi=(164, 48, 62), jacket_dark=(70, 14, 28),
    pants=(38, 24, 32), pants_dark=(26, 16, 22), boots=(24, 18, 22),
    skin=(170, 112, 82), skin_dark=(132, 84, 62), hair=(232, 232, 240),
    glove=(50, 30, 34), accent=(255, 120, 30), accent_hi=(255, 226, 160),
)
# CPU roster for the fighter select screen. Optional palette keys: style (head/body variant),
# sleeve/sleeve_dark (arm colors when they differ from the torso), tattoo, hair_hi.
OPPONENT_PALETTES = {
    # Easy: street punk rookie. Two-tone mohawk, white tank top, bare tattooed arms, lime shades.
    "rookie": dict(
        style="punk",
        jacket=(214, 210, 222), jacket_hi=(246, 244, 252), jacket_dark=(150, 144, 170),
        sleeve=(222, 168, 132), sleeve_dark=(176, 124, 100), tattoo=(40, 70, 90),
        pants=(30, 30, 40), pants_dark=(20, 20, 28), boots=(60, 24, 40),
        skin=(222, 168, 132), skin_dark=(176, 124, 100), hair=(255, 40, 170), hair_hi=(120, 255, 90),
        glove=(30, 26, 36), accent=(120, 255, 90), accent_hi=(230, 255, 210),
    ),
    # Medium: corporate security enforcer. Slate armor, shoulder pads, sealed helmet, amber visor band.
    "enforcer": dict(
        style="enforcer",
        jacket=(52, 64, 84), jacket_hi=(104, 122, 150), jacket_dark=(30, 38, 52),
        pants=(36, 42, 56), pants_dark=(24, 28, 38), boots=(18, 18, 24),
        skin=(196, 150, 120), skin_dark=(150, 110, 90), hair=(52, 64, 84),
        glove=(22, 24, 30), accent=(255, 186, 40), accent_hi=(255, 240, 190),
    ),
    # Hard: elite cyborg assassin. Bulky gunmetal armor, back unit, cabled pale skull, red eyes.
    "cyborg": dict(
        style="cyborg",
        jacket=(46, 46, 54), jacket_hi=(96, 98, 110), jacket_dark=(24, 24, 30),
        pants=(40, 40, 48), pants_dark=(22, 22, 28), boots=(16, 16, 20),
        skin=(206, 186, 186), skin_dark=(150, 126, 130), hair=(28, 28, 34),
        glove=(30, 30, 36), accent=(255, 30, 50), accent_hi=(255, 190, 190),
    ),
}
OUTLINE = (10, 6, 18, 255)
FRAME = 64

# Angles in degrees: 0 = straight down, +90 = toward facing (+x), 180 = up.
GUARD = dict(fa=40, ff=160, ba=20, bf=150)


def pose(**kw):
    p = dict(lean=5, ft=18, fs=-6, bt=-16, bs=4, grounded=True, hip_y=38, head=0, flash=False, dazed=False)
    p.update(GUARD)
    p.update(kw)
    return p


def anim_frames():
    idle = [pose(ft=18 + 5 * k, fs=-8 - 6 * k, bt=-10 + 4 * k, bs=-10 - 8 * k, ff=160 - 4 * k)
            for k in (0, 1, 2, 1)]
    walk = []
    for i in range(6):
        p = 2 * math.pi * i / 6
        s = math.sin(p)
        walk.append(pose(lean=8, ft=28 * s, fs=28 * s - 26 * max(0, -math.cos(p)),
                         bt=-28 * s, bs=-28 * s - 26 * max(0, math.cos(p)),
                         fa=40 + 10 * s, ba=20 - 10 * s))
    jump = [pose(grounded=False, hip_y=36, ft=70, fs=-20, bt=20, bs=-50, fa=130, ff=170, ba=-30, bf=40),
            pose(grounded=False, hip_y=38, ft=24, fs=10, bt=-18, bs=-8, fa=100, ff=130, ba=-100, bf=-120, lean=0)]
    punch = [pose(lean=0, fa=-20, ff=100, ba=40, bf=170),
             pose(lean=14, ft=40, fs=0, bt=-30, bs=-10, fa=88, ff=90, ba=-40, bf=40),
             pose(lean=8, ft=30, fs=-4, bt=-24, bs=-8, fa=60, ff=120)]
    kick = [pose(lean=-6, ft=85, fs=-15, bt=-6, bs=-2),
            pose(lean=-16, ft=88, fs=88, bt=-12, bs=-4, fa=20, ff=120, ba=-30, bf=20),
            pose(lean=0, ft=40, fs=-10, bt=-10, bs=-4)]
    hurt = [pose(lean=-18, head=-2, ft=8, fs=-12, bt=-22, bs=-6, fa=-40, ff=10, ba=-60, bf=-20, flash=True),
            pose(lean=-26, head=-3, ft=4, fs=-18, bt=-28, bs=-10, fa=-70, ff=-30, ba=-80, bf=-50)]
    return [("idle", idle), ("walk", walk), ("jump", jump), ("punch", punch), ("kick", kick), ("hurt", hurt)]


def ko_frames():
    """Knockout: snapped back by the blow, flung through the air, then limp with the visor dead."""
    return [
        pose(grounded=False, hip_y=36, lean=-34, head=-3, ft=26, fs=6, bt=-24, bs=-30,
             fa=-110, ff=-150, ba=-140, bf=-175, flash=True),
        pose(grounded=False, hip_y=36, lean=-48, head=-4, ft=50, fs=30, bt=10, bs=20,
             fa=-150, ff=-170, ba=-165, bf=160),
        pose(grounded=False, hip_y=38, lean=-55, head=-4, ft=35, fs=50, bt=15, bs=40,
             fa=-35, ff=-15, ba=-55, bf=-35, dazed=True),
    ]


def block_frames():
    """Block: forearms raised in front of the face on a braced, low stance; the last frame is the
    recoil when a hit is absorbed."""
    stance = dict(ft=38, fs=-24, bt=-30, bs=12, hip_y=40)
    return [
        pose(lean=0, fa=70, ff=130, ba=55, bf=120, **stance),
        pose(lean=-4, fa=105, ff=172, ba=88, bf=168, **stance),
        pose(lean=-12, head=-1, fa=112, ff=-178, ba=96, bf=176, ft=30, fs=-26, bt=-36, bs=8, hip_y=40),
    ]


def step(p, angle, length):
    a = math.radians(angle)
    return (p[0] + math.sin(a) * length, p[1] + math.cos(a) * length)


def ipt(p):
    return (int(round(p[0])), int(round(p[1])))


def segment(d, a, b, width, color):
    d.line([ipt(a), ipt(b)], fill=color, width=width)
    r = (width - 1) / 2
    for p in (a, b):
        x, y = ipt(p)
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)


def draw_fighter(pal, p):
    img = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    THIGH, SHIN, UPPER, FORE = 10, 10, 8, 7
    style = pal.get("style", "agent")
    bulk = {"enforcer": 1, "cyborg": 2}.get(style, 0)  # extra torso half-width
    limb = 1 if style == "cyborg" else 0               # extra limb thickness

    def legs_from(hip):
        out = {}
        for side, dx in (("b", -2), ("f", 2)):
            h = (hip[0] + dx, hip[1])
            knee = step(h, p[side + "t"], THIGH)
            ankle = step(knee, p[side + "s"], SHIN)
            out[side] = (h, knee, ankle)
        return out

    hip = (31.0, float(p["hip_y"]))
    if p["grounded"]:
        legs = legs_from(hip)
        lowest = max(legs["b"][2][1], legs["f"][2][1])
        hip = (hip[0], hip[1] + (60 - lowest))
    legs = legs_from(hip)
    lean = math.radians(p["lean"])
    up = (math.sin(lean), -math.cos(lean))
    neck = (hip[0] + up[0] * 15, hip[1] + up[1] * 15)
    shoulder = (hip[0] + up[0] * 12, hip[1] + up[1] * 12)
    head = (neck[0] + up[0] * 6 + p["head"], neck[1] + up[1] * 6)

    def leg(side, color, shade):
        h, knee, ankle = legs[side]
        segment(d, h, knee, 6 + limb, color)
        segment(d, knee, ankle, 5 + limb, color)
        ax, ay = ipt(ankle)
        d.rectangle([ax - 2 - limb, ay - 1 - limb, ax + 4, ay + 2], fill=pal["boots"])
        d.line([ax - 2 - limb, ay + 2, ax + 4, ay + 2], fill=shade)
        if bulk:  # knee guard with a status light
            kx, ky = ipt(knee)
            d.rectangle([kx - 1, ky - 1, kx + 1, ky + 1], fill=pal["jacket_hi"])
            d.point((kx, ky), fill=pal["accent"])

    def arm(upper_angle, fore_angle, color, glove, glow, cyber=False):
        elbow = step(shoulder, upper_angle, UPPER)
        hand = step(elbow, fore_angle, FORE)
        segment(d, shoulder, elbow, 5 + limb, color)
        segment(d, elbow, hand, 4 + limb, color)
        if cyber:  # plated cyber arm: highlight ridge, dark plate seams, neon elbow joint
            d.line([ipt(step(shoulder, upper_angle, 1)), ipt(step(shoulder, upper_angle, UPPER - 2))],
                   fill=pal["cyber_hi"])
            d.line([ipt(step(elbow, fore_angle, 1)), ipt(step(elbow, fore_angle, FORE - 2))], fill=pal["cyber_hi"])
            d.point(ipt(step(shoulder, upper_angle, UPPER * 0.5)), fill=pal["cyber_dark"])
            d.point(ipt(step(elbow, fore_angle, FORE * 0.55)), fill=pal["cyber_dark"])
            d.point(ipt(elbow), fill=pal["accent"])
        elif "tattoo" in pal:  # ink bands on the bare arm
            a, b = ipt(shoulder), ipt(elbow)
            for t in (0.35, 0.65):
                d.point((round(a[0] + (b[0] - a[0]) * t), round(a[1] + (b[1] - a[1]) * t)), fill=pal["tattoo"])
            d.point(ipt(step(elbow, fore_angle, FORE * 0.5)), fill=pal["tattoo"])
        hx, hy = ipt(hand)
        r = 2 + limb
        d.ellipse([hx - r, hy - r, hx + r, hy + r], fill=glove)
        d.point(ipt(step(hand, fore_angle, 1.5)), fill=glow)

    side = (math.cos(lean), math.sin(lean))
    def off(pt, s):
        return ipt((pt[0] + side[0] * s, pt[1] + side[1] * s))

    if style == "cyborg":  # armored back unit behind the shoulders, with launcher cells
        bx, by = off(neck, -7)
        d.rectangle([bx - 4, by - 6, bx + 3, by + 6], fill=pal["jacket_dark"])
        d.rectangle([bx - 4, by - 6, bx + 3, by - 5], fill=pal["jacket_hi"])
        for cy in (by - 3, by + 1):
            d.point((bx - 2, cy), fill=pal["accent"])
            d.point((bx + 1, cy), fill=pal["accent"])

    if style == "rocker":  # long hair falling behind the shoulders
        hx0, hy0 = ipt(head)
        d.polygon([(hx0 - 6, hy0 - 4), (hx0 + 2, hy0 - 6), (hx0 + 1, hy0 + 3), (hx0 - 1, hy0 + 7),
                   (hx0 - 4, hy0 + 8), (hx0 - 8, hy0 + 7), (hx0 - 8, hy0 + 1)], fill=pal["hair"])
        d.line([(hx0 - 6, hy0 + 1), (hx0 - 6, hy0 + 6)], fill=pal["hair_hi"])

    # Back limbs, darker.
    arm(p["ba"], p["bf"], pal.get("sleeve_dark", pal["jacket_dark"]), pal.get("back_glove", pal["glove"]),
        pal["accent"])
    leg("b", pal["pants_dark"], pal["accent"])

    # Torso: jacket wedge, belt, glowing front seam.
    hw, nw = 5 + bulk // 2, 6 + bulk
    d.polygon([off(hip, -hw), off(hip, hw), off(neck, nw), off(neck, -nw)], fill=pal["jacket"])
    d.line([off(neck, 1 - nw), off(hip, 1 - hw)], fill=pal["jacket_dark"])
    d.line([off(neck, nw - 2), off(hip, hw - 1)], fill=pal["accent"])
    d.line([off(neck, 2), off(hip, 2)], fill=pal["jacket_hi"])
    d.line([off(hip, -hw), off(hip, hw)], fill=pal["pants_dark"], width=2)
    d.point(off(hip, 1), fill=pal["accent_hi"])
    if style == "agent":
        d.polygon([off(neck, -6), off(neck, -2), ipt((neck[0] - 5 + up[0] * 3, neck[1] + up[1] * 3))],
                  fill=pal["jacket_hi"])  # popped collar
    elif style == "punk":  # tank top: an overall strap instead of a collar
        d.line([off(neck, 3), off(hip, 0)], fill=pal["pants"])
    elif style == "rocker":  # quilted armored vest: shoulder strap, quilting rows, studded leather belt
        d.line([off(neck, -nw), off(neck, nw - 1)], fill=pal["jacket_hi"])
        for t in (0.35, 0.6):
            row = (neck[0] + (hip[0] - neck[0]) * t, neck[1] + (hip[1] - neck[1]) * t)
            d.line([off(row, -nw + 2), off(row, nw - 3)], fill=pal["jacket_dark"])
        d.line([off(hip, -hw), off(hip, hw)], fill=pal["belt"], width=2)
        for k in (-3, -1, 1, 3):
            d.point(off(hip, k), fill=pal["stud"])
    else:  # chest plate and a high armored collar
        mid = ((hip[0] + neck[0]) / 2, (hip[1] + neck[1]) / 2)
        d.line([off(mid, -3), off(mid, 3)], fill=pal["jacket_hi"])
        d.point(off(mid, 0), fill=pal["accent"])
        d.polygon([off(neck, -nw + 1), off(neck, 3), ipt((neck[0] + up[0] * 4, neck[1] + up[1] * 4))],
                  fill=pal["jacket_dark"])

    leg("f", pal["pants"], pal["accent"])

    hx, hy = ipt(head)
    dim = lerp(pal["accent"], OUTLINE[:3], 0.7)  # powered-down visor / eyes
    if style == "enforcer":
        # Sealed helmet: shell, top ridge, antenna, full-width visor band, jaw guard.
        d.ellipse([hx - 6, hy - 7, hx + 6, hy + 6], fill=pal["jacket"])
        d.arc([hx - 6, hy - 7, hx + 6, hy + 6], 200, 320, fill=pal["jacket_hi"])
        d.line([hx - 5, hy + 4, hx + 4, hy + 4], fill=pal["jacket_dark"])
        d.line([hx - 5, hy - 6, hx - 5, hy - 10], fill=pal["jacket_hi"])
        d.point((hx - 5, hy - 11), fill=pal["accent"])
        d.rectangle([hx - 3, hy - 2, hx + 6, hy], fill=dim if p["dazed"] else pal["accent"])
        if not p["dazed"]:
            d.line([hx - 1, hy - 2, hx + 5, hy - 2], fill=pal["accent_hi"])
    elif style == "cyborg":
        # Pale face under a gunmetal skull cap ridged with cables, glowing red eyes, jaw grille.
        d.ellipse([hx - 5, hy - 6, hx + 5, hy + 6], fill=pal["skin"])
        d.chord([hx - 6, hy - 7, hx + 6, hy + 5], 180, 360, fill=pal["jacket"])
        for cx in (-3, 0, 3):
            d.line([hx + cx, hy - 6, hx + cx - 1, hy - 2], fill=pal["jacket_hi"])
        d.line([hx - 6, hy - 1, hx + 6, hy - 1], fill=pal["jacket_dark"])
        d.line([hx - 5, hy + 2, hx - 3, hy + 6], fill=pal["skin_dark"])
        d.rectangle([hx, hy + 3, hx + 4, hy + 5], fill=pal["jacket_dark"])
        d.line([hx + 1, hy + 4, hx + 3, hy + 4], fill=pal["jacket_hi"])
        for ex in (hx + 1, hx + 4):
            d.rectangle([ex, hy, ex + 1, hy + 1], fill=dim if p["dazed"] else pal["accent"])
        if not p["dazed"]:
            d.point((hx + 5, hy), fill=pal["accent_hi"])
            d.point((hx - 3, hy - 4), fill=pal["accent"])
    elif style == "rocker":
        # Stubbled face under a long black mane with a side part, red-lensed aviators.
        d.ellipse([hx - 5, hy - 5, hx + 5, hy + 6], fill=pal["skin"])
        d.line([hx - 5, hy + 3, hx - 3, hy + 6], fill=pal["skin_dark"])
        d.line([hx - 1, hy + 5, hx + 3, hy + 5], fill=pal["stubble"])     # jaw stubble
        d.point((hx + 4, hy + 4), fill=pal["stubble"])
        d.point((hx + 1, hy + 3), fill=pal["stubble"])                     # moustache
        d.point((hx + 3, hy + 3), fill=pal["skin_dark"])                   # mouth
        d.polygon([(hx - 6, hy + 1), (hx - 6, hy - 5), (hx - 2, hy - 8), (hx + 3, hy - 8), (hx + 6, hy - 5),
                   (hx + 6, hy - 3), (hx + 2, hy - 5), (hx - 1, hy - 4), (hx - 3, hy + 2)], fill=pal["hair"])
        d.line([hx - 2, hy - 7, hx + 3, hy - 7], fill=pal["hair_hi"])     # sheen
        lens = lerp(pal["lens"], OUTLINE[:3], 0.5) if p["dazed"] else pal["lens"]
        d.line([hx - 1, hy - 2, hx + 6, hy - 2], fill=pal["frame"])       # aviator bar
        d.rectangle([hx, hy - 1, hx + 2, hy], fill=lens)
        d.rectangle([hx + 4, hy - 1, hx + 6, hy], fill=lens)
        if not p["dazed"]:
            d.point((hx + 4, hy - 1), fill=pal["lens_hi"])
    elif style == "punk":
        # Shaved sides, tall two-tone mohawk, slim shades, earring.
        d.ellipse([hx - 5, hy - 5, hx + 5, hy + 6], fill=pal["skin"])
        d.line([hx - 5, hy + 3, hx - 3, hy + 6], fill=pal["skin_dark"])
        d.line([hx - 5, hy - 3, hx - 3, hy - 4], fill=pal["skin_dark"])
        tips = [(hx - 6, hy - 9), (hx - 2, hy - 12), (hx + 2, hy - 13), (hx + 5, hy - 10)]
        d.polygon([(hx - 5, hy - 4), tips[0], (hx - 3, hy - 6), tips[1], (hx, hy - 7), tips[2],
                   (hx + 3, hy - 7), tips[3], (hx + 5, hy - 4)], fill=pal["hair"])
        for tip in tips:
            d.point(tip, fill=pal["hair_hi"])
        d.rectangle([hx, hy - 2, hx + 6, hy - 1], fill=dim if p["dazed"] else pal["accent"])
        d.point((hx - 2, hy + 1), fill=pal["accent_hi"])
    else:
        # Head: hair, face, visor.
        d.ellipse([hx - 5, hy - 5, hx + 5, hy + 6], fill=pal["skin"])
        d.line([hx - 5, hy + 3, hx - 3, hy + 6], fill=pal["skin_dark"])
        d.polygon([(hx - 6, hy - 1), (hx - 5, hy - 6), (hx - 2, hy - 9), (hx, hy - 7), (hx + 2, hy - 10),
                   (hx + 4, hy - 7), (hx + 7, hy - 8), (hx + 6, hy - 3), (hx - 1, hy - 3), (hx - 4, hy + 2)],
                  fill=pal["hair"])
        if p["dazed"]:  # visor powered down
            d.rectangle([hx - 2, hy - 2, hx + 6, hy], fill=dim)
            d.point((hx + 2, hy - 1), fill=pal["accent"])
        else:
            d.rectangle([hx - 2, hy - 2, hx + 6, hy], fill=pal["accent"])
            d.line([hx + 1, hy - 2, hx + 5, hy - 2], fill=pal["accent_hi"])
            d.point((hx + 6, hy - 1), fill=pal["accent_hi"])

    if "cyber" in pal:  # the front arm is the cyber arm
        arm(p["fa"], p["ff"], pal["cyber"], pal["cyber_hi"], pal["accent_hi"], cyber=True)
    else:
        arm(p["fa"], p["ff"], pal.get("sleeve", pal["jacket"]), pal["glove"], pal["accent_hi"])
    if bulk:  # pauldron over the front shoulder
        sx, sy = ipt(shoulder)
        r = 2 + bulk
        d.ellipse([sx - r, sy - r, sx + r, sy + r - 1], fill=pal["jacket_hi"])
        d.line([sx - r + 1, sy + r - 1, sx + r - 1, sy + r - 1], fill=pal["jacket_dark"])
        d.point((sx + 1, sy), fill=pal["accent"])

    # 1px dark outline.
    alpha = img.getchannel("A")
    grown = alpha.filter(ImageFilter.MaxFilter(3))
    outline = Image.new("RGBA", img.size, OUTLINE)
    outline.putalpha(grown)
    outline.alpha_composite(img)
    img = outline

    if p["flash"]:
        white = Image.new("RGBA", img.size, (255, 255, 255, 0))
        white.putalpha(img.getchannel("A").point(lambda a: 140 if a else 0))
        img.alpha_composite(white)
    return img


def make_sheet(pal):
    anims = anim_frames()
    cols = max(len(f) for _, f in anims)
    sheet = Image.new("RGBA", (cols * FRAME, len(anims) * FRAME), (0, 0, 0, 0))
    for row, (_, frames) in enumerate(anims):
        for col, p in enumerate(frames):
            sheet.alpha_composite(draw_fighter(pal, p), (col * FRAME, row * FRAME))
    return sheet


def make_ko_strip(pal, frames=None):
    frames = frames or ko_frames()
    strip = Image.new("RGBA", (len(frames) * FRAME, FRAME), (0, 0, 0, 0))
    for col, p in enumerate(frames):
        strip.alpha_composite(draw_fighter(pal, p), (col * FRAME, 0))
    return strip


def make_block_strip(pal):
    return make_ko_strip(pal, block_frames())


def make_block_shield():
    """14x40 curved shield of hex cells: white-hot core edge fading to cyan, transparent gaps."""
    w, h = 14, 40
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    for y in range(h):
        v = (y - (h - 1) / 2) / ((h - 1) / 2)       # -1 .. 1 down the shield
        bulge = (1 - v * v) ** 0.5                 # arc bulging toward the attacker (+x)
        edge = 3 + bulge * 8
        for x in range(w):
            d = edge - x                           # distance inside the front edge
            if d < 0 or d > 5:
                continue
            fade = 1 - abs(v) ** 3
            if d < 1:
                c, a = (235, 255, 255), 255
            elif (x + (y // 3) % 2) % 3 == 0 or y % 3 == 0:  # hex cell lines
                c, a = (60, 240, 255), int(220 * fade)
            else:
                c, a = (40, 200, 255), int(70 * fade * (1 - d / 6))
            img.putpixel((x, y), c + (a,))
    return img


def make_portrait(pal):
    """Head-and-shoulders bust from the first idle frame on a dark neon gradient, 4x upscaled."""
    bust = draw_fighter(pal, anim_frames()[0][1][0]).crop((12, 0, 52, 40))
    bg = Image.new("RGBA", bust.size, (0, 0, 0, 255))
    d = ImageDraw.Draw(bg)
    top = lerp(pal["accent"], (8, 6, 18), 0.75)
    for y in range(bust.height):
        c = lerp(top, (8, 6, 18), y / bust.height)
        if y % 3 == 0:  # scanlines
            c = lerp(c, (0, 0, 0), 0.5)
        d.line([0, y, bust.width - 1, y], fill=c + (255,))
    bg.alpha_composite(bust)
    return bg.resize((bust.width * 4, bust.height * 4), Image.NEAREST)


def make_select_ui(out):
    """White (tintable) select-screen pieces: a 9x9 star and a 24x24 9-slice neon card frame."""
    star = Image.new("RGBA", (9, 9), (0, 0, 0, 0))
    rows = ["....#....", "...###...", "...###...", "#########", ".#######.", "..#####..", "..#####..",
            ".###.###.", ".#.....#."]
    for y, row in enumerate(rows):
        for x, c in enumerate(row):
            if c == "#":
                star.putpixel((x, y), (255, 255, 255, 255))
    star.save(out / "select_star.png")

    card = Image.new("RGBA", (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    for r, a in ((0, 60), (1, 130)):  # stepped outer glow
        d.rectangle([r, r, 23 - r, 23 - r], outline=(255, 255, 255, a))
    d.rectangle([2, 2, 21, 21], outline=(255, 255, 255, 255))
    for x, y in ((3, 3), (20, 3), (3, 20), (20, 20)):  # corner studs
        d.point((x, y), fill=(255, 255, 255, 255))
    card.save(out / "select_card.png")


def make_opponents(out):
    out.mkdir(parents=True, exist_ok=True)
    for oid, pal in OPPONENT_PALETTES.items():
        make_sheet(pal).save(out / f"fighter_{oid}.png")
        make_ko_strip(pal).save(out / f"fighter_{oid}_ko.png")
        make_block_strip(pal).save(out / f"fighter_{oid}_block.png")
        make_portrait(pal).save(out / f"portrait_{oid}.png")


# ---------------------------------------------------------------- HUD

HUD_W, HUD_H = 184, 22
FILL_POS = (32, 6)       # top-left of the fill inside the player frame
FILL_W, FILL_H = 140, 10
TAG_FONT = {
    "P": ["110", "101", "110", "100", "100"],
    "1": ["010", "110", "010", "010", "111"],
    "C": ["011", "100", "100", "100", "011"],
    "U": ["101", "101", "101", "101", "111"],
}


def para_rows(x0, y0, w, h):
    """Rows of a right-leaning parallelogram: (y, left x, right x)."""
    for y in range(y0, y0 + h):
        left = x0 + (y0 + h - 1 - y) // 2
        yield y, left, left + w - 1


def draw_tag_text(d, x, y, text, color):
    for ch in text:
        for gy, row in enumerate(TAG_FONT[ch]):
            for gx, bit in enumerate(row):
                if bit == "1":
                    d.point((x + gx, y + gy), fill=color)
        x += 4


def hud_frame(accent):
    img = Image.new("RGBA", (HUD_W, HUD_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    plate, dim = (8, 6, 18, 235), lerp(accent, (8, 6, 18), 0.6) + (255,)
    rows = list(para_rows(0, 0, 172, HUD_H))
    for y, l, r in rows:
        d.line([l, y, r, y], fill=plate)
        d.point((l, y), fill=accent + (255,))
        d.point((r, y), fill=accent + (255,))
    for y, l, r in (rows[0], rows[-1]):
        d.line([l, y, r, y], fill=accent + (255,))
    for y, l, r in para_rows(2, 2, 168, HUD_H - 4):  # inner trim
        d.point((l, y), fill=dim)
        d.point((r, y), fill=dim)
    for y, l, r in para_rows(4, 4, 24, 14):           # name tag block
        d.line([l, y, r, y], fill=accent + (255,))
    for y, l, r in para_rows(FILL_POS[0], FILL_POS[1], FILL_W, FILL_H):  # empty slot
        d.line([l, y, r, y], fill=(24, 16, 36, 255))
    for x in range(40, 168, 10):                      # ticks under the slot
        d.line([x, 18, x + 2, 18], fill=dim)
    d.line([150, 0, 168, 0], fill=lerp(accent, (255, 255, 255), 0.6) + (255,))  # bright notch
    d.line([151, 1, 160, 1], fill=lerp(accent, (255, 255, 255), 0.3) + (255,))
    return img


def hud_fill():
    """Greyscale segmented fill; tinted per fighter by the UI Image colour."""
    w = FILL_W + (FILL_H - 1) // 2
    img = Image.new("RGBA", (w, FILL_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    shades = [255, 235, 205, 205, 205, 205, 205, 170, 170, 140]
    for y, l, r in para_rows(0, 0, FILL_W, FILL_H):
        v = shades[y]
        d.line([l, y, r, y], fill=(v, v, v, 255))
        for gx in range(l + 7, r, 8):  # slanted segment gaps
            g = int(v * 0.55)
            d.point((gx, y), fill=(g, g, g, 255))
    return img


def make_hud(out):
    out.mkdir(parents=True, exist_ok=True)
    tag_y = 4 + (14 - 5) // 2

    player = hud_frame(CYAN)
    draw_tag_text(ImageDraw.Draw(player), 16, tag_y, "P1", (8, 6, 18, 255))
    player.save(out / "hud_frame_player.png")

    ai = ImageOps.mirror(hud_frame((255, 120, 30)))
    draw_tag_text(ImageDraw.Draw(ai), HUD_W - 1 - 19 - 5, tag_y, "CPU", (8, 6, 18, 255))
    ai.save(out / "hud_frame_ai.png")

    fill = hud_fill()
    fill.save(out / "hud_fill_left.png")
    ImageOps.mirror(fill).save(out / "hud_fill_right.png")


# ---------------------------------------------------------------- main

def main():
    rng = random.Random(2077)
    (ART / "Backdrop").mkdir(parents=True, exist_ok=True)
    (ART / "Fighters").mkdir(parents=True, exist_ok=True)

    far, mid, near = make_far(rng), make_mid(rng), make_near(rng)
    far.save(ART / "Backdrop" / "backdrop_far.png")
    mid.save(ART / "Backdrop" / "backdrop_mid.png")
    near.save(ART / "Backdrop" / "backdrop_near.png")

    player, ai = make_sheet(PLAYER_PALETTE), make_sheet(AI_PALETTE)
    player.save(ART / "Fighters" / "fighter_player.png")
    ai.save(ART / "Fighters" / "fighter_ai.png")
    player_ko, ai_ko = make_ko_strip(PLAYER_PALETTE), make_ko_strip(AI_PALETTE)
    player_ko.save(ART / "Fighters" / "fighter_player_ko.png")
    ai_ko.save(ART / "Fighters" / "fighter_ai_ko.png")
    make_hud(ART / "UI")
    make_block_strip(PLAYER_PALETTE).save(ART / "Fighters" / "fighter_player_block.png")
    make_block_strip(AI_PALETTE).save(ART / "Fighters" / "fighter_ai_block.png")
    make_opponents(ART / "Fighters" / "Opponents")
    (ART / "VFX").mkdir(parents=True, exist_ok=True)
    make_block_shield().save(ART / "VFX" / "block_shield.png")
    make_select_ui(ART / "UI")

    if len(sys.argv) > 1:  # optional upscaled previews for review
        out = Path(sys.argv[1])
        out.mkdir(parents=True, exist_ok=True)
        comp = far.copy()
        comp.alpha_composite(mid)
        comp.alpha_composite(near)
        comp.resize((W * 2, H * 2), Image.NEAREST).save(out / "backdrop_preview.png")
        both = Image.new("RGBA", (player.width * 2, player.height), (60, 60, 70, 255))
        both.alpha_composite(player, (0, 0))
        both.alpha_composite(ai, (player.width, 0))
        both.resize((both.width * 3, both.height * 3), Image.NEAREST).save(out / "fighters_preview.png")
        kos = Image.new("RGBA", (player_ko.width, player_ko.height * 2), (60, 60, 70, 255))
        kos.alpha_composite(player_ko, (0, 0))
        kos.alpha_composite(ai_ko, (0, FRAME))
        kos.resize((kos.width * 4, kos.height * 4), Image.NEAREST).save(out / "ko_preview.png")
        roster = Image.new("RGBA", (FRAME * 9, FRAME * 3 + 160), (60, 60, 70, 255))
        for i, pal in enumerate(OPPONENT_PALETTES.values()):
            sheet = make_sheet(pal)
            for row in range(3):  # idle 0, punch 1, kick 1 per fighter
                src = [(0, 0), (1, 3), (1, 4)][row]
                frame = sheet.crop((src[0] * FRAME, src[1] * FRAME, (src[0] + 1) * FRAME, (src[1] + 1) * FRAME))
                roster.alpha_composite(frame, (FRAME * 3 * i + FRAME * row, 0))
            roster.alpha_composite(make_ko_strip(pal), (FRAME * 3 * i, FRAME))
            roster.alpha_composite(make_portrait(pal), (FRAME * 3 * i + 16, FRAME * 2))
        roster.resize((roster.width * 3, roster.height * 3), Image.NEAREST).save(out / "roster_preview.png")
        pals = [PLAYER_PALETTE, AI_PALETTE] + list(OPPONENT_PALETTES.values())
        blocks = Image.new("RGBA", (FRAME * 3 + 40, FRAME * len(pals)), (60, 60, 70, 255))
        for i, pal in enumerate(pals):
            blocks.alpha_composite(make_block_strip(pal), (0, FRAME * i))
            blocks.alpha_composite(make_block_shield(), (FRAME * 3 + 10, FRAME * i + 12))
        blocks.resize((blocks.width * 4, blocks.height * 4), Image.NEAREST).save(out / "block_preview.png")


if __name__ == "__main__":
    main()
