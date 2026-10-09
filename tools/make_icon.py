"""Generates src/LazyRead/LazyRead.ico: an open book with sound waves on the app's mint accent tile."""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
SCALE = 8


def render(size: int) -> Image.Image:
    s = size * SCALE
    small = size <= 24
    image = Image.new("RGBA", (s, s))
    # Vertical mint gradient around the app accent (#A9EACD), clipped to a rounded tile.
    gradient = Image.new("RGBA", (s, s))
    top, bottom = (191, 243, 219), (146, 222, 189)
    for y in range(s):
        t = y / (s - 1)
        gradient.paste(tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,), (0, y, s, y + 1))
    mask = Image.new("L", (s, s))
    inset = 0 if small else s * 0.03
    ImageDraw.Draw(mask).rounded_rectangle((inset, inset, s - 1 - inset, s - 1 - inset), radius=s * 0.24, fill=255)
    image.paste(gradient, (0, 0), mask)

    draw = ImageDraw.Draw(image)
    ink = (20, 44, 36, 255)  # App ink on accent (#142C24).
    u = s / 100
    # Open book: two pages meeting at a spine gap.
    spine, gap = 50 * u, (3.5 if small else 2.5) * u
    top_y, bottom_y, droop = (60 if small else 60) * u, (84 if small else 82) * u, 5 * u
    left, right = (16 if small else 19) * u, (84 if small else 81) * u
    draw.polygon([(spine - gap, top_y + droop), (left, top_y), (left, bottom_y - droop), (spine - gap, bottom_y)], fill=ink)
    draw.polygon([(spine + gap, top_y + droop), (right, top_y), (right, bottom_y - droop), (spine + gap, bottom_y)], fill=ink)
    # Sound waves rising from the book.
    width = round((8 if small else 6.5) * u)
    radii = [24] if small else [15, 29]
    centre_y = (57 if small else 57) * u
    for radius in radii:
        r = radius * u
        box = (spine - r, centre_y - r, spine + r, centre_y + r)
        draw.arc(box, 225, 315, fill=ink, width=width)
        for angle in (225, 315):  # Rounded stroke ends.
            x = spine + (r - width / 2) * math.cos(math.radians(angle))
            y = centre_y + (r - width / 2) * math.sin(math.radians(angle))
            draw.ellipse((x - width / 2, y - width / 2, x + width / 2, y + width / 2), fill=ink)
    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    output = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "src" / "LazyRead" / "LazyRead.ico"
    frames = [render(size) for size in SIZES]
    frames[-1].save(output, format="ICO", sizes=[(f.width, f.height) for f in frames], append_images=frames[:-1])
    preview = Image.new("RGBA", (sum(SIZES) + 10 * len(SIZES), 256), (32, 32, 32, 255))
    x = 0
    for frame in frames:
        preview.paste(frame, (x, 0), frame)
        x += frame.width + 10
    if len(sys.argv) > 2:
        preview.save(sys.argv[2])


if __name__ == "__main__":
    main()
