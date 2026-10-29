#!/usr/bin/env bash
# Skip-marker gate. Every skip marker in tests/ must have a matching "Is the skip marker `<kind>` in `<file>` still needed?"
# SUP-nnn row in docs/open-questions.md, and every such row must still match a marker. Kinds:
#   Assert.Skip, Assert.SkipUnless, Assert.SkipWhen, Fact(Skip=...) (Fact/Theory attribute with Skip/SkipUnless/SkipWhen).
# Rows for the Category=Live trait are not skips and are ignored here.
# Matching is by (file, kind); occurrence counts are not compared. Markers inside comments or string literals do not count.
# Usage: bash scripts/ci/check-skip-markers.sh [repo-root]   Exit 1 when any marker or row is unmatched.
set -euo pipefail
root="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$root"
exec python3 - "$root" <<'PY'
import re
import sys
from pathlib import Path

root = Path(sys.argv[1])
rows_doc = root / "docs" / "open-questions.md"
rows = set()
for m in re.finditer(r"\|\s*(SUP-\d+)\s*\|\s*Is the skip marker `([^`]+)` in `([^`]+)`", rows_doc.read_text(encoding="utf-8")):
    rows.add((m.group(3), m.group(2), m.group(1)))
row_keys = {(f, k) for f, k, _ in ((r[0], r[1], r[2]) for r in rows)}

patterns = [
    ("Assert.SkipUnless", re.compile(r"\bAssert\.SkipUnless\b")),
    ("Assert.SkipWhen", re.compile(r"\bAssert\.SkipWhen\b")),
    ("Assert.Skip", re.compile(r"\bAssert\.Skip\b")),
    ("Fact(Skip=...)", re.compile(r"\[(?:Fact|Theory)\([^\]]*\bSkip(?:Unless|When)?\s*=")),
]


def in_string_or_comment(line: str, pos: int) -> bool:
    """True when pos lies in a // comment or inside a string literal of the line (quote parity; good enough for test sources)."""
    prefix = line[:pos]
    return prefix.count('"') % 2 == 1 or "//" in prefix


markers: dict[tuple[str, str], list[int]] = {}
for path in sorted((root / "tests").rglob("*.cs")):
    rel = path.relative_to(root).as_posix()
    if "/obj/" in rel or "/bin/" in rel:
        continue
    for number, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        stripped = line.lstrip()
        if stripped.startswith(("//", "*", "///")):
            continue
        for kind, rx in patterns:
            m = rx.search(line)
            if m and not in_string_or_comment(line, m.start()):
                markers.setdefault((rel, kind), []).append(number)
                if kind in ("Assert.SkipUnless", "Assert.SkipWhen"):
                    break

failed = False
for (rel, kind), lines in sorted(markers.items()):
    if (rel, kind) not in row_keys:
        print(f"skip marker without a SUP row: {kind} in {rel} (line {lines[0]})", file=sys.stderr)
        failed = True
for rel, kind, sup in sorted(rows):
    if kind == "Category=Live trait":
        continue
    if (rel, kind) not in markers:
        print(f"{sup} names no live marker: {kind} in {rel}", file=sys.stderr)
        failed = True
if failed:
    print("check-skip-markers: unmatched markers or rows (see above); add or remove the SUP row in docs/open-questions.md", file=sys.stderr)
    sys.exit(1)
print(f"check-skip-markers: ok ({len(markers)} marker kinds across {len({r for r, _ in markers})} files)")
PY
