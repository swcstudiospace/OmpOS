#!/usr/bin/env bash
# Rebuild every OmpOS brand asset from assets/brand/src.
#
# Prerequisites on PATH: google-chrome (headless), pdftocairo (poppler-utils),
# optipng, convert (ImageMagick 6, used only for PNG -> ICO), python3 with the
# modules cairosvg and Pillow. No font tooling is needed: Chrome shapes the TTFs in
# src/OmpOS.Native.Views/Assets/Fonts and pdftocairo outlines the glyphs.
# If python3 lacks the modules, point OMPOS_BRAND_PYTHON at a venv interpreter:
#   python3 -m venv /tmp/ompos-brand-venv && /tmp/ompos-brand-venv/bin/pip install cairosvg pillow
#   OMPOS_BRAND_PYTHON=/tmp/ompos-brand-venv/bin/python scripts/brand/build-brand.sh
# The build is idempotent: unchanged files are not rewritten.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
python="${OMPOS_BRAND_PYTHON:-python3}"

missing=0
for tool in google-chrome pdftocairo optipng convert "$python"; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "build-brand: missing prerequisite: $tool" >&2
    missing=1
  fi
done
if [ "$missing" -ne 0 ]; then
  exit 2
fi
if ! "$python" -c 'import cairosvg, PIL' >/dev/null 2>&1; then
  echo "build-brand: python module cairosvg and/or Pillow missing for $python (see header for a venv recipe)" >&2
  exit 2
fi

export PYTHONDONTWRITEBYTECODE=1
exec "$python" "$here/build_brand.py"
