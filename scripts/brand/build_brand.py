#!/usr/bin/env python3
"""Reproducible Autonogrammer brand build: assets/brand/src -> every published asset.

Inputs  : assets/brand/src/*.svg (editable sources; logos and banners contain <text>),
          the bundled OFL fonts in src/Autonogrammer.Native.Views/Assets/Fonts.
Outputs : see docs/brand.md ("Asset inventory"); the list is also enforced by
          scripts/ci/check-brand.sh.
Tools   : google-chrome (shaping, screenshots, PDF), pdftocairo (PDF -> glyph
          outlines), cairosvg + Pillow (rasterise the text-free mark), optipng,
          ImageMagick convert (PNG -> ICO only).
The script is idempotent: files whose bytes did not change are not rewritten.
"""
from __future__ import annotations

import io
import json
import re
import shutil
import struct
import subprocess
import tempfile
from pathlib import Path

import cairosvg
from PIL import Image

import chrome_render

REPO = Path(__file__).resolve().parents[2]
SRC = REPO / "assets" / "brand" / "src"
BRAND = REPO / "assets" / "brand"
WEB = REPO / "apps" / "Autonogrammer.Web" / "wwwroot"
RCL = REPO / "src" / "Autonogrammer.UI" / "wwwroot" / "brand"
NATIVE = REPO / "apps" / "Autonogrammer.Native" / "Assets" / "Brand"
VIEWS = REPO / "src" / "Autonogrammer.Native.Views" / "Assets" / "Brand"
ANDROID = REPO / "apps" / "Autonogrammer.Android" / "Resources"

PURPLE = "#6d28d9"
WRITTEN: list[Path] = []


# ---------------------------------------------------------------- file helpers

def write_bytes(path: Path, data: bytes) -> None:
    """Write only when the content changed (idempotent builds keep mtimes stable)."""
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)
    WRITTEN.append(path)


def write_text(path: Path, text: str) -> None:
    write_bytes(path, text.encode("utf-8"))


def optimise_png(data: bytes) -> bytes:
    """Run optipng over PNG bytes (lossless)."""
    with tempfile.TemporaryDirectory(prefix="autonogrammer-optipng-") as tmp:
        p = Path(tmp) / "x.png"
        p.write_bytes(data)
        subprocess.run(["optipng", "-quiet", "-o3", "-strip", "all", str(p)], check=True)
        return p.read_bytes()


def write_png(path: Path, image: Image.Image) -> None:
    buf = io.BytesIO()
    image.save(buf, format="PNG")
    write_bytes(path, optimise_png(buf.getvalue()))


# ------------------------------------------------------------- mark variants

class Mark:
    """Parses src/mark.svg once and derives every geometry variant from it."""

    def __init__(self, name: str) -> None:
        text = (SRC / name).read_text(encoding="utf-8")
        self.text = text
        self.defs = re.search(r"<defs>[\s\S]*?</defs>", text).group(0)
        self.rect = re.search(r"<rect [^>]*/>", text).group(0)
        self.glyph = re.search(r"<g fill=\"none\"[\s\S]*?</g>", text).group(0)

    def svg(self, *, rx: int | None = None, tile: bool = True, scale: float = 1.0,
            pad: float = 1.0) -> str:
        """Return a square 512-unit SVG.

        rx          override corner radius (0 = full-bleed square, 256 = circle)
        tile        False draws the glyph only (Android adaptive foreground)
        scale       glyph scale around the centre
        pad         scales the whole drawing (tile included) around the centre, for
                    app-icon grids that reserve a transparent margin
        """
        rect = self.rect
        if rx is not None:
            rect = re.sub(r'rx="\d+"', f'rx="{rx}"', rect)
        parts = []
        if tile:
            parts.append(rect)
        glyph = self.glyph
        if scale != 1.0:
            glyph = f'<g transform="translate(256 256) scale({scale}) translate(-256 -256)">{glyph}</g>'
        parts.append(glyph)
        body = "".join(parts)
        if pad != 1.0:
            body = f'<g transform="translate(256 256) scale({pad}) translate(-256 -256)">{body}</g>'
        defs = self.defs if tile else ""
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">'
                f"{defs}{body}</svg>")


def raster(svg: str, size: int) -> Image.Image:
    png = cairosvg.svg2png(bytestring=svg.encode("utf-8"), output_width=size, output_height=size)
    return Image.open(io.BytesIO(png)).convert("RGBA")


def flatten_on(image: Image.Image, colour: tuple[int, int, int]) -> Image.Image:
    base = Image.new("RGBA", image.size, colour + (255,))
    base.alpha_composite(image)
    return base.convert("RGB")


# --------------------------------------------------------- outlined SVG text

