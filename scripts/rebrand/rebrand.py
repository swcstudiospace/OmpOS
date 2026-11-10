#!/usr/bin/env python3
"""One-shot OmpOS -> Autonogrammer rename tool (Python 3.12, standard library only).

Subcommands
  dry-run (default)  Plan everything, write nothing to --root. Prints the report and
                     writes rebrand-report.md / rebrand-report.json to --report-dir.
  apply              Refuse on collisions or a dirty tree, then git-mv paths deepest-first
                     and rewrite contents. Stages renames only through `git mv`; never commits.
  verify             Re-plan on the current tree; fails (exit 1) when anything would still
                     be renamed. With --baseline <rebrand-report.json> it also proves that
                     rejected near-matches (compose, composer, ...) and kept engine tokens
                     kept their counts.

Flags
  --root DIR              repository root (default: current directory)
  --config FILE           token/exclusion data (default: rebrand.config.json next to this file)
  --urls                  also rewrite URLs and hostnames that depend on an external rename
                          (GitHub repo slug, Vercel/Railway names, ompos.dev). Default: protected
                          and listed as owner actions.
  --engine-adapters MODE  keep (default) or rename. keep: first-party Omp* identifiers that name
                          the external omp engine integration stay. rename: they become
                          Autonogrammer* (bare omp, OMP_*, omp://, ~/.omp, models.yml, ... still
                          stay). The five product abbreviations are renamed in both modes.
  --report-dir DIR        default /tmp/ag-recon/rebrand-report
  --allow-dirty           apply on a tree with uncommitted changes (not recommended)
  --baseline FILE         verify: a rebrand-report.json written by dry-run BEFORE apply

The tool is stateless and idempotent: it only ever acts on what still matches, so applying it
to an already renamed tree changes nothing and applying it to a newer base renames only the
new content.

Keep markers: in any text file, every line from one containing `rebrand:keep-start` to the next
containing `rebrand:keep-end` (both lines included, any comment syntax) is never rewritten and never
counted as rewritable. Unbalanced, nested or one-line markers are an error naming file and line.
dry-run reports each file with kept blocks. Files whose content is excluded by the config are not
scanned for markers.
"""

from __future__ import annotations

import argparse
import bisect
import collections
import datetime as _dt
import json
import os
import re
import stat
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
DEFAULT_CONFIG = HERE / "rebrand.config.json"
DEFAULT_REPORT_DIR = "/tmp/ag-recon/rebrand-report"
COMMANDS = ("dry-run", "apply", "verify")
EXAMPLES_PER_ITEM = 2


class RebrandError(Exception):
    """A refusal the user must act on (collision, dirty tree, bad config)."""


# --------------------------------------------------------------------------- config


def glob_to_regex(pattern: str) -> re.Pattern[str]:
    """Root-relative glob: * inside a segment, ** across segments, ? one char, [a-z] one char of a set."""
    out: list[str] = []
    i = 0
    while i < len(pattern):
        c = pattern[i]
        if c == "*":
            if pattern[i : i + 2] == "**":
                j = i + 2
                if pattern[j : j + 1] == "/":
                    out.append("(?:.*/)?")
                    i = j + 1
                    continue
                out.append(".*")
                i = j
                continue
            out.append("[^/]*")
        elif c == "?":
            out.append("[^/]")
        elif c == "[" and (j := pattern.find("]", i + 2)) > 0 and "/" not in pattern[i:j]:
            out.append("[" + pattern[i + 1 : j].replace("\\", "\\\\") + "]")
            i = j
        else:
            out.append(re.escape(c))
        i += 1
    return re.compile("^" + "".join(out) + "$")


class Config:
    def __init__(self, data: dict):
        self.data = data
        self.token_map: dict[str, str] = data["tokenMap"]
        self.fallback: dict[str, str] = data["fallbackCase"]
        self.brand_re = re.compile(data["brandRegex"])
        eng = data["engineAdapters"]
        self.keep = [
            (
                p["name"],
                re.compile(p["pattern"]) if p.get("pattern") else None,
                re.compile(p["leftNot"]) if p.get("leftNot") else None,
                p.get("censusKey"),
            )
            for p in eng["keepPatterns"]
        ]
        self.abbr: dict[str, str] = eng["productAbbreviations"]
        self.abbr_re = (
            re.compile(
                r"(?:" + "|".join(re.escape(k) for k in sorted(self.abbr, key=len, reverse=True)) + r")(?![A-Za-z0-9_])"
            )
            if self.abbr
            else None
        )
        self.engine_rules = [
            (r["name"], re.compile(r["pattern"]), r["replacement"]) for r in eng["engineAdapterRules"]
        ]
        self.url_rules = [
            (
                r["name"],
                re.compile(r["pattern"]),
                re.compile(r["lineMustMatch"]) if r.get("lineMustMatch") else None,
                r.get("owner", ""),
            )
            for r in data["protectedUrlPatterns"]
        ]
        self.excl = [
            (glob_to_regex(r["glob"]), r["class"], bool(r.get("content")), bool(r.get("path")))
            for r in data["excludedGlobs"]
        ]
        self.binary_ext = {e.lower() for e in data["binaryExtensions"]}
        self.partial = data.get("partialFiles", {})

    @classmethod
    def load(cls, path: Path) -> "Config":
        try:
            return cls(json.loads(path.read_text(encoding="utf-8")))
        except (OSError, ValueError, KeyError, re.error) as exc:
            raise RebrandError(f"cannot load config {path}: {exc}") from exc

    def policy(self, rel: str) -> tuple[str | None, str | None]:
        """(content-exclusion class, path-exclusion class) for a root-relative path."""
        content_cls = path_cls = None
        for rx, cls, content, path in self.excl:
            if rx.match(rel):
                if content and content_cls is None:
                    content_cls = cls
                if path and path_cls is None:
                    path_cls = cls
        return content_cls, path_cls

    def brand_replacement(self, token: str) -> tuple[str, bool]:
        mapped = self.token_map.get(token)
        if mapped is not None:
            return mapped, True
        if token.isupper():
            return self.fallback["upper"], False
        if token[0].isupper():
            return self.fallback["pascal"], False
        return self.fallback["lower"], False

    def frozen_ranges(self, rel: str, text: str) -> tuple[str | None, list[tuple[int, int]]]:
        spec = self.partial.get(rel)
        if not spec:
            return None, []
        m = re.compile(spec["frozenFromRegex"], re.M).search(text)
        if not m:
            return spec["class"], []
        ranges = [(m.start(), len(text))]
        if spec.get("processLinesRegex"):
            carve = [
                (x.start(), x.end())
                for x in re.compile(spec["processLinesRegex"], re.M).finditer(text, m.start())
            ]
            ranges = []
            pos = m.start()
            for s, e in carve:
                if s > pos:
                    ranges.append((pos, s))
                pos = e
            if pos < len(text):
                ranges.append((pos, len(text)))
        return spec["class"], ranges


# --------------------------------------------------------------------------- options


