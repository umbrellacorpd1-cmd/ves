"""Render the launcher's app icon (pink tile + white play glyph) into a multi-size .ico.

Usage: python3 make_icon.py <output.ico> [preview.png]
"""
import sys

from PIL import Image, ImageDraw, ImageFilter

MASTER = 1024
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(len(a)))


def rounded_mask(size, inset, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle((inset, inset, size - inset, size - inset), radius=radius, fill=255)
    return mask


def render(inset):
    size = MASTER
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))

    # Diagonal pink gradient, matching the Majestic accent (#E81C5A).
    top_left, bottom_right = (255, 84, 132), (184, 12, 66)
    grad = Image.new("RGBA", (size, size))
    px = grad.load()
    for y in range(size):
        for x in range(size):
            px[x, y] = lerp(top_left, bottom_right, (x + y) / (2 * (size - 1))) + (255,)

    radius = int((size - 2 * inset) * 0.27)
    mask = rounded_mask(size, inset, radius)
    img.paste(grad, (0, 0), mask)

    # Soft glossy highlight on the upper half.
    gloss = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(gloss)
    for i in range(size // 2):
        alpha = int(46 * (1 - i / (size / 2)))
        gd.line([(0, inset + i), (size, inset + i)], fill=(255, 255, 255, alpha))
    gloss.putalpha(Image.composite(gloss.getchannel("A"), Image.new("L", (size, size), 0), mask))
    img = Image.alpha_composite(img, gloss)

    # Hairline inner border like the site's 1px rgba(255,255,255,.15) rings.
    ring = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(ring).rounded_rectangle(
        (inset + 6, inset + 6, size - inset - 6, size - inset - 6), radius=radius - 6,
        outline=(255, 255, 255, 60), width=10)
    img = Image.alpha_composite(img, ring)

    # Play triangle with rounded corners, nudged right for optical centring.
    cx, cy = size / 2 + size * 0.035, size / 2
    h = (size - 2 * inset) * 0.40
    pts = [(cx - h * 0.42, cy - h * 0.5), (cx - h * 0.42, cy + h * 0.5), (cx + h * 0.5, cy)]
    tri = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    td = ImageDraw.Draw(tri)
    td.polygon(pts, fill=(255, 255, 255, 255))
    stroke = int(size * 0.055)
    td.line(pts + [pts[0]], fill=(255, 255, 255, 255), width=stroke, joint="curve")
    for x, y in pts:
        r = stroke / 2
        td.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, 255))

    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shadow.paste((90, 0, 30, 110), (0, int(size * 0.02)), tri)
    shadow = shadow.filter(ImageFilter.GaussianBlur(size * 0.025))
    img = Image.alpha_composite(img, shadow)
    img = Image.alpha_composite(img, tri)
    return img


def main(out_ico, preview=None):
    framed = render(inset=int(MASTER * 0.06))
    tight = render(inset=int(MASTER * 0.01))
    frames = []
    for s in SIZES:
        src = tight if s <= 24 else framed
        frames.append(src.resize((s, s), Image.LANCZOS))
    frames[-1].save(out_ico, format="ICO", sizes=[(s, s) for s in SIZES], append_images=frames[:-1])
    if preview:
        framed.resize((256, 256), Image.LANCZOS).save(preview)
    print("wrote", out_ico)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