def outline_text(src: Path) -> str:
    """Return the SVG with every <text> replaced by glyph outline paths.

    Chrome prints the SVG (it shapes and kerns the real TTFs), pdftocairo turns the
    PDF into an SVG whose glyphs are paths, and each glyph run is placed again at
    the exact position Chrome chose.
    """
    svg = src.read_text(encoding="utf-8")
    with tempfile.TemporaryDirectory(prefix="autonogrammer-outline-") as tmp:
        pdf = Path(tmp) / "t.pdf"
        out = Path(tmp) / "t.svg"
        chrome_render.print_pdf(src, pdf)
        subprocess.run(["pdftocairo", "-svg", str(pdf), str(out)], check=True)
        cairo = out.read_text(encoding="utf-8")

    width_src = float(re.search(r'viewBox="0 0 ([\d.]+) ', svg).group(1))
    width_pdf = float(re.search(r'<svg[^>]* width="([\d.]+)"', cairo).group(1))
    k = width_src / width_pdf

    glyphs = {m.group(1): m.group(2)
              for m in re.finditer(r'<g id="(glyph-\d+-\d+)">\s*<path d="([^"]*)"', cairo)}
    uses = re.finditer(
        r'<g fill="rgb\(([\d.]+)%, ([\d.]+)%, ([\d.]+)%\)" fill-opacity="1">\s*'
        r'<use xlink:href="#(glyph-\d+-\d+)" x="([\d.]+)" y="([\d.]+)"/>', cairo)

    def place(d: str, dx: float, dy: float) -> str:
        tokens = re.findall(r"[A-Za-z]|-?\d+\.?\d*", d)
        out_tokens: list[str] = []
        index = 0
        for token in tokens:
            if token.isalpha():
                out_tokens.append(token)
                index = 0
            else:
                value = float(token)
                value = (value + (dx if index % 2 == 0 else dy)) * k
                out_tokens.append(f"{value:.2f}".rstrip("0").rstrip("."))
                index += 1
        return " ".join(out_tokens)

    runs: list[tuple[str, list[str]]] = []
    for m in uses:
        colour = "#" + "".join(f"{round(float(m.group(i)) / 100 * 255):02x}" for i in (1, 2, 3))
        d = place(glyphs[m.group(4)], float(m.group(5)), float(m.group(6)))
        if runs and runs[-1][0] == colour:
            runs[-1][1].append(d)
        else:
            runs.append((colour, [d]))
    if not runs:
        raise RuntimeError(f"no glyph runs found while outlining {src.name}")

    paths = "".join(f'<path fill="{c}" d="{" ".join(ds)}"/>' for c, ds in runs)
    group = f'<g id="wordmark">{paths}</g>'
    texts = list(re.finditer(r"[ \t]*<text[\s\S]*?</text>\n?", svg))
    if not texts:
        raise RuntimeError(f"{src.name} has no <text> to outline")
    result = svg[:texts[0].start()] + "  " + group + "\n" + svg[texts[-1].end():]
    result = re.sub(r"[ \t]*<text[\s\S]*?</text>\n?", "", result)
    if "<text" in result or "font-family" in result:
        raise RuntimeError(f"outlining {src.name} left text behind")
    return result


# ------------------------------------------------------------------- builders

def build_svgs() -> None:
    for name in ("mark", "mark-dark"):
        write_text(BRAND / f"autonogrammer-{name}.svg", (SRC / f"{name}.svg").read_text(encoding="utf-8"))
    for name in ("logo", "logo-dark", "logo-stacked"):
        write_text(BRAND / f"autonogrammer-{name}.svg", outline_text(SRC / f"{name}.svg"))
    for name in ("autonogrammer-mark.svg", "autonogrammer-logo.svg", "autonogrammer-logo-dark.svg"):
        write_bytes(RCL / name, (BRAND / name).read_bytes())
    write_text(WEB / "favicon.svg", (SRC / "mark-small.svg").read_text(encoding="utf-8"))


def build_banners() -> None:
    for src_name, out_name in (("banner", "autonogrammer-banner"), ("banner-dark", "autonogrammer-banner-dark"),
                               ("social-preview", "autonogrammer-social-preview")):
        with tempfile.TemporaryDirectory(prefix="autonogrammer-shot-") as tmp:
            shot = Path(tmp) / "shot.png"
            chrome_render.screenshot(SRC / f"{src_name}.svg", shot)
            image = Image.open(shot).convert("RGB")
        write_png(BRAND / f"{out_name}.png", image)
    social = BRAND / "autonogrammer-social-preview.png"
    if social.stat().st_size >= 1_048_576:
        raise RuntimeError("social preview exceeds 1 MB")