class Options:
    def __init__(self, urls: bool = False, engine_adapters: str = "keep"):
        if engine_adapters not in ("keep", "rename"):
            raise RebrandError("--engine-adapters must be keep or rename")
        self.urls = urls
        self.engine_adapters = engine_adapters


# --------------------------------------------------------------------------- candidates


_OMPOS_RE = re.compile(r"ompos", re.I)
_IDENT_CHAR = re.compile(r"[A-Za-z0-9_]")


def candidates(text: str, cfg: Config, opts: Options):
    """Non-overlapping rename candidates: (start, end, old, new, source)."""
    raw = []
    # The literal prefilter keeps this fast; the brand regex then decides with its look-behinds.
    for p in _OMPOS_RE.finditer(text):
        m = cfg.brand_re.match(text, p.start())
        if m:
            raw.append((m.start(), m.end(), m.group(), cfg.brand_replacement(m.group())[0], "brand", 0))
    if cfg.abbr_re is not None and "Omp" in text:
        for m in cfg.abbr_re.finditer(text):
            if m.start() and _IDENT_CHAR.match(text[m.start() - 1]):
                continue
            raw.append((m.start(), m.end(), m.group(), cfg.abbr[m.group()], "abbreviation", 1))
    if opts.engine_adapters == "rename" and ("Omp" in text or "omp" in text):
        for name, rx, rep in cfg.engine_rules:
            for m in rx.finditer(text):
                raw.append((m.start(), m.end(), m.group(), rep, "engine-adapter", 2))
    raw.sort(key=lambda c: (c[0], c[5], -(c[1] - c[0])))
    out = []
    last_end = -1
    for s, e, old, new, src, _prio in raw:
        if s < last_end:
            continue
        out.append((s, e, old, new, src))
        last_end = e
    return out


def merged(spans: list[tuple[int, int, str]]):
    """Sort and merge overlapping (start, end, name) spans; the first name wins."""
    spans.sort()
    out: list[list] = []
    for s, e, n in spans:
        if out and s <= out[-1][1]:
            out[-1][1] = max(out[-1][1], e)
        else:
            out.append([s, e, n])
    return out


def overlap(spans, starts, s: int, e: int):
    """Return the merged span overlapping [s, e), or None."""
    i = bisect.bisect_right(starts, s) - 1
    for j in (i, i + 1):
        if 0 <= j < len(spans) and spans[j][0] < e and s < spans[j][1]:
            return spans[j]
    return None


def apply_edits(text: str, edits) -> str:
    out = []
    pos = 0
    for s, e, _old, new, _src in edits:
        out.append(text[pos:s])
        out.append(new)
        pos = e
    out.append(text[pos:])
    return "".join(out)


def rename_segment(seg: str, cfg: Config, opts: Options) -> str:
    return apply_edits(seg, candidates(seg, cfg, opts))


# --------------------------------------------------------------------------- classification helpers

_ALNUM = re.compile(r"[A-Za-z0-9]")


def word_at(text: str, s: int, e: int) -> str:
    l, r = s, e
    while l > 0 and text[l - 1].isascii() and text[l - 1].isalnum():
        l -= 1
    while r < len(text) and text[r].isascii() and text[r].isalnum():
        r += 1
    return text[l:r]


def ident_at(text: str, s: int, e: int) -> str:
    l, r = s, e
    while l > 0 and text[l - 1].isascii() and (text[l - 1].isalnum() or text[l - 1] == "_"):
        l -= 1
    while r < len(text) and text[r].isascii() and (text[r].isalnum() or text[r] == "_"):
        r += 1
    return text[l:r]


def brand_kind(text: str, s: int, e: int, tok: str) -> str:
    before = text[max(0, s - 3) : s]
    after = text[e : e + 3]
    if tok == "OMPOS":
        return "env-var / constant (OMPOS)"
    if before[-2:].lower() == "x-":
        return "http header (X-Ompos-*)"
    if after.startswith("://"):
        return "uri scheme (ompos://)"
    if before[-1:] == "." and tok[0].islower():
        return "dot directory or lowercase id (.ompos)"
    if (after[:1] == "-" or before[-1:] == "-") and tok[0].islower():
        return "kebab / css / plugin id (ompos-*)"
    if after[:1] == "." or before[-1:] == ".":
        return "namespace / project / file name (OmpOS.*)"
    if tok[0].islower():
        return "lowercase identifier or path (ompos)"
    if after[:1].isupper() or before[-1:].islower():
        return "Pascal/camel identifier (OmpOSTokens)"
    return "prose / name (OmpOS)"


def zone_of(rel: str) -> str:
    parts = rel.split("/")
    if len(parts) == 1:
        return "root"
    if parts[0] == ".planning" and len(parts) > 2:
        return f".planning/{parts[1]}" if parts[1] in ("milestones", "phases", "research") else ".planning"
    if parts[0] == "docs" and len(parts) > 2 and parts[1] == "runbooks" and parts[2] == "evidence":
        return "docs/runbooks/evidence"
    return parts[0]


# --------------------------------------------------------------------------- collector


class Collector:
    def __init__(self):
        self.tokens: dict[str, dict] = {}
        self.idents: collections.Counter[str] = collections.Counter()
        self.kinds: collections.Counter[str] = collections.Counter()
        self.zones: dict[str, dict] = {}
        self.excluded: dict[str, dict] = {}
        self.abbr: dict[str, dict] = {}
        self.engine_edits: collections.Counter[str] = collections.Counter()
        self.unmapped: collections.Counter[str] = collections.Counter()
        self.rejects: dict[str, dict] = {}
        self.reject_windows: collections.Counter[str] = collections.Counter()
        self.other_omp: dict[str, dict] = {}
        self.omp_kinds: collections.Counter[str] = collections.Counter()
        self.adapter_idents: collections.Counter[str] = collections.Counter()
        self.keep_counts: collections.Counter[str] = collections.Counter()
        self.url_hits: list[dict] = []
        self.url_excluded = 0
        self.url_tokens = 0
        self.vetoed: list[dict] = []
        self.kept: dict[str, list[dict]] = {}
        self.flags: dict[str, dict] = {}
        self.article_hits: list[str] = []
        self.files_rewritten: dict[str, int] = {}
        self.non_utf8: list[str] = []
        self.nul_text: list[str] = []
        self.text_files = 0
        self.binary_files = 0

    @staticmethod
    def bump(table: dict, key: str, loc, n: int = 1, **extra):
        """Count one hit; `loc` is a str or a zero-argument callable evaluated only while examples are missing."""
        item = table.get(key)
        if item is None:
            item = table[key] = {"count": 0, "files": 0, "examples": [], **extra}
        item["count"] += n
        if len(item["examples"]) < EXAMPLES_PER_ITEM:
            example = loc() if callable(loc) else loc
            if example not in item["examples"]:
                item["examples"].append(example)
        return item


def line_of(text: str, idx: int) -> int:
    return text.count("\n", 0, idx) + 1


KEEP_START = "rebrand:keep-start"
KEEP_END = "rebrand:keep-end"


