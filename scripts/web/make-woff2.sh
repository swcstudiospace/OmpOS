#!/usr/bin/env bash
# Converts the bundled static TTFs of the three brand families to woff2 for the web head, and copies each family's OFL.txt beside them.
# Glyphs are not edited or subsetted: the conversion is the lossless woff2 container (fontTools + brotli), so the SIL OFL 1.1 terms
# of the unmodified fonts still apply. The script is idempotent: running it again rewrites byte-identical output.
#
#   scripts/web/make-woff2.sh
#
# Source: src/Autonogrammer.Native.Views/Assets/Fonts/<Family>/*.ttf   Output: src/Autonogrammer.UI/wwwroot/fonts/<Family>/*.woff2 + OFL.txt
# Needs python3 with the venv module and network access to PyPI on the first run only (the venv is reused afterwards).
# The venv lives outside the repository; override its location with AUTONOGRAMMER_WOFF2_VENV.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
src="$root/src/Autonogrammer.Native.Views/Assets/Fonts"
dst="$root/src/Autonogrammer.UI/wwwroot/fonts"
venv="${AUTONOGRAMMER_WOFF2_VENV:-/tmp/autonogrammer-woff2-venv}"

if [[ ! -x "$venv/bin/python" ]] || ! "$venv/bin/python" -c 'import fontTools, brotli' 2>/dev/null; then
  python3 -m venv "$venv"
  "$venv/bin/pip" install --quiet fonttools brotli
fi

for family in Anybody DMSans SpaceMono; do
  [[ -d "$src/$family" ]] || { echo "error: missing font source directory $src/$family" >&2; exit 1; }
  mkdir -p "$dst/$family"
  cp -f "$src/$family/OFL.txt" "$dst/$family/OFL.txt"
  for ttf in "$src/$family"/*.ttf; do
    out="$dst/$family/$(basename "${ttf%.ttf}").woff2"
    "$venv/bin/python" - "$ttf" "$out" <<'PY'
import sys
from fontTools.ttLib import TTFont

font = TTFont(sys.argv[1])
font.flavor = "woff2"
font.save(sys.argv[2])
PY
    echo "wrote ${out#"$root"/}"
  done
done
