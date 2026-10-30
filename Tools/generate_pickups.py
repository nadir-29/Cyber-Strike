"""Generates Cyber Strike's ability pickup art into Assets/Art/Pickups/ and Assets/Art/UI/.

Run from the project root:  py Tools/generate_pickups.py [preview_dir]

Outputs
  Assets/Art/Pickups/pickup_<ability>.png  28x28, 16x16 item with a stepped neon glow (world pickups)
  Assets/Art/UI/icon_<ability>.png         16x16 item, no glow (held-ability HUD icon)
  Assets/Art/UI/hud_ability_slot_player.png / _ai.png  20x20 slot frame for the HUD icon

Abilities: double_damage (overclock chip), healing (medical nanobot),
ability_steal (hacking chip), ice_strike (cryo crystal).
Pickups are meant for 22.4 pixels per unit, like the fighters.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_art import ART, CYAN, OUTLINE, lerp  # noqa: E402

ICON = 16
PICKUP = 28
SLOT = 20

WHITE = (255, 255, 255)
DARK = OUTLINE[:3]


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def outline(img):
    """1px dark outline around every opaque pixel, like the fighter sprites."""
    alpha = img.getchannel("A").point(lambda a: 255 if a > 0 else 0)
    grown = alpha.filter(ImageFilter.MaxFilter(3))
    ring = Image.new("RGBA", img.size, OUTLINE)
    ring.putalpha(grown)
    ring.alpha_composite(img)
    return ring


# ---------------------------------------------------------------- items (drawn in a 14x14 area, outlined to 16x16)

def overclock_chip():
    """Double Damage: overheating red CPU with a lightning bolt."""
    rim, body, die, hot = (255, 60, 90), (120, 20, 44), (54, 8, 22), (255, 220, 120)
    img = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for p in (4, 7, 10):  # pins on all four sides
        d.line([p, 1, p, 2], fill=rgba((200, 200, 220)))
        d.line([p, 13, p, 14], fill=rgba((200, 200, 220)))
        d.line([1, p, 2, p], fill=rgba((200, 200, 220)))
        d.line([13, p, 14, p], fill=rgba((200, 200, 220)))
    d.rectangle([3, 3, 12, 12], fill=rgba(body), outline=rgba(rim))
    d.rectangle([5, 5, 10, 10], fill=rgba(die))
    for x, y in [(9, 5), (8, 6), (7, 7), (8, 7), (9, 7), (8, 8), (7, 9), (6, 10)]:  # bolt
        d.point((x, y), fill=rgba(hot))
    d.point((4, 4), fill=rgba(lerp(rim, WHITE, 0.6)))
    return outline(img)


def medical_nanobot():
    """Healing: round white nanobot with a green cross, antenna and legs."""
    green, shell, shade = (60, 255, 140), (225, 255, 240), (150, 200, 185)
    img = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line([7, 1, 7, 2], fill=rgba((200, 200, 220)))       # antenna
    d.point((7, 1), fill=rgba(green))
    for a, b in [((4, 11), (2, 14)), ((7, 12), (7, 14)), ((10, 11), (12, 14))]:  # legs
        d.line([a, b], fill=rgba((170, 170, 190)))
    d.ellipse([2, 3, 12, 12], fill=rgba(shell))
    d.ellipse([2, 3, 12, 12], outline=rgba(shade))
    d.arc([2, 3, 12, 12], 200, 330, fill=rgba(WHITE))
    d.rectangle([6, 5, 8, 10], fill=rgba(green))            # cross
    d.rectangle([4, 7, 10, 8], fill=rgba(green))
    d.point((7, 6), fill=rgba(lerp(green, WHITE, 0.6)))
    return outline(img)


def hacking_chip():
    """Ability Steal: purple hacking card with gold contacts and a terminal prompt."""
    purple, dark, gold, glyph = (180, 80, 255), (70, 30, 110), (255, 200, 80), (230, 200, 255)
    img = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(1, 4), (11, 4), (14, 7), (14, 11), (1, 11)], fill=rgba(dark), outline=rgba(purple))
    d.rectangle([3, 6, 5, 9], fill=rgba(gold))               # contact pad
    d.line([4, 6, 4, 9], fill=rgba(lerp(gold, DARK, 0.4)))
    d.line([3, 7, 5, 7], fill=rgba(lerp(gold, DARK, 0.4)))
    for x, y in [(8, 6), (9, 7), (8, 8)]:                    # ">"
        d.point((x, y), fill=rgba(glyph))
    d.line([10, 9, 12, 9], fill=rgba(CYAN))                  # "_" cursor
    d.line([2, 4, 9, 4], fill=rgba(lerp(purple, WHITE, 0.5)))
    d.line([6, 2, 6, 3], fill=rgba(purple))                  # data trace off the top
    d.line([6, 2, 9, 2], fill=rgba(purple))
    d.point((10, 2), fill=rgba(CYAN))
    return outline(img)


def cryo_crystal():
    """Ice Strike: faceted ice shard with a frost sparkle."""
    ice, deep, light = (120, 220, 255), (40, 120, 200), (230, 250, 255)
    img = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(7, 1), (12, 6), (8, 14), (3, 6)], fill=rgba(ice))
    d.polygon([(7, 1), (8, 14), (12, 6)], fill=rgba(deep))   # shaded facet
    d.line([3, 6, 12, 6], fill=rgba(light))                  # girdle
    d.line([7, 2, 5, 5], fill=rgba(WHITE))                   # highlight
    for x, y in [(12, 1), (11, 2), (13, 2), (12, 3), (12, 2)]:  # sparkle
        d.point((x, y), fill=rgba(WHITE))
    return outline(img)


ITEMS = [
    ("double_damage", overclock_chip, (255, 60, 90)),
    ("healing", medical_nanobot, (60, 255, 140)),
    ("ability_steal", hacking_chip, (180, 80, 255)),
    ("ice_strike", cryo_crystal, (120, 220, 255)),
]


# ---------------------------------------------------------------- pickup glow / HUD slot

def with_glow(icon, color):
    """Centre the icon on a larger canvas with a stepped pixel glow and corner sparks."""
    img = Image.new("RGBA", (PICKUP, PICKUP), (0, 0, 0, 0))
    off = (PICKUP - ICON) // 2
    mask = Image.new("L", (PICKUP, PICKUP), 0)
    mask.paste(icon.getchannel("A").point(lambda a: 255 if a > 0 else 0), (off, off))
    for size, alpha in [(9, 28), (5, 70), (3, 140)]:  # outer to inner rings
        ring = mask.filter(ImageFilter.MaxFilter(size)).point(lambda a, al=alpha: al if a else 0)
        layer = Image.new("RGBA", img.size, rgba(color, 0))
        layer.putalpha(ring)
        img.alpha_composite(layer)
    spark = rgba(lerp(color, WHITE, 0.6))
    d = ImageDraw.Draw(img)
    for x, y in [(2, 5), (24, 3), (25, 22), (4, 24)]:
        d.point((x, y), fill=spark)
    img.alpha_composite(icon, (off, off))
    return img


def slot_frame(accent):
    img = Image.new("RGBA", (SLOT, SLOT), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, SLOT - 1, SLOT - 1], fill=(8, 6, 18, 235), outline=rgba(lerp(accent, DARK, 0.6)))
    for x0, y0, dx, dy in [(0, 0, 1, 1), (SLOT - 1, 0, -1, 1), (0, SLOT - 1, 1, -1), (SLOT - 1, SLOT - 1, -1, -1)]:
        d.line([x0, y0, x0 + 3 * dx, y0], fill=rgba(accent))   # bright corner brackets
        d.line([x0, y0, x0, y0 + 3 * dy], fill=rgba(accent))
    return img


def main():
    pickups, ui = ART / "Pickups", ART / "UI"
    pickups.mkdir(parents=True, exist_ok=True)
    ui.mkdir(parents=True, exist_ok=True)

    sheet = []
    for name, draw, color in ITEMS:
        icon = draw()
        pickup = with_glow(icon, color)
        icon.save(ui / f"icon_{name}.png")
        pickup.save(pickups / f"pickup_{name}.png")
        sheet.append(pickup)
    slot_frame(CYAN).save(ui / "hud_ability_slot_player.png")
    slot_frame((255, 120, 30)).save(ui / "hud_ability_slot_ai.png")

    if len(sys.argv) > 1:  # optional upscaled preview for review
        out = Path(sys.argv[1])
        out.mkdir(parents=True, exist_ok=True)
        prev = Image.new("RGBA", (PICKUP * len(sheet), PICKUP), (30, 22, 48, 255))
        for i, p in enumerate(sheet):
            prev.alpha_composite(p, (i * PICKUP, 0))
        prev.resize((prev.width * 8, prev.height * 8), Image.NEAREST).save(out / "pickups_preview.png")


if __name__ == "__main__":
    main()