def keep_blocks(rel: str, text: str) -> list[tuple[int, int, int, int]]:
    """Keep-marker blocks of a text file: (start offset, end offset, first line, last line).

    A block runs from the start of the line containing `rebrand:keep-start` to the end of the line
    containing `rebrand:keep-end` (both marker lines included, in any comment syntax). Blocks do not
    nest; an unbalanced, nested or one-line marker is a RebrandError that names the file and line.
    """
    if "rebrand:keep-" not in text:
        return []
    blocks: list[tuple[int, int, int, int]] = []
    opened: tuple[int, int] | None = None
    pos = 0
    for lineno, line in enumerate(text.split("\n"), 1):
        start, end = pos, pos + len(line)
        pos = end + 1
        has_start, has_end = KEEP_START in line, KEEP_END in line
        if has_start and has_end:
            raise RebrandError(f"{rel}:{lineno}: {KEEP_START} and {KEEP_END} on the same line")
        if has_start:
            if opened is not None:
                raise RebrandError(f"{rel}:{lineno}: nested {KEEP_START} (the block opened at line {opened[1]} is still open)")
            opened = (start, lineno)
        elif has_end:
            if opened is None:
                raise RebrandError(f"{rel}:{lineno}: {KEEP_END} without a matching {KEEP_START}")
            blocks.append((opened[0], end, opened[1], lineno))
            opened = None
    if opened is not None:
        raise RebrandError(f"{rel}:{opened[1]}: {KEEP_START} is never closed by {KEEP_END}")
    return blocks


# --------------------------------------------------------------------------- per-file processing


def process_text(rel: str, text: str, cfg: Config, opts: Options, col: Collector | None,
                 content_cls: str | None):
    """Return the list of accepted edits (empty when the content is excluded)."""
    cands = candidates(text, cfg, opts)
    blocks = keep_blocks(rel, text) if content_cls is None else []
    kb_spans = [(b[0], b[1], i) for i, b in enumerate(blocks)]
    kb_starts = [b[0] for b in blocks]
    kb_tokens = [0] * len(blocks)
    frozen_cls, frozen = cfg.frozen_ranges(rel, text) if cands else (None, [])
    fstarts = [f[0] for f in frozen]
    brand_starts = {c[0] for c in cands if c[4] == "brand"}

    keep_spans: list[tuple[int, int, str]] = []
    if col is not None or cands:
        for name, rx, left_not, _census in cfg.keep:
            if rx is None:
                continue  # counted by the omp census scan instead
            hits = 0
            for m in rx.finditer(text):
                if kb_spans and overlap(kb_spans, kb_starts, m.start(), m.end()) is not None:
                    continue
                if left_not is not None and m.start() and left_not.match(text[m.start() - 1]):
                    continue
                hits += 1
                keep_spans.append((m.start(), m.end(), name))
            if col is not None and content_cls is None and hits:
                col.keep_counts[name] += hits
    kspans = merged(keep_spans)
    kstarts = [k[0] for k in kspans]

    url_spans: list[tuple[int, int, str]] = []
    url_owner: dict[str, str] = {}
    if cands and not opts.urls:
        for name, rx, line_rx, owner in cfg.url_rules:
            url_owner[name] = owner
            for m in rx.finditer(text):
                if line_rx is not None:
                    ls = text.rfind("\n", 0, m.start()) + 1
                    le = text.find("\n", m.end())
                    if not line_rx.search(text[ls : le if le >= 0 else len(text)]):
                        continue
                url_spans.append((m.start(), m.end(), name))
    uspans = merged(url_spans)
    ustarts = [u[0] for u in uspans]

    accepted = []
    seen_url_spans: set[int] = set()
    local_tokens: collections.Counter[str] = collections.Counter()
    for s, e, old, new, src in cands:
        if kb_spans and (kb := overlap(kb_spans, kb_starts, s, e)) is not None:
            if src == "brand":
                kb_tokens[kb[2]] += 1
            continue
        if src == "brand" and col is not None and content_cls is None:
            prefix2 = text[max(0, s - 2) : s]
            if len(prefix2) == 2 and prefix2[0] in "aA" and prefix2[1] == " " and (
                s == 2 or not text[s - 3].isalnum()
            ):
                if len(col.article_hits) < 20:
                    col.article_hits.append(f"{rel}:{line_of(text, s)}")
            if (s > 0 and text[s - 1].isdigit()) or (e < len(text) and text[e].isdigit()):
                w = word_at(text, s, e)
                col.bump(col.flags, w, lambda: f"{rel}:{line_of(text, s)}", reason="brand token touches a digit")
        if frozen and (i := bisect.bisect_right(fstarts, s) - 1) >= 0 and frozen[i][0] <= s < frozen[i][1]:
            if col is not None and src == "brand":
                _count_excluded(col, frozen_cls or "frozen", rel, old, text, s)
                _count_url_in_excluded(col, uspans, ustarts, s, e, seen_url_spans)
            continue
        if content_cls is not None:
            if col is not None and src == "brand":
                _count_excluded(col, content_cls, rel, old, text, s)
                _count_url_in_excluded(col, uspans, ustarts, s, e, seen_url_spans)
            continue
        u = overlap(uspans, ustarts, s, e) if uspans else None
        if u is not None:
            if col is not None:
                col.url_tokens += 1
                if u[0] not in seen_url_spans:
                    seen_url_spans.add(u[0])
                    col.url_hits.append(
                        {"file": rel, "line": line_of(text, u[0]), "text": text[u[0] : u[1]],
                         "rule": u[2], "owner": url_owner.get(u[2], "")}
                    )
            continue
        k = overlap(kspans, kstarts, s, e) if kspans else None
        if k is not None:
            if col is not None:
                col.vetoed.append({"file": rel, "line": line_of(text, s), "text": old, "keep": k[2]})
            continue
        accepted.append((s, e, old, new, src))
        if col is not None:
            _count_accepted(col, cfg, rel, text, s, e, old, new, src, local_tokens)
    if col is not None:
        if accepted:
            col.files_rewritten[rel] = len(accepted)
        if blocks:
            col.kept[rel] = [
                {"firstLine": b[2], "lastLine": b[3], "brandTokens": kb_tokens[i]} for i, b in enumerate(blocks)
            ]
        _scan_omp_words(rel, text, cfg, opts, col, brand_starts, content_cls, kb_spans, kb_starts)
    return accepted


def _count_excluded(col: Collector, cls: str, rel: str, tok: str, text: str, s: int):
    ex = col.excluded.setdefault(cls, {"files": set(), "tokens": 0})
    ex["files"].add(rel)
    ex["tokens"] += 1


def _count_url_in_excluded(col: Collector, uspans, ustarts, s: int, e: int, seen: set[int]):
    u = overlap(uspans, ustarts, s, e) if uspans else None
    if u is not None and u[0] not in seen:
        seen.add(u[0])
        col.url_excluded += 1


