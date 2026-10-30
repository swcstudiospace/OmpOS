#!/usr/bin/env bash
# Verify the Autonogrammer brand asset set: every file exists, PNG dimensions are exact,
# the social preview is under 1 MB, published SVGs are outlined (no <text>) and
# favicon.ico holds 16, 32 and 48 pixel images. Needs only python3 (PNG sizes are read from the file header).
# Usage: scripts/ci/check-brand.sh   (run from anywhere; exits non-zero on any failure)
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$root"

command -v python3 >/dev/null 2>&1 || { echo "check-brand: missing tool: python3" >&2; exit 2; }

exec python3 - <<'PY'
import re
import struct
import sys
from pathlib import Path

failures: list[str] = []


def fail(message: str) -> None:
    failures.append(message)


def png_size(path: Path) -> tuple[int, int]:
    header = path.read_bytes()[:24]
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        raise ValueError(f"{path} is not a PNG file")
    width, height = struct.unpack(">II", header[16:24])
    return width, height


# (path, exact size) for every PNG in the brand set.
png_expectations: dict[str, tuple[int, int]] = {
    "assets/brand/autonogrammer-banner.png": (1280, 320),
    "assets/brand/autonogrammer-banner-dark.png": (1280, 320),
    "assets/brand/autonogrammer-social-preview.png": (1280, 640),
    "assets/brand/autonogrammer-icon-1024.png": (1024, 1024),
    "apps/Autonogrammer.Web/wwwroot/favicon.png": (64, 64),
    "apps/Autonogrammer.Web/wwwroot/favicon-32.png": (32, 32),
    "apps/Autonogrammer.Web/wwwroot/apple-touch-icon.png": (180, 180),
    "apps/Autonogrammer.Web/wwwroot/icon-192.png": (192, 192),
    "apps/Autonogrammer.Web/wwwroot/icon-512.png": (512, 512),
    "apps/Autonogrammer.Web/wwwroot/icon-maskable-512.png": (512, 512),
    "apps/Autonogrammer.Native/Assets/Brand/autonogrammer-256.png": (256, 256),
    "apps/Autonogrammer.Native/Assets/Brand/autonogrammer-512.png": (512, 512),
    "src/Autonogrammer.Native.Views/Assets/Brand/autonogrammer-mark-256.png": (256, 256),
}
launcher = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
foreground = {"mdpi": 108, "hdpi": 162, "xhdpi": 216, "xxhdpi": 324, "xxxhdpi": 432}
for density, size in launcher.items():
    for name in ("ic_launcher.png", "ic_launcher_round.png"):
        png_expectations[f"apps/Autonogrammer.Android/Resources/mipmap-{density}/{name}"] = (size, size)
    png_expectations[f"apps/Autonogrammer.Android/Resources/mipmap-{density}/ic_launcher_foreground.png"] = (
        foreground[density], foreground[density])

published_svgs = [
    "assets/brand/autonogrammer-mark.svg",
    "assets/brand/autonogrammer-mark-dark.svg",
    "assets/brand/autonogrammer-logo.svg",
    "assets/brand/autonogrammer-logo-dark.svg",
    "assets/brand/autonogrammer-logo-stacked.svg",
    "apps/Autonogrammer.Web/wwwroot/favicon.svg",
    "src/Autonogrammer.UI/wwwroot/brand/autonogrammer-mark.svg",
    "src/Autonogrammer.UI/wwwroot/brand/autonogrammer-logo.svg",
    "src/Autonogrammer.UI/wwwroot/brand/autonogrammer-logo-dark.svg",
]
other_files = [
    "apps/Autonogrammer.Web/wwwroot/favicon.ico",
    "apps/Autonogrammer.Web/wwwroot/site.webmanifest",
    "apps/Autonogrammer.Native/Assets/Brand/autonogrammer.ico",
    "apps/Autonogrammer.Native/Assets/Brand/autonogrammer.icns",
    "apps/Autonogrammer.Android/Resources/mipmap-anydpi-v26/ic_launcher.xml",
    "apps/Autonogrammer.Android/Resources/mipmap-anydpi-v26/ic_launcher_round.xml",
    "apps/Autonogrammer.Android/Resources/values/ic_launcher_background.xml",
    "scripts/brand/build-brand.sh",
    "docs/brand.md",
]
sources = ["mark", "mark-dark", "mark-small", "logo", "logo-dark", "logo-stacked",
           "banner", "banner-dark", "social-preview"]
