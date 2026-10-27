"""Sample menu pin frames faithfully from the approved v2 source.

Preserves the original colors, shading and contour. Requires Pillow; never
adds an outline or writes brand PNGs or ICO files.
"""
from pathlib import Path

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1] / "src" / "TopMostTool"
OUTPUT = ROOT / "Assets" / "MenuPinIcons"
SIZES = range(12, 65)


def build_frame(source, bounds, size):
    span = max(bounds[2] - bounds[0], bounds[3] - bounds[1])
    inset = max(2, size * 0.10)
    side = span * size / (size - inset * 2)
    # Sample through transparent padding, retaining the complete source edges.
    padding = source.width
    padded = ImageOps.expand(source, border=padding, fill=(0, 0, 0, 0))
    center_x = (bounds[0] + bounds[2]) / 2 + padding
    center_y = (bounds[1] + bounds[3]) / 2 + padding
    # RGBA resizing uses premultiplied alpha, avoiding dark fringes. Sample the
    # original only once at the final display size instead of sharpening or
    # painting a new contour that differs from the tray and toolbar artwork.
    frame = padded.resize((size, size), Image.Resampling.LANCZOS,
                          box=(center_x - side / 2, center_y - side / 2,
                               center_x + side / 2, center_y + side / 2))
    # Discard only practically invisible filter tails; preserve edge coverage.
    frame.putalpha(frame.getchannel("A").point(
        lambda value: 0 if value <= 4 else value))
    return frame


def main():
    source = Image.open(ROOT / "Assets" / "Source" / "tray.png").convert("RGBA")
    bounds = source.getbbox()
    if bounds is None:
        raise ValueError("The approved pin source is transparent")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for size in SIZES:
        build_frame(source, bounds, size).save(OUTPUT / f"Pin-{size}.png")
    print(f"Generated {len(SIZES)} menu pin frames in {OUTPUT}")


if __name__ == "__main__":
    main()