def _count_accepted(col, cfg, rel, text, s, e, old, new, src, local_tokens):
    def example() -> str:
        return f"{rel}:{line_of(text, s)}"

    if src == "brand":
        if old not in cfg.token_map:
            col.unmapped[old] += 1
        item = Collector.bump(col.tokens, old, example, replacement=new)
        if local_tokens[old] == 0:
            item["files"] += 1
        local_tokens[old] += 1
        col.idents[ident_at(text, s, e)] += 1
        col.kinds[brand_kind(text, s, e, old)] += 1
        z = col.zones.setdefault(zone_of(rel), {"files": set(), "tokens": 0})
        z["files"].add(rel)
        z["tokens"] += 1
    elif src == "abbreviation":
        item = Collector.bump(col.abbr, old, example, replacement=new)
    else:
        col.engine_edits[old] += 1


def _scan_omp_words(rel, text, cfg, opts, col, brand_starts, content_cls, kept=(), kept_starts=()):
    """Reject scan (every ompos near-match, every other omp word) and engine-token census."""
    if content_cls == "rename-docs":
        return
    for m in _OMP_RE.finditer(text):
        i = m.start()
        if kept and overlap(kept, kept_starts, i, i + 3) is not None:
            continue
        if i in brand_starts:
            continue
        piece5 = text[i : i + 5]
        left = text[i - 1] if i > 0 else ""
        if piece5.lower() == "ompos":
            _record_ompos_reject(rel, text, i, col)
            continue
        right = text[i + 3] if i + 3 < len(text) else ""
        tok = text[i : i + 3]
        if tok == "omp" and not left.isalpha() and not right.islower():
            kind = "bare omp (engine)" if not right.isupper() else "camel omp identifier (adapter)"
        elif tok == "Omp" and not (left.isupper()) and not right.islower():
            kind = (
                "Pascal Omp identifier (adapter)"
                if right.isupper() or left.islower()
                else "Omp as a word (engine, prose)"
            )
        elif tok == "OMP" and not left.isalpha() and not right.isalpha():
            kind = "OMP upper (engine)"
        else:
            w = word_at(text, i, i + 3)
            Collector.bump(col.other_omp, w, lambda: f"{rel}:{line_of(text, i)}")
            continue
        if content_cls is None:
            col.omp_kinds[kind] += 1
            if "adapter" in kind:
                col.adapter_idents[word_at(text, i, i + 3)] += 1


_OMP_RE = re.compile(r"omp", re.I)


def _record_ompos_reject(rel, text, i, col):
    tok = text[i : i + 5]
    left = text[i - 1] if i > 0 else ""
    right = text[i + 5] if i + 5 < len(text) else ""
    w = word_at(text, i, i + 5)
    if left.isalpha() and tok in ("ompos", "OMPOS") and left in "cC":
        reason = "embedded in a longer word: letter before 'ompos' (compose/composer/composite/composition/decompose family)"
        severity = "benign"
    elif left.isalpha():
        reason = f"left edge is the letter {left!r} before {tok!r}: not a brand token, review"
        severity = "suspect"
    elif right.islower():
        reason = f"right edge is the lowercase letter {right!r} after {tok!r}: not a brand token, review"
        severity = "suspect"
    else:
        reason = f"casing {tok!r} is not covered by the brand regex or tokenMap: review"
        severity = "suspect"
    item = Collector.bump(col.rejects, w, lambda: f"{rel}:{line_of(text, i)}", reason=reason, severity=severity)
    col.reject_windows[(left + tok + text[i + 5 : i + 7]).lower()] += 1
    return item


# --------------------------------------------------------------------------- repository access


def git(root: Path, *args: str, check: bool = True, text: bool = True):
    res = subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=text)
    if check and res.returncode != 0:
        err = res.stderr if text else res.stderr.decode("utf-8", "replace")
        raise RebrandError(f"git {' '.join(args)} failed: {err.strip()}")
    return res


class Entry:
    __slots__ = ("rel", "kind", "tracked", "content_cls", "path_cls", "new_rel")

    def __init__(self, rel: str, kind: str, tracked: bool):
        self.rel = rel
        self.kind = kind
        self.tracked = tracked
        self.content_cls: str | None = None
        self.path_cls: str | None = None
        self.new_rel = rel


def list_entries(root: Path) -> list[Entry]:
    if git(root, "rev-parse", "--is-inside-work-tree", check=False).returncode != 0:
        raise RebrandError(f"{root} is not a git work tree")
    entries: dict[str, Entry] = {}
    staged = git(root, "ls-files", "-z", "-s", text=False).stdout.decode("utf-8", "surrogateescape")
    for rec in filter(None, staged.split("\0")):
        meta, _, path = rec.partition("\t")
        mode = meta.split(" ")[0]
        kind = "gitlink" if mode == "160000" else "symlink" if mode == "120000" else "file"
        entries.setdefault(path, Entry(path, kind, True))
    others = git(root, "ls-files", "-z", "--others", "--exclude-standard", text=False).stdout
    for path in filter(None, others.decode("utf-8", "surrogateescape").split("\0")):
        entries.setdefault(path, Entry(path, "file", False))
    out = []
    for rel in sorted(entries):
        e = entries[rel]
        full = root / rel
        try:
            st = os.lstat(full)
        except FileNotFoundError:
            continue  # deleted in the work tree
        if e.kind == "file" and stat.S_ISLNK(st.st_mode):
            e.kind = "symlink"
        out.append(e)
    return out


# --------------------------------------------------------------------------- plan


class Plan:
    def __init__(self):
        self.entries: list[Entry] = []
        self.ops: list[dict] = []
        self.collisions: list[str] = []
        self.case_only: list[str] = []
        self.warnings: list[str] = []
        self.col = Collector()
        self.counts: collections.Counter[str] = collections.Counter()
        self.symlink_notes: list[str] = []
        self.pending: collections.Counter[str] = collections.Counter()


def build_plan(root: Path, cfg: Config, opts: Options, census: bool = True) -> Plan:
    plan = Plan()
    col = plan.col
    entries = list_entries(root)
    plan.entries = entries

    # ---- content census / edits
    for e in entries:
        e.content_cls, e.path_cls = cfg.policy(e.rel)
        if e.kind != "file":
            plan.counts[e.kind] += 1
            continue
        if Path(e.rel).suffix.lower() in cfg.binary_ext:
            col.binary_files += 1
            continue
        try:
            data = (root / e.rel).read_bytes()
        except OSError as exc:
            plan.warnings.append(f"cannot read {e.rel}: {exc}")
            continue
        if b"\0" in data:
            col.binary_files += 1
            col.nul_text.append(e.rel)
            continue
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            col.non_utf8.append(e.rel)
            continue
        col.text_files += 1
        edits = process_text(e.rel, text, cfg, opts, col if census else None, e.content_cls)
        for ed in edits:
            plan.pending[ed[4]] += 1
    plan.counts["text"] = col.text_files
    plan.counts["binary"] = col.binary_files
    plan.counts["file"] = sum(1 for e in entries if e.kind == "file")

    # ---- path plan
    for e in entries:
        parts = e.rel.split("/")
        if e.path_cls is not None:
            e.new_rel = e.rel
            continue
        last = len(parts) if e.kind == "file" else len(parts) - 1
        new_parts = [rename_segment(p, cfg, opts) if i < last else p for i, p in enumerate(parts)]
        e.new_rel = "/".join(new_parts)
        if e.kind in ("symlink", "gitlink") and e.new_rel != e.rel:
            plan.symlink_notes.append(f"{e.kind} {e.rel} moves with its renamed parent directory (name and target untouched)")
    _plan_paths(root, plan)
    return plan