def build_icons() -> None:
    full = Mark("mark.svg")
    small = Mark("mark-small.svg")

    write_png(BRAND / "autonogrammer-icon-1024.png", raster(full.svg(), 1024))

    # Web head
    write_png(WEB / "favicon.png", raster(small.svg(), 64))
    write_png(WEB / "favicon-32.png", raster(small.svg(), 32))
    write_png(WEB / "apple-touch-icon.png", raster(full.svg(rx=0), 180).convert("RGB").convert("RGBA"))
    write_png(WEB / "icon-192.png", raster(full.svg(), 192))
    write_png(WEB / "icon-512.png", raster(full.svg(), 512))
    write_png(WEB / "icon-maskable-512.png", raster(full.svg(rx=0), 512).convert("RGB"))
    ico_sizes = {16: small, 32: small, 48: full}
    write_bytes(WEB / "favicon.ico", make_ico({s: raster(m.svg(), s) for s, m in ico_sizes.items()}))
    manifest = {
        "name": "Autonogrammer",
        "short_name": "Autonogrammer",
        "description": "The open agent workspace on the omp engine.",
        "start_url": "/",
        "scope": "/",
        "display": "standalone",
        "theme_color": PURPLE,
        "background_color": "#ffffff",
        "icons": [
            {"src": "icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any"},
            {"src": "icon-512.png", "sizes": "512x512", "type": "image/png", "purpose": "any"},
            {"src": "icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable"},
            {"src": "favicon.svg", "sizes": "any", "type": "image/svg+xml", "purpose": "any"},
        ],
    }
    write_text(WEB / "site.webmanifest", json.dumps(manifest, indent=2) + "\n")

    # Desktop
    ico_desktop = {16: small, 24: small, 32: small, 48: full, 64: full, 128: full, 256: full}
    write_bytes(NATIVE / "autonogrammer.ico", make_ico({s: raster(m.svg(), s) for s, m in ico_desktop.items()}))
    write_png(NATIVE / "autonogrammer-256.png", raster(full.svg(), 256))
    write_png(NATIVE / "autonogrammer-512.png", raster(full.svg(), 512))
    write_png(VIEWS / "autonogrammer-mark-256.png", raster(full.svg(), 256))
    chunks = {}
    for code, size in (("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024)):
        buf = io.BytesIO()
        # Apple icon grid: the artwork fills 80.5 percent of the canvas.
        raster(full.svg(pad=0.805), size).save(buf, format="PNG")
        chunks[code] = optimise_png(buf.getvalue())
    write_bytes(NATIVE / "autonogrammer.icns", make_icns(chunks))

    # Android
    densities = {"mdpi": 1, "hdpi": 1.5, "xhdpi": 2, "xxhdpi": 3, "xxxhdpi": 4}
    for name, factor in densities.items():
        folder = ANDROID / f"mipmap-{name}"
        launcher = int(48 * factor)
        layer = int(108 * factor)
        source = small if launcher <= 72 else full
        write_png(folder / "ic_launcher.png", raster(source.svg(), launcher))
        write_png(folder / "ic_launcher_round.png", raster(source.svg(rx=256, scale=0.92), launcher))
        # Adaptive foreground: glyph only, ring kept inside the 66 dp circle that every mask shows.
        write_png(folder / "ic_launcher_foreground.png", raster(full.svg(tile=False, scale=0.86), layer))
    adaptive = ('<?xml version="1.0" encoding="utf-8"?>\n'
                '<adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">\n'
                '  <background android:drawable="@color/ic_launcher_background" />\n'
                '  <foreground android:drawable="@mipmap/ic_launcher_foreground" />\n'
                '</adaptive-icon>\n')
    write_text(ANDROID / "mipmap-anydpi-v26" / "ic_launcher.xml", adaptive)
    write_text(ANDROID / "mipmap-anydpi-v26" / "ic_launcher_round.xml", adaptive)
    write_text(ANDROID / "values" / "ic_launcher_background.xml",
               '<?xml version="1.0" encoding="utf-8"?>\n<resources>\n'
               f'  <color name="ic_launcher_background">{PURPLE}</color>\n</resources>\n')


def make_ico(images: dict[int, Image.Image]) -> bytes:
    """Write a multi-image ICO using ImageMagick (PNG inputs, no resampling by Pillow)."""
    with tempfile.TemporaryDirectory(prefix="autonogrammer-ico-") as tmp:
        files = []
        for size, image in sorted(images.items()):
            p = Path(tmp) / f"{size}.png"
            image.save(p, format="PNG")
            files.append(str(p))
        out = Path(tmp) / "out.ico"
        subprocess.run(["convert", *files, str(out)], check=True)
        return out.read_bytes()


def make_icns(chunks: dict[str, bytes]) -> bytes:
    """Minimal ICNS container: 'icns', total length, then type/length/PNG chunks (big endian)."""
    body = b"".join(code.encode("ascii") + struct.pack(">I", 8 + len(data)) + data
                    for code, data in chunks.items())
    return b"icns" + struct.pack(">I", 8 + len(body)) + body


def main() -> None:
    if shutil.which("google-chrome") is None:
        raise SystemExit("google-chrome is required")
    build_svgs()
    build_banners()
    build_icons()
    print(f"brand build ok: {len(WRITTEN)} files")


if __name__ == "__main__":
    main()