other_files += [f"assets/brand/src/{name}.svg" for name in sources]

for rel in [*png_expectations, *published_svgs, *other_files]:
    if not Path(rel).is_file():
        fail(f"missing file: {rel}")

for rel, expected in png_expectations.items():
    path = Path(rel)
    if path.is_file():
        actual = png_size(path)
        if actual != expected:
            fail(f"{rel}: expected {expected[0]}x{expected[1]}, found {actual[0]}x{actual[1]}")
        if path.read_bytes()[:8] != b"\x89PNG\r\n\x1a\n":
            fail(f"{rel}: not a PNG file")

social = Path("assets/brand/autonogrammer-social-preview.png")
if social.is_file() and social.stat().st_size >= 1_048_576:
    fail(f"{social}: {social.stat().st_size} bytes, must be < 1048576")

for rel in published_svgs:
    path = Path(rel)
    if path.is_file():
        text = path.read_text(encoding="utf-8")
        if re.search(r"<text[\s>]", text) or "font-family" in text:
            fail(f"{rel}: published SVG must be outlined (no <text>, no font-family)")
        if "<svg" not in text:
            fail(f"{rel}: not an SVG")

# Both ICO files must carry the sizes they promise.
def ico_sizes(rel: str) -> set[int]:
    data = Path(rel).read_bytes()
    if data[:4] != b"\x00\x00\x01\x00":
        fail(f"{rel}: not an ICO file")
        return set()
    count = int.from_bytes(data[4:6], "little")
    sizes = set()
    for i in range(count):
        entry = data[6 + 16 * i: 22 + 16 * i]
        sizes.add(entry[0] or 256)
    return sizes

if Path("apps/Autonogrammer.Web/wwwroot/favicon.ico").is_file():
    found = ico_sizes("apps/Autonogrammer.Web/wwwroot/favicon.ico")
    if not {16, 32, 48} <= found:
        fail(f"favicon.ico must hold 16/32/48, found {sorted(found)}")
if Path("apps/Autonogrammer.Native/Assets/Brand/autonogrammer.ico").is_file():
    found = ico_sizes("apps/Autonogrammer.Native/Assets/Brand/autonogrammer.ico")
    if not {16, 32, 48, 64, 128, 256} <= found:
        fail(f"autonogrammer.ico must hold 16..256, found {sorted(found)}")

# ICNS: header, total length and the four PNG chunk types.
icns = Path("apps/Autonogrammer.Native/Assets/Brand/autonogrammer.icns")
if icns.is_file():
    data = icns.read_bytes()
    if data[:4] != b"icns" or int.from_bytes(data[4:8], "big") != len(data):
        fail("autonogrammer.icns: bad header or length")
    else:
        types, offset = [], 8
        while offset < len(data):
            types.append(data[offset:offset + 4].decode("ascii"))
            offset += int.from_bytes(data[offset + 4:offset + 8], "big")
        if types != ["ic07", "ic08", "ic09", "ic10"]:
            fail(f"autonogrammer.icns: unexpected chunks {types}")

manifest = Path("apps/Autonogrammer.Web/wwwroot/site.webmanifest")
if manifest.is_file():
    import json
    data = json.loads(manifest.read_text(encoding="utf-8"))
    if data.get("name") != "Autonogrammer" or data.get("short_name") != "Autonogrammer":
        fail("site.webmanifest: name/short_name must be Autonogrammer")
    if data.get("theme_color") != "#6d28d9" or data.get("background_color") != "#ffffff":
        fail("site.webmanifest: theme_color/background_color wrong")
    if not any("maskable" in icon.get("purpose", "") for icon in data.get("icons", [])):
        fail("site.webmanifest: no maskable icon")

if failures:
    print("check-brand: FAILED", file=sys.stderr)
    for message in failures:
        print(f"  - {message}", file=sys.stderr)
    sys.exit(1)
print(f"check-brand: ok ({len(png_expectations)} PNGs, {len(published_svgs)} SVGs, {len(other_files)} other files)")
PY