def _plan_paths(root: Path, plan: Plan):
    entries = plan.entries
    # directory prefix consistency
    dir_new: dict[str, set[str]] = collections.defaultdict(set)
    for e in entries:
        op = e.rel.split("/")
        np_ = e.new_rel.split("/")
        for k in range(1, len(op)):
            if e.new_rel == e.rel:
                dir_new["/".join(op[:k])].add(op[k - 1])
            else:
                dir_new["/".join(op[:k])].add(np_[k - 1])
    dir_ops = []
    for prefix, names in dir_new.items():
        old_name = prefix.split("/")[-1]
        if len(names) > 1:
            plan.collisions.append(
                f"mixed directory {prefix}: entries disagree on its new name ({', '.join(sorted(names))}); "
                "a path-excluded file probably lives inside a renamed directory"
            )
            continue
        (new_name,) = names
        if new_name != old_name:
            parent = "/".join(prefix.split("/")[:-1])
            dir_ops.append((prefix, (parent + "/" if parent else "") + new_name))
    tracked_dirs = set()
    for e in entries:
        if e.tracked:
            parts = e.rel.split("/")
            for k in range(1, len(parts)):
                tracked_dirs.add("/".join(parts[:k]))
    ops = []
    for e in entries:
        if e.kind != "file":
            continue
        ob = e.rel.split("/")[-1]
        nb = e.new_rel.split("/")[-1]
        if ob != nb:
            parent = "/".join(e.rel.split("/")[:-1])
            ops.append(
                {"type": "file", "old": e.rel, "new": (parent + "/" if parent else "") + nb, "tracked": e.tracked,
                 "depth": len(e.rel.split("/"))}
            )
    for old, new in dir_ops:
        ops.append({"type": "dir", "old": old, "new": new, "tracked": old in tracked_dirs,
                    "depth": len(old.split("/"))})
    ops.sort(key=lambda o: (-o["depth"], o["old"]))
    plan.ops = ops

    # collisions between final paths
    final: dict[str, list[str]] = collections.defaultdict(list)
    for e in entries:
        final[e.new_rel.lower()].append(e.rel)
    for low, olds in final.items():
        if len(olds) > 1:
            plan.collisions.append(f"{len(olds)} paths map to {low}: {', '.join(sorted(olds)[:4])}")
    file_targets = {e.new_rel.lower() for e in entries if e.kind == "file"}
    dir_targets = set()
    for e in entries:
        parts = e.new_rel.lower().split("/")
        for k in range(1, len(parts)):
            dir_targets.add("/".join(parts[:k]))
    for clash in sorted(file_targets & dir_targets):
        plan.collisions.append(f"{clash} would be both a file and a directory")
    # destination already occupied on disk or among siblings that are not moving away
    moving = {o["old"].lower() for o in ops}
    for o in ops:
        dest = o["new"]
        if dest.lower() in moving:
            continue
        if os.path.lexists(root / dest):
            plan.collisions.append(f"destination {dest} already exists on disk (tracked, untracked or ignored)")
    seen = collections.defaultdict(list)
    for o in ops:
        seen[(o["new"].lower())].append(o["old"])
    for dest, olds in seen.items():
        if len(olds) > 1:
            plan.collisions.append(f"{len(olds)} moves target {dest}: {', '.join(sorted(olds)[:4])}")
    plan.collisions = sorted(dict.fromkeys(plan.collisions))
    for e in entries:
        if e.new_rel != e.rel and e.new_rel.lower() == e.rel.lower():
            plan.case_only.append(e.rel)
        if len(e.new_rel.split("/")[-1].encode("utf-8")) > 255:
            plan.warnings.append(f"basename over 255 bytes after rename: {e.new_rel}")


# --------------------------------------------------------------------------- report


def _ancestors(rel: str):
    parts = rel.split("/")
    return ["/".join(parts[:k]) for k in range(1, len(parts))]


