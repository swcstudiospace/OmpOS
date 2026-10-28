#!/usr/bin/env python3
"""Headless Chrome helpers for the Autonogrammer brand build.

Chrome is the only renderer on the build host that shapes and kerns the bundled
TTFs correctly, so it rasterises banners (screenshot) and produces the PDF that
pdftocairo turns into outlined glyph paths (print-to-pdf).
"""
from __future__ import annotations

import re
import subprocess
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
FONTS = REPO / "src" / "Autonogrammer.Native.Views" / "Assets" / "Fonts"

CHROME_FLAGS = [
    "--headless=new",
    "--no-sandbox",
    "--disable-gpu",
    "--hide-scrollbars",
    "--allow-file-access-from-files",
    "--force-device-scale-factor=1",
    "--default-background-color=00000000",
]


def font_css() -> str:
    """Return @font-face rules for the bundled OFL typefaces (absolute file URLs)."""
    faces = [
        ("Anybody", 600, "Anybody/Anybody-SemiBold.ttf"),
        ("DM Sans", 400, "DMSans/DMSans-Regular.ttf"),
        ("DM Sans", 500, "DMSans/DMSans-Medium.ttf"),
        ("DM Sans", 600, "DMSans/DMSans-SemiBold.ttf"),
        ("Space Mono", 400, "SpaceMono/SpaceMono-Regular.ttf"),
        ("Space Mono", 700, "SpaceMono/SpaceMono-Bold.ttf"),
    ]
    return "\n".join(
        f"@font-face{{font-family:'{family}';font-weight:{weight};src:url('{(FONTS / rel).as_uri()}')}}"
        for family, weight, rel in faces
    )


def _viewbox(svg: str) -> tuple[float, float]:
    m = re.search(r'viewBox="0 0 ([\d.]+) ([\d.]+)"', svg)
    if not m:
        raise ValueError("SVG source needs a viewBox starting at 0 0")
    return float(m.group(1)), float(m.group(2))


def _page(svg: str, width: float, height: float) -> str:
    return (
        "<!doctype html><meta charset=utf-8><style>"
        + font_css()
        + f"@page{{size:{width}px {height}px;margin:0}}"
        + "html,body{margin:0;padding:0}svg{display:block}</style>"
        + svg
    )


def screenshot(svg_path: Path, out_png: Path) -> None:
    """Rasterise an SVG source at its natural pixel size with Chrome."""
    svg = svg_path.read_text(encoding="utf-8")
    width, height = _viewbox(svg)
    with tempfile.TemporaryDirectory(prefix="autonogrammer-brand-") as tmp:
        page = Path(tmp) / "page.html"
        page.write_text(_page(svg, width, height), encoding="utf-8")
        subprocess.run(
            ["google-chrome", *CHROME_FLAGS,
             f"--window-size={int(width)},{int(height)}",
             f"--screenshot={out_png}", page.as_uri()],
            check=True, capture_output=True, timeout=120,
        )


def print_pdf(svg_path: Path, out_pdf: Path) -> None:
    """Print an SVG source to a one-page PDF whose page equals the viewBox."""
    svg = svg_path.read_text(encoding="utf-8")
    width, height = _viewbox(svg)
    with tempfile.TemporaryDirectory(prefix="autonogrammer-brand-") as tmp:
        page = Path(tmp) / "page.html"
        page.write_text(_page(svg, width, height), encoding="utf-8")
        subprocess.run(
            ["google-chrome", *CHROME_FLAGS, "--no-pdf-header-footer",
             f"--print-to-pdf={out_pdf}", page.as_uri()],
            check=True, capture_output=True, timeout=120,
        )
