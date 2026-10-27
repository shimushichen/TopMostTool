"""Rebuild six v2-style action PNGs from the approved generated source sheet.

Only ScreenshotMenuIcons is written. Brand PNGs/ICOs are managed separately
with build-icons.ps1. Rebuilding requires Pillow, without model/API access.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1] / "src" / "TopMostTool"
NAMES = ("ZoomIn", "ZoomOut", "ResetZoom", "Copy", "Save", "Close")
LABELS = ("放大", "缩小", "重置缩放", "复制", "保存", "关闭")


def clean_cell(cell, size=256):
    cell = cell.convert("RGBA")
    alpha = cell.getchannel("A")
    support = alpha.filter(ImageFilter.MedianFilter(3)).point(lambda value: 255 if value >= 32 else 0)
    bounds = support.getbbox()
    if bounds is None:
        raise ValueError("Empty sprite cell")
    bounds = (max(0, bounds[0] - 2), max(0, bounds[1] - 2),
              min(cell.width, bounds[2] + 2), min(cell.height, bounds[3] + 2))
    art = cell.crop(bounds)
    extent = round(size * 52 / 64)
    art.thumbnail((extent, extent), Image.Resampling.LANCZOS)
    result = Image.new("RGBA", (size, size))
    result.alpha_composite(art, ((size - art.width) // 2, (size - art.height) // 2))
    return result


def optical_offset(image):
    alpha = image.getchannel("A")
    mass = moment = 0
    for y in range(image.height):
        for x in range(image.width):
            weight = alpha.getpixel((x, y))
            if weight < 16:
                continue
            mass += weight
            moment += (y + 0.5) * weight
    return round((image.height / 2 - moment / mass) * 20 / image.height, 3)


def make_preview(images, destination):
    result = Image.new("RGB", (1080, 438), "#F3F6F7")
    draw = ImageDraw.Draw(result)
    title_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 24)
    label_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 16)
    small_font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc", 12)
    draw.text((24, 16), "TopMostTool · v2 风格操作图标", fill="#24323A", font=title_font)
    for index, (name, label) in enumerate(zip(NAMES, LABELS)):
        x = 20 + index * 176
        draw.rounded_rectangle((x, 64, x + 164, 245), radius=14, fill="white")
        large = images[name].resize((108, 108), Image.Resampling.LANCZOS)
        result.paste(large, (x + 28, 77), large)
        draw.text((x + 82, 205), label, fill="#24323A", font=label_font, anchor="mt")
        for row, background in enumerate(("white", "#262B30")):
            y = 263 + row * 72
            draw.rounded_rectangle((x, y, x + 164, y + 60), radius=8, fill=background)
            for column, size in enumerate((16, 20, 24, 32)):
                icon = images[name].resize((size, size), Image.Resampling.LANCZOS)
                result.paste(icon, (x + 21 + column * 40 - size // 2, y + 23 - size // 2), icon)
                draw.text((x + 21 + column * 40, y + 44), str(size), font=small_font,
                          fill="#657480" if row == 0 else "#D7E0E7", anchor="mt")
    draw.text((24, 416), "16 / 20 / 24 / 32 px 实际尺寸 · 透明 PNG", fill="#657480", font=small_font)
    destination.parent.mkdir(parents=True, exist_ok=True)
    result.save(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "Assets/Source/screenshot-menu-v2-style.png")
    parser.add_argument("--output", type=Path, default=ROOT / "Assets/ScreenshotMenuIcons")
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    source = Image.open(args.source).convert("RGBA")
    if source.getchannel("A").getextrema()[0] != 0:
        raise ValueError("Source sheet must have a transparent background")
    args.output.mkdir(parents=True, exist_ok=True)
    images = {}
    offsets = {}
    for index, name in enumerate(NAMES):
        column, row = index % 3, index // 3
        cell = source.crop((round(column * source.width / 3), round(row * source.height / 2),
                            round((column + 1) * source.width / 3), round((row + 1) * source.height / 2)))
        image = clean_cell(cell)
        image.save(args.output / (name + ".png"))
        # Each display frame comes directly from the large source cell: never
        # shrink a 64px sprite and then enlarge a 20px bitmap for Windows DPI.
        for size in (16, 20, 24, 25, 30, 32, 35, 40, 48, 50, 60, 64, 80, 96, 128):
            clean_cell(cell, size).save(args.output / f"{name}-{size}.png")
        images[name] = image
        offsets[name] = optical_offset(image)
    if args.preview:
        make_preview(images, args.preview)
    print(json.dumps({"optical_offsets_at_20px": offsets, "size": [256, 256],
                      "source": str(args.source)}, indent=2))


if __name__ == "__main__":
    main()