def build_report(root: Path, cfg: Config, opts: Options, plan: Plan, command: str) -> dict:
    col = plan.col
    tokens = [
        {"token": t, "replacement": v["replacement"], "count": v["count"], "files": v["files"],
         "examples": v["examples"]}
        for t, v in sorted(col.tokens.items(), key=lambda kv: -kv[1]["count"])
    ]
    zones = {z: {"files": len(v["files"]), "tokens": v["tokens"]} for z, v in sorted(col.zones.items())}
    excluded = {c: {"files": len(v["files"]), "tokens": v["tokens"]} for c, v in sorted(col.excluded.items())}
    rejects = [
        {"word": w, "count": v["count"], "examples": v["examples"], "severity": v["severity"], "reason": v["reason"]}
        for w, v in sorted(col.rejects.items(), key=lambda kv: (kv[1]["severity"], -kv[1]["count"], kv[0]))
    ]
    dirs = [o for o in plan.ops if o["type"] == "dir"]
    files = [o for o in plan.ops if o["type"] == "file"]
    renamed_dirs = {o["old"] for o in dirs}
    inner = [o for o in files if any(a in renamed_dirs for a in _ancestors(o["old"]))]
    loose = [o for o in files if o not in inner]
    keep_counts = dict(sorted(col.keep_counts.items()))
    for name, _rx, _left, census_key in cfg.keep:
        if census_key:
            keep_counts[name] = col.omp_kinds.get(census_key, 0)
    keep_counts = dict(sorted(keep_counts.items()))
    ambiguities = _ambiguities(plan, opts)
    return {
        "generatedAt": _dt.datetime.now(_dt.timezone.utc).isoformat(timespec="seconds"),
        "command": command,
        "root": str(root),
        "options": {"urls": opts.urls, "engineAdapters": opts.engine_adapters},
        "summary": {
            "entries": len(plan.entries),
            "files": plan.counts["file"], "textFiles": col.text_files, "binaryFiles": col.binary_files,
            "symlinksSkipped": plan.counts["symlink"], "gitlinksSkipped": plan.counts["gitlink"],
            "nonUtf8Skipped": len(col.non_utf8), "nulTextSkipped": len(col.nul_text),
            "filesToRewrite": len(col.files_rewritten),
            "brandTokensToRewrite": sum(t["count"] for t in tokens),
            "distinctBrandIdentifiers": len(col.idents),
            "productAbbreviationEdits": sum(v["count"] for v in col.abbr.values()),
            "engineAdapterEdits": sum(col.engine_edits.values()),
            "tokensInExcludedContent": sum(v["tokens"] for v in excluded.values()),
            "urlProtectedTokens": col.url_tokens,
            "pathOps": len(plan.ops), "dirOps": len(dirs), "fileOps": len(files),
            "collisions": len(plan.collisions), "caseOnlyRenames": len(plan.case_only),
            "rejectedOmposWords": len(rejects),
            "rejectedOmposOccurrences": sum(r["count"] for r in rejects),
            "suspectRejectedWords": sum(1 for r in rejects if r["severity"] == "suspect"),
            "otherOmpWords": len(col.other_omp),
            "otherOmpOccurrences": sum(v["count"] for v in col.other_omp.values()),
            "vetoedByKeepPatterns": len(col.vetoed),
            "keepMarkerBlocks": sum(len(v) for v in col.kept.values()),
            "keepMarkerFiles": len(col.kept),
        },
        "tokens": tokens,
        "unmappedCasings": dict(col.unmapped),
        "identifiers": dict(col.idents.most_common()),
        "categories": {
            "zones": zones,
            "excludedContent": excluded,
            "kinds": dict(col.kinds.most_common()),
        },
        "rejected": rejects,
        "rejectWindows": dict(sorted(col.reject_windows.items())),
        "otherOmpWords": {
            w: {"count": v["count"], "examples": v["examples"]}
            for w, v in sorted(col.other_omp.items(), key=lambda kv: -kv[1]["count"])
        },
        "engine": {
            "mode": opts.engine_adapters,
            "keptTokenCensus": dict(col.omp_kinds.most_common()),
            "keepPatternCounts": keep_counts,
            "adapterIdentifiers": {"distinct": len(col.adapter_idents), "occurrences": sum(col.adapter_idents.values()),
                                    "top": dict(col.adapter_idents.most_common(25)),
                                    "all": dict(col.adapter_idents.most_common())},
            "productAbbreviations": {
                k: {"replacement": cfg.abbr[k], "count": col.abbr.get(k, {}).get("count", 0),
                    "examples": col.abbr.get(k, {}).get("examples", [])}
                for k in cfg.abbr
            },
            "adapterRenames": dict(col.engine_edits.most_common()),
        },
        "urlProtected": col.url_hits,
        "urlProtectedInExcludedFiles": col.url_excluded,
        "paths": {
            "ops": plan.ops, "dirs": len(dirs), "files": len(files), "innerFiles": len(inner), "looseFiles": len(loose),
            "collisions": plan.collisions, "caseOnly": plan.case_only, "warnings": plan.warnings,
            "symlinkNotes": plan.symlink_notes,
        },
        "flags": [{"word": w, **v} for w, v in sorted(col.flags.items())],
        "articleHits": col.article_hits,
        "vetoed": col.vetoed,
        "keptBlocks": {f: v for f, v in sorted(col.kept.items())},
        "nonUtf8": col.non_utf8,
        "nulText": col.nul_text,
        "ambiguities": ambiguities,
        "ownerActions": _owner_actions(plan, opts),
    }


def _ambiguities(plan: Plan, opts: Options) -> list[str]:
    col = plan.col
    out = [
        "Recorded protocol frames (tests/*/TestData/frames/**) are excluded from content rewriting because replay tests compare bytes; they hold a few real 'ompos' tokens (a /tmp/ompos-rpc-live path). Confirm they may stay.",
        "All of docs/plans/v0.16.0-research/** except 01-rename-inventory.md, every ADR, .planning/phases/** and docs/plans/** are REWRITTEN (the owner decision overrides report 01's default 'frozen'). Only docs/runbooks/evidence/**, .planning/milestones/**, released CHANGELOG sections and generated files are frozen.",
        "The Vercel/Railway project-name rule is textual: ompos-web/ompos-api are protected only on lines that also mention vercel or railway. ompos-api is also the default JWT issuer string in code and tests; those lines are renamed, so every deployed API and worker must switch issuer together (hard cutover).",
        "NOTICE and docs/licensing.md carry the trademark clause ('no right to use the name OmpOS or its logo'): the tool rewrites the name, the owner must approve the legal wording.",
        "ompos.dev (hooks schema $id) is treated as an owned domain and protected until --urls; it is not a Vercel/GitHub name, so say whether it should follow now.",
        "Only five first-party Omp* identifiers are product abbreviations (config list). Every other first-party Omp*/omp* identifier is kept as engine integration; the report lists them (engine.adapterIdentifiers) for a reviewer pass.",
        "Android ApplicationId com.swcstudiospace.ompos is renamed by the token rule (com.swcstudiospace.autonogrammer): this is a new store identity, previous installs will not upgrade.",
        "Submodules plugins/agent-substrate, agent-swarm, claude-ultrathink are not in this tree; if they read OMPOS_* or .ompos they must be changed in their own repos (gate G9).",
        "Hard cutover leaves the C# runtime reading only new names: see the runtime-migration list in docs/runbooks/rebrand-autonogrammer.md.",
    ]
    if col.unmapped:
        out.append(f"Casings the token map does not list matched and used the fallback: {dict(col.unmapped)}")
    if any(r["severity"] == "suspect" for r in col.rejects.values()):
        out.append("Rejected near-matches marked 'suspect' need a human look (see the rejected table).")
    if col.flags:
        out.append(f"{len(col.flags)} brand tokens touch a digit (flags): confirm they are not hashes or base64.")
    if col.article_hits:
        out.append(f"'a <brand>' article hits ({len(col.article_hits)}+): 'a Autonogrammer' reads wrong, fix by hand (should be 'an').")
    if col.non_utf8:
        out.append(f"{len(col.non_utf8)} text files are not valid UTF-8 and were skipped: {col.non_utf8[:5]}")
    if col.nul_text:
        out.append(f"{len(col.nul_text)} extension-less or unknown files contain NUL bytes and were treated as binary: {col.nul_text[:5]}")
    return out


def _owner_actions(plan: Plan, opts: Options) -> list[str]:
    col = plan.col
    acts = []
    if col.url_hits or col.url_excluded:
        acts.append(
            f"URLs and hostnames protected until --urls ({len(col.url_hits)} places in rewritable files, "
            f"{col.url_excluded} more in frozen files): rename the GitHub repo swcstudiospace/OmpOS, the Vercel project "
            "(ompos-web*.vercel.app), the Railway service/project names and register the new domain; then re-run with --urls."
        )
    acts += [
        "Rename the GitHub repository, Vercel project and Railway service/project, then run apply --urls (or re-run after they move).",
        "Set the renamed environment variables (AUTONOGRAMMER_*) in Vercel project settings, Railway service variables, repository secrets and developer .env files.",
        "Recreate or migrate local containers, volumes, podman secrets and the PostgreSQL schema/Dragonfly keys that carried the old names; images and labels change name (hard cutover, no alias).",
        "Regenerate generated files in order (see the runbook): relock, dotnet format, openapi, notices, brand assets.",
        "After the rename commit exists, add gitleaks fingerprints for the relocated recorded frames (the old .gitleaksignore lines stay untouched).",
        "Add the rename entry under [Unreleased] in CHANGELOG.md and review the NOTICE trademark wording.",
        "Delete obj/ and bin/ before the first build (they cache the old paths).",
    ]
    return acts


