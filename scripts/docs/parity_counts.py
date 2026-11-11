#!/usr/bin/env python3
"""Generate or check the Cowork parity summary from the audited row tables."""

from __future__ import annotations

import argparse
import re
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STATUSES = ("MET", "PARTIAL", "NOT-STARTED", "DEFERRED", "OMITTED-BY-DECISION")
HEADER = "| Family | MET | PARTIAL | NOT-STARTED | DEFERRED | OMITTED-BY-DECISION | Total |"
SEPARATOR = "|---|---|---|---|---|---|---|"


def parse_rows(content: str) -> list[list[str]]:
    rows = []
    in_table = False
    for raw in content.splitlines():
        line = raw.strip()
        if not (line.startswith("|") and line.endswith("|")):
            in_table = False
            continue
        cells = [cell.strip() for cell in line[1:-1].split("|")]
        if cells[:5] == ["Row", "Feature", "Status", "Evidence", "Gap or blocker"]:
            in_table = True
            continue
        if not in_table:
            continue
        if all(re.fullmatch(r":?-+:?", cell) for cell in cells):
            continue
        if len(cells) != 5:
            raise ValueError(f"malformed parity row: {line}")
        match = re.fullmatch(r"([A-Z-]+)(?:\s+\([^()]+\))?", cells[2])
        if not match or match[1] not in STATUSES:
            raise ValueError(f"invalid status for {cells[0]}: {cells[2]}")
        cells[2] = match[1]
        rows.append(cells)
    return rows


def emit_counts_block(rows: list[list[str]]) -> str:
    counts = {family: Counter(row[2] for row in rows if row[0].startswith(family)) for family in ("C", "K")}
    lines = ["## 3. Summary", "", HEADER, SEPARATOR]
    for family, label in (("C", "Cowork core (C1-C31)"), ("K", "Knowledge/scheduled (K01-K20)")):
        values = [counts[family][status] for status in STATUSES]
        lines.append(f"| {label} | " + " | ".join(map(str, [*values, sum(values)])) + " |")
    values = [counts["C"][status] + counts["K"][status] for status in STATUSES]
    lines.append("| **All** | " + " | ".join(f"**{value}**" for value in [*values, sum(values)]) + " |")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="compare the embedded summary to generated counts")
    parser.add_argument("--file", type=Path, default=ROOT / "docs" / "parity" / "cowork-parity.md", help="audit document to read")
    args = parser.parse_args()
    try:
        content = args.file.read_text(encoding="utf-8")
        rows = parse_rows(content)
        if not rows:
            raise ValueError("no parity rows found")
        block = emit_counts_block(rows)
        if not args.check:
            print(block)
            return 0
        match = re.search(r"(?ms)^## 3\. Summary\s*\n(.*?)(?=^## |\Z)", content)
        if not match or match.group(0).strip() != block:
            print("MISMATCH: embedded summary differs from generated counts", file=sys.stderr)
            return 1
        ids = [row[0] for row in rows]
        expected = {*(f"C{i}" for i in range(1, 32)), *(f"K{i:02d}" for i in range(1, 21))}
        if len(ids) != len(expected) or set(ids) != expected:
            raise ValueError("expected exactly one of each C1-C31 and K01-K20")
        print("OK: parity counts match")
        return 0
    except (OSError, ValueError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
