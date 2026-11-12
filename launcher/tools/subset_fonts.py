"""Subset the bundled OFL fonts to Latin + Cyrillic so the launcher exe stays small.

Usage: python3 subset_fonts.py <source-dir> <output-dir>
The source dir must contain the upstream static TTFs listed in FONTS.
"""
import os
import sys

from fontTools import subset

FONTS = [
    "Inter-Regular.ttf",
    "Inter-Medium.ttf",
    "Inter-SemiBold.ttf",
    "Inter-Bold.ttf",
    "InterDisplay-Bold.ttf",
    "Montserrat-Bold.ttf",
    "Montserrat-ExtraBold.ttf",
    "Montserrat-BlackItalic.ttf",
    "GeistMono-Medium.ttf",
    "GeistMono-SemiBold.ttf",
]

UNICODES = (
    "U+0020-007E,U+00A0-00FF,U+0100-017F,U+0400-045F,U+0490-0491,"
    "U+2010-2027,U+2030-203A,U+2044,U+20AC,U+20BD,U+2116,U+2122,"
    "U+2190-2199,U+2212,U+2215,U+2219,U+221E,U+2248,U+2260,U+2264-2265,"
    "U+25B2-25BC,U+25CF,U+25CB,U+2713,U+2715,U+00D7"
)


def main(src, dst):
    os.makedirs(dst, exist_ok=True)
    for name in FONTS:
        path = os.path.join(src, name)
        out = os.path.join(dst, name)
        args = [
            path,
            "--unicodes=" + UNICODES,
            "--layout-features=*",
            "--name-IDs=*",
            "--name-legacy",
            "--name-languages=*",
            "--glyph-names",
            "--notdef-outline",
            "--output-file=" + out,
        ]
        subset.main(args)
        print(f"{name}: {os.path.getsize(path) // 1024} KB -> {os.path.getsize(out) // 1024} KB")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