# --------------------------------------------------------------------------- rendering


def _t(rows, header):
    out = ["| " + " | ".join(header) + " |", "|" + "|".join(" --- " for _ in header) + "|"]
    for r in rows:
        out.append("| " + " | ".join(str(c).replace("|", "\\|") for c in r) + " |")
    return out


def render_md(rep: dict, console: bool = False) -> str:
    s = rep["summary"]
    L: list[str] = []
    L.append(f"# Rebrand report ({rep['command']})")
    L.append("")
    L.append(f"Root `{rep['root']}`; urls={rep['options']['urls']}; engine-adapters={rep['options']['engineAdapters']}; generated {rep['generatedAt']}.")
    L.append("")
    L.append("## Headline")
    L += _t([[k, v] for k, v in s.items()], ["measure", "value"])
    L.append("")
    L.append("## Matched brand tokens (every distinct casing)")
    L += _t([[f"`{t['token']}`", f"`{t['replacement']}`", t["count"], t["files"], "; ".join(t["examples"])] for t in rep["tokens"]],
            ["token", "becomes", "count", "files", "two examples"])
    if rep["unmappedCasings"]:
        L.append("")
        L.append(f"Unmapped casings (fallback used): {rep['unmappedCasings']}")
    L.append("")
    L.append("## Per-category counts")
    L.append("")
    L.append("Zones (rewritable content):")
    L += _t([[z, v["files"], v["tokens"]] for z, v in rep["categories"]["zones"].items()], ["zone", "files", "tokens"])
    L.append("")
    L.append("Kinds of occurrence:")
    L += _t(list(rep["categories"]["kinds"].items()), ["kind", "tokens"])
    L.append("")
    L.append("Tokens left untouched in excluded or frozen content:")
    L += _t([[c, v["files"], v["tokens"]] for c, v in rep["categories"]["excludedContent"].items()], ["class", "files", "tokens"])
    L.append("")
    L.append("## Rejected near-matches of `ompos` (boundary proof)")
    L.append("")
    L.append(f"{s['rejectedOmposWords']} distinct words, {s['rejectedOmposOccurrences']} occurrences contain `ompos` in any casing but are NOT brand tokens; {s['suspectRejectedWords']} need review.")
    L.append("")
    rej = rep["rejected"]
    if console:
        suspects = [r for r in rej if r["severity"] == "suspect"]
        benign = [r for r in rej if r["severity"] == "benign"]
        shown = suspects + benign
    else:
        shown = rej
    L += _t([[r["word"], r["count"], r["severity"], "; ".join(r["examples"]), r["reason"]] for r in shown],
            ["word", "count", "severity", "examples", "reason"])
    L.append("")
    L.append(f"Other `omp`-containing words (prompt, StringComparison, ...): {s['otherOmpWords']} distinct, {s['otherOmpOccurrences']} occurrences; top 15:")
    L += _t([[w, v["count"], "; ".join(v["examples"])] for w, v in list(rep["otherOmpWords"].items())[:15]], ["word", "count", "examples"])
    L.append("")
    L.append("## Engine tokens (kept) and product abbreviations (renamed)")
    eng = rep["engine"]
    L.append("")
    L += _t(list(eng["keptTokenCensus"].items()), ["kept token class (rewritable files)", "occurrences"])
    L.append("")
    L += _t(list(eng["keepPatternCounts"].items()), ["keep pattern", "matches (proof: unchanged by apply)"])
    L.append("")
    L.append(f"First-party engine-adapter identifiers kept in mode `{eng['mode']}`: {eng['adapterIdentifiers']['distinct']} distinct, {eng['adapterIdentifiers']['occurrences']} occurrences; top 25:")
    L += _t(list(eng["adapterIdentifiers"]["top"].items()), ["identifier", "occurrences"])
    L.append("")
    L += _t([[k, v["replacement"], v["count"], "; ".join(v["examples"])] for k, v in eng["productAbbreviations"].items()],
            ["product abbreviation", "becomes", "count", "examples"])
    if eng["adapterRenames"]:
        L.append("")
        L.append(f"Adapter renames applied (mode rename): {sum(eng['adapterRenames'].values())} occurrences.")
    L.append("")
    L.append("## Path rename plan")
    p = rep["paths"]
    L.append("")
    L.append(f"{len(p['ops'])} moves ({p['dirs']} directories, {p['files']} files), deepest first. Collision check: {'FAILED' if p['collisions'] else 'no collisions'}; case-only renames: {len(p['caseOnly'])}.")
    if p["collisions"]:
        L.append("")
        L += [f"- COLLISION: {c}" for c in p["collisions"]]
    if p["warnings"]:
        L += [f"- warning: {w}" for w in p["warnings"]]
    if p["symlinkNotes"]:
        L += [f"- {n}" for n in p["symlinkNotes"][:10]]
    ops = p["ops"][:12] if console else p["ops"]
    L.append("")
    L += _t([[o["type"], o["old"], o["new"], "git mv" if o["tracked"] else "os.rename"] for o in ops], ["type", "from", "to", "how"])
    if console and len(p["ops"]) > len(ops):
        L.append(f"... {len(p['ops']) - len(ops)} more in rebrand-report.md")
    L.append("")
    L.append(f"## Protected URLs and hostnames ({len(rep['urlProtected'])} places; {rep['urlProtectedInExcludedFiles']} more in frozen files)")
    L.append("")
    hits = rep["urlProtected"][:15] if console else rep["urlProtected"]
    L += _t([[h["file"] + ":" + str(h["line"]), h["text"], h["rule"]] for h in hits], ["where", "text", "rule"])
    if console and len(rep["urlProtected"]) > len(hits):
        L.append(f"... {len(rep['urlProtected']) - len(hits)} more in rebrand-report.md")
    if rep["flags"]:
        L.append("")
        L.append("## Flags")
        L += _t([[f["word"], f["count"], "; ".join(f["examples"]), f["reason"]] for f in rep["flags"]], ["word", "count", "examples", "reason"])
    if rep["vetoed"]:
        L.append("")
        L.append(f"## Brand tokens vetoed by an engine keep pattern: {len(rep['vetoed'])}")
        L += [f"- {v['file']}:{v['line']} {v['text']} inside {v['keep']}" for v in rep["vetoed"][:20]]
    if rep["keptBlocks"]:
        n_blocks = sum(len(v) for v in rep["keptBlocks"].values())
        L.append("")
        L.append(f"## Keep-marker blocks left untouched: {n_blocks} in {len(rep['keptBlocks'])} files")
        L += _t([[f, "; ".join(f"{b['firstLine']}-{b['lastLine']} ({b['brandTokens']} tokens)" for b in blocks)]
                 for f, blocks in rep["keptBlocks"].items()], ["file", "blocks (lines, brand tokens kept)"])
    L.append("")
    L.append("## Owner actions")
    L += [f"- {a}" for a in rep["ownerActions"]]
    L.append("")
    L.append("## Ambiguous items for the owner")
    L += [f"- {a}" for a in rep["ambiguities"]]
    L.append("")
    return "\n".join(L)


def write_reports(rep: dict, report_dir: Path, root: Path):
    report_dir = report_dir.resolve()
    try:
        report_dir.relative_to(root.resolve())
    except ValueError:
        pass
    else:
        raise RebrandError(f"--report-dir {report_dir} is inside --root; choose a directory outside the repository")
    report_dir.mkdir(parents=True, exist_ok=True)
    (report_dir / "rebrand-report.json").write_text(json.dumps(rep, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (report_dir / "rebrand-report.md").write_text(render_md(rep), encoding="utf-8")


# --------------------------------------------------------------------------- apply


def apply_plan(root: Path, cfg: Config, opts: Options, plan: Plan, allow_dirty: bool, out) -> dict:
    if plan.collisions:
        raise RebrandError("refusing to apply, path collisions:\n  " + "\n  ".join(plan.collisions))
    if not allow_dirty:
        dirty = git(root, "status", "--porcelain").stdout.strip()
        if dirty:
            raise RebrandError(
                "refusing to apply on a dirty tree (commit or stash first, or pass --allow-dirty):\n"
                + "\n".join(dirty.splitlines()[:10])
            )
    done = 0
    for op in plan.ops:
        src, dst = root / op["old"], root / op["new"]
        try:
            if op["tracked"]:
                git(root, "mv", "--", op["old"], op["new"])
            else:
                os.rename(src, dst)
        except (RebrandError, OSError) as exc:
            raise RebrandError(
                f"move {op['old']} -> {op['new']} failed after {done} successful moves: {exc}. "
                "The tree is partially renamed; inspect `git status` (nothing was committed)."
            ) from exc
        done += 1
    rewritten = 0
    for e in plan.entries:
        if e.kind != "file" or e.content_cls is not None:
            continue
        if Path(e.rel).suffix.lower() in cfg.binary_ext:
            continue
        # location after the moves: directories and basename already renamed
        new_full = root / e.new_rel
        try:
            data = new_full.read_bytes()
        except OSError:
            continue
        if b"\0" in data:
            continue
        try:
            text = data.decode("utf-8")
        except UnicodeDecodeError:
            continue
        edits = process_text(e.new_rel, text, cfg, opts, None, e.content_cls)
        if not edits:
            continue
        new_text = apply_edits(text, edits)
        mode = os.stat(new_full).st_mode
        fd, tmp = tempfile.mkstemp(prefix=".rebrand-", dir=str(new_full.parent))
        try:
            with os.fdopen(fd, "wb") as fh:
                fh.write(new_text.encode("utf-8"))
            os.chmod(tmp, stat.S_IMODE(mode))
            os.replace(tmp, new_full)
        except BaseException:
            if os.path.exists(tmp):
                os.unlink(tmp)
            raise
        rewritten += 1
    return {"moves": done, "filesRewritten": rewritten}


# --------------------------------------------------------------------------- verify


def verify_against(plan: Plan, rep: dict | None = None, baseline: dict | None = None) -> list[str]:
    problems = []
    if plan.pending:
        problems.append(
            f"{plan.pending['brand']} brand tokens, {plan.pending['abbreviation']} product abbreviations, "
            f"{plan.pending['engine-adapter']} adapter identifiers still rewritable"
        )
    if plan.ops:
        problems.append(f"{len(plan.ops)} paths still carry a brand token")
    if plan.collisions:
        problems.append("path collisions: " + "; ".join(plan.collisions[:3]))
    if baseline and rep:
        if baseline.get("rejectWindows") != rep["rejectWindows"]:
            problems.append("rejected near-match counts differ from the baseline (substring damage?)")
        for name, n in baseline["engine"]["keepPatternCounts"].items():
            now = rep["engine"]["keepPatternCounts"].get(name, 0)
            if now != n:
                problems.append(f"engine keep pattern {name!r}: baseline {n}, now {now}")
    return problems


# --------------------------------------------------------------------------- cli


def parse_args(argv: list[str]) -> argparse.Namespace:
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("--root", default=".", help="repository root (default: .)")
    common.add_argument("--config", default=str(DEFAULT_CONFIG))
    common.add_argument("--urls", action="store_true", help="also rewrite URLs/hostnames that depend on external renames")
    common.add_argument("--engine-adapters", choices=("keep", "rename"), default="keep",
                        help="keep (default) or rename first-party Omp* engine-integration identifiers")
    common.add_argument("--report-dir", default=DEFAULT_REPORT_DIR)
    parser = argparse.ArgumentParser(prog="rebrand.py", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command")
    sub.add_parser("dry-run", parents=[common], help="plan and report, write nothing to the repo (default)")
    ap = sub.add_parser("apply", parents=[common], help="git mv paths and rewrite contents")
    ap.add_argument("--allow-dirty", action="store_true")
    vp = sub.add_parser("verify", parents=[common], help="fail when anything would still be renamed")
    vp.add_argument("--baseline", help="rebrand-report.json from a dry-run before apply")
    if not argv or argv[0].startswith("-") and argv[0] not in ("-h", "--help"):
        argv = ["dry-run", *argv]
    return parser.parse_args(argv)


def main(argv: list[str] | None = None, out=None) -> int:
    out = out or sys.stdout
    args = parse_args(list(sys.argv[1:] if argv is None else argv))
    command = args.command or "dry-run"
    try:
        root = Path(args.root).resolve()
        cfg = Config.load(Path(args.config))
        opts = Options(urls=args.urls, engine_adapters=args.engine_adapters)
        plan = build_plan(root, cfg, opts)
        rep = build_report(root, cfg, opts, plan, command)
        report_dir = Path(args.report_dir)
        if command == "dry-run":
            write_reports(rep, report_dir, root)
            print(render_md(rep, console=True), file=out)
            print(f"Reports: {report_dir}/rebrand-report.md and rebrand-report.json", file=out)
            return 2 if plan.collisions else 0
        if command == "apply":
            res = apply_plan(root, cfg, opts, plan, getattr(args, "allow_dirty", False), out)
            plan2 = build_plan(root, cfg, opts, census=False)
            write_reports(rep, report_dir, root)
            problems = verify_against(plan2)
            print(f"apply: {res['moves']} moves, {res['filesRewritten']} files rewritten; report {report_dir}", file=out)
            if problems:
                print("post-apply verification found: " + "; ".join(problems), file=out)
                return 1
            print("post-apply verification: clean (a second run would change nothing)", file=out)
            return 0
        baseline = None
        if getattr(args, "baseline", None):
            try:
                baseline = json.loads(Path(args.baseline).read_text(encoding="utf-8"))
            except (OSError, ValueError) as exc:
                raise RebrandError(f"cannot read baseline: {exc}") from exc
        problems = verify_against(plan, rep, baseline)
        write_reports(rep, report_dir, root)
        if problems:
            print("verify FAILED:\n  " + "\n  ".join(problems), file=out)
            return 1
        print(f"verify OK: nothing left to rename ({rep['summary']['urlProtectedTokens']} URL tokens still protected)", file=out)
        return 0
    except RebrandError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
