#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 SWC Studio
"""Generate THIRD-PARTY-NOTICES.md for OmpOS (stdlib only, offline, deterministic).

Inputs (all read from this checkout and the local NuGet cache; nothing is fetched):
  * every packages.lock.json in the repository (resolved versions),
  * <nuget cache>/<id>/<version>/<id>.nuspec for licence, copyright and project data,
  * licence and notice files shipped inside the cached packages,
  * font directories (OFL.txt), vendored JavaScript (banners, wwwroot/lib/*/VERSION.txt), bundled images,
  * deploy/*.Containerfile pins (Bun, omp), .gitmodules, plugins/*/package.json.

Usage:
  python3 scripts/legal/generate-third-party-notices.py            write THIRD-PARTY-NOTICES.md
  python3 scripts/legal/generate-third-party-notices.py --check    exit 1 when the committed file is stale
  options: --output PATH, --cache DIR (default: $NUGET_PACKAGES or ~/.nuget/packages)

Exit codes: 0 ok, 1 stale (--check only), 2 a licence could not be determined or an input is missing.
A licence is never guessed: a package whose licence is neither an SPDX expression the script has a
text for, nor an entry of FILE_LICENCE_EXCEPTIONS / LICENCE_URL_MAP, is listed and the run fails.
The NuGet cache must be populated (run bootstrap.sh or dotnet restore first).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUTPUT_NAME = "THIRD-PARTY-NOTICES.md"
TEXTS = Path(__file__).resolve().parent / "license-texts"

# SPDX identifiers this script holds a licence text for (scripts/legal/license-texts/<id>.txt).
KNOWN_SPDX = ("Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "MIT", "PostgreSQL")

# Packages whose nuspec declares <license type="file">. The licence is the vendor-verified one; the
# marker must appear in the file shipped inside the package, so a changed file in a newer version fails
# the run instead of silently keeping the old conclusion.
FILE_LICENCE_EXCEPTIONS = {
    "Avalonia.Angle.Windows.Natives": (
        "BSD-3-Clause",
        "ANGLE Project Authors",
        "Vendor repository https://github.com/AvaloniaUI/angle (LICENSE, package commit 1c89805903c1482166356d3b950d474973180e61): BSD-3-Clause, Copyright 2018 The ANGLE Project Authors.",
    ),
    "Unicode.net": (
        "MIT",
        "NeoSmart Technologies",
        "Vendor repository https://github.com/neosmart/unicode.net (LICENSE): MIT, Copyright (c) 2017 NeoSmart Technologies.",
    ),
}

# Packages with a <licenseUrl> but no <license> element. Empty today; extend only after reading the page.
LICENCE_URL_MAP: dict[str, str] = {}

# Packages that are build or design time even if the lock files list them as ordinary references.
BUILD_TIME_IDS = {
    "Microsoft.NET.Sdk.WebAssembly.Pack",
    "Microsoft.NET.ILLink.Tasks",
    "Microsoft.AspNetCore.Components.WebAssembly.DevServer",
    "Microsoft.Extensions.ApiDescription.Server",
    "Microsoft.AspNetCore.Components.Analyzers",
}

SKIP_DIRS = {".git", "bin", "obj", "node_modules", ".planning", ".swarm", ".dotnet", ".nuget"}
SUBMODULE_LICENCES = {
    "plugins/claude-ultrathink": "AGPL-3.0-or-later (its LICENSE; same family as OmpOS)",
    "plugins/agent-swarm": "none published (no LICENSE file; OQ-102)",
    "plugins/agent-substrate": "none published (no LICENSE file, private repository; OQ-102)",
}
FONT_EXT = {".ttf", ".otf", ".woff", ".woff2"}
IMAGE_EXT = {".png", ".svg", ".ico", ".jpg", ".jpeg", ".webp", ".gif"}
NOTICE_PREFIXES = ("license", "licence", "third-party", "notice", "copying")


class Undetermined(Exception):
    """Collected licence problems; message lists every offender."""


def walk(root: Path):
    """Yield files under root in sorted order, skipping build output, caches and submodule checkouts."""
    for dirpath, dirnames, filenames in os.walk(root):
        rel = Path(dirpath).relative_to(REPO).as_posix()
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS and not (rel == "plugins" and d != "gsd-core-omp"))
        for name in sorted(filenames):
            yield Path(dirpath) / name


def read_text(path: Path) -> str:
    """Read UTF-8 (BOM tolerated), normalise newlines, strip trailing whitespace per file."""
    raw = path.read_bytes().decode("utf-8-sig", errors="replace")
    return raw.replace("\r\n", "\n").replace("\r", "\n").strip("\n") + "\n"


def cell(text: str) -> str:
    """Make a string safe for one markdown table cell."""
    return " ".join(text.split()).replace("|", "\\|")


def fence(text: str) -> tuple[str, str]:
    """Return an opening and closing fence longer than any backtick run in text."""
    longest = max((len(m) for m in re.findall(r"`+", text)), default=0)
    ticks = "`" * max(3, longest + 1)
    return ticks + "text", ticks


def scope_rank(scope: str) -> int:
    return {"shipped": 0, "build-time": 1, "tools": 2, "test": 3}[scope]


def collect_packages() -> dict[tuple[str, str], str]:
    """Map (id, resolved version) to scope: shipped | tools | test (lock-path based)."""
    found: dict[tuple[str, str], str] = {}
    for path in walk(REPO):
        if path.name != "packages.lock.json":
            continue
        rel = path.relative_to(REPO).as_posix()
        scope = "test" if rel.startswith("tests/") else "tools" if rel.startswith("tools/") else "shipped"
        data = json.loads(path.read_text(encoding="utf-8"))
        for deps in data.get("dependencies", {}).values():
            for name, info in deps.items():
                if info.get("type") == "Project" or "resolved" not in info:
                    continue
                key = (name, info["resolved"])
                if key not in found or scope_rank(scope) < scope_rank(found[key]):
                    found[key] = scope
    return found


def nuspec_info(cache: Path, name: str, version: str) -> dict | None:
    """Parse the cached nuspec of a package; None when it is not in the cache."""
    folder = cache / name.lower() / version
    spec = folder / f"{name.lower()}.nuspec"
    if not spec.is_file():
        return None
    root = ET.fromstring(spec.read_bytes().decode("utf-8-sig"))
    meta = next(el for el in root if el.tag.endswith("metadata"))

    def text_of(tag: str) -> str:
        for el in meta:
            if el.tag.split("}")[-1] == tag:
                return (el.text or "").strip()
        return ""

    lic_type = lic_text = ""
    for el in meta:
        if el.tag.split("}")[-1] == "license":
            lic_type, lic_text = el.get("type", ""), (el.text or "").strip()
    return {
        "folder": folder,
        "licence_type": lic_type,
        "licence_text": lic_text,
        "licence_url": text_of("licenseUrl"),
        "copyright": text_of("copyright"),
        "authors": text_of("authors"),
        "project_url": text_of("projectUrl"),
        "dev": text_of("developmentDependency").lower() == "true",
    }


def resolve_licence(name: str, info: dict) -> tuple[str, str | None, str | None]:
    """Return (spdx expression, file text or None, note or None); raise KeyError when undetermined."""
    kind, text = info["licence_type"], info["licence_text"]
    if kind == "expression":
        tokens = [t for t in re.split(r"[\s()]+", text) if t and t not in {"AND", "OR", "WITH"}]
        unknown = [t for t in tokens if t not in KNOWN_SPDX]
        if unknown:
            raise KeyError(f"expression '{text}' needs a text for {', '.join(unknown)}")
        return text, None, None
    if kind == "file":
        if name not in FILE_LICENCE_EXCEPTIONS:
            raise KeyError("licence is a file inside the package and no vendor-verified exception is recorded")
        spdx, marker, note = FILE_LICENCE_EXCEPTIONS[name]
        path = info["folder"] / text
        if not path.is_file():
            raise KeyError(f"licence file '{text}' not found in the cached package")
        body = read_text(path)
        if marker not in body:
            raise KeyError(f"licence file '{text}' no longer mentions '{marker}'; re-verify the exception")
        return spdx, body, note
    if not kind and info["licence_url"] in LICENCE_URL_MAP:
        return LICENCE_URL_MAP[info["licence_url"]], None, None
    raise KeyError(f"no licence element and licenseUrl '{info['licence_url'] or '(none)'}' is not in LICENCE_URL_MAP")


def package_notice_files(folder: Path) -> list[Path]:
    """Licence-like files at the package root (not below lib/, build/ and so on)."""
    return sorted(p for p in folder.iterdir() if p.is_file() and p.name.lower().startswith(NOTICE_PREFIXES))


def gather_nuget(cache: Path):
    """Resolve every locked package; return (rows, notice blobs); raise Undetermined with all failures."""
    packages = collect_packages()
    missing: list[str] = []
    problems: list[str] = []
    rows = []
    for (name, version), scope in sorted(packages.items(), key=lambda kv: (kv[0][0].lower(), kv[0][1])):
        info = nuspec_info(cache, name, version)
        if info is None:
            missing.append(f"{name} {version}")
            continue
        try:
            spdx, file_text, note = resolve_licence(name, info)
        except KeyError as error:
            problems.append(f"{name} {version}: {error.args[0]}")
            continue
        if scope == "shipped" and (info["dev"] or name in BUILD_TIME_IDS):
            scope = "build-time"
        rows.append({"name": name, "version": version, "scope": scope, "spdx": spdx, "file_text": file_text, "note": note, **info})
    if missing:
        problems.insert(0, f"not in the NuGet cache (restore first): {', '.join(missing)}")
    if problems:
        raise Undetermined("\n".join(f"  - {p}" for p in problems))
    blobs: dict[str, dict] = {}
    for row in rows:
        if row["scope"] != "shipped":
            continue
        for path in package_notice_files(row["folder"]):
            body = read_text(path)
            digest = hashlib.sha256(body.encode("utf-8")).hexdigest()
            blob = blobs.setdefault(digest, {"text": body, "names": set(), "sources": set()})
            blob["names"].add(path.name)
            blob["sources"].add(f"{row['name']} {row['version']}")
    return rows, blobs


def font_sections():
    """Return (family rows, problems) for every directory holding font files."""
    by_dir: dict[Path, list[Path]] = {}
    for path in walk(REPO):
        if path.suffix.lower() in FONT_EXT:
            by_dir.setdefault(path.parent, []).append(path)
    rows, problems = [], []
    for folder in sorted(by_dir, key=lambda p: p.as_posix()):
        rel = folder.relative_to(REPO).as_posix()
        licence_file = folder / "OFL.txt"
        if not licence_file.is_file():
            problems.append(f"font directory without OFL.txt: {rel}")
            continue
        copyright_line = next((ln.strip() for ln in read_text(licence_file).splitlines() if ln.strip().lower().startswith("copyright")), "")
        if not copyright_line:
            problems.append(f"{rel}/OFL.txt has no copyright line")
            continue
        rows.append({"dir": rel, "family": folder.name, "copyright": copyright_line, "files": sorted(p.name for p in by_dir[folder])})
    return rows, problems


def bootstrap_rows():
    """Vendored Bootstrap, read from the banner of its CSS; empty when the library is not vendored."""
    rows = []
    for path in walk(REPO):
        if path.name == "bootstrap.css" and path.parent.name == "css":
            head = path.read_text(encoding="utf-8", errors="replace")[:600]
            version = re.search(r"Bootstrap\s+v([0-9][^\s]*)", head)
            holder = re.search(r"Copyright\s+([^\n]*?)\s*\n", head)
            licence = re.search(r"Licensed under (\w+)", head)
            if not (version and holder and licence and licence.group(1) == "MIT"):
                raise Undetermined(f"  - {path.relative_to(REPO).as_posix()}: banner does not state version, copyright and MIT")
            library = path.parent.parent
            rows.append({"dir": library.relative_to(REPO).as_posix(), "version": version.group(1), "copyright": "Copyright " + holder.group(1).strip()})
    return rows


def vendored_js_rows():
    """Vendored JavaScript libraries: every wwwroot/lib/<name>/VERSION.txt block (name, package, version, licence, licence-file)."""
    rows = []
    for path in walk(REPO):
        if path.name != "VERSION.txt" or path.parent.parent.name != "lib" or path.parent.parent.parent.name != "wwwroot":
            continue
        rel = path.parent.relative_to(REPO).as_posix()
        for block in read_text(path).split("\n\n"):
            fields = dict(re.findall(r"^([a-z-]+):\s*(.+)$", block, flags=re.MULTILINE))
            if not {"name", "package", "version", "licence", "licence-file"} <= fields.keys():
                continue
            if fields["licence"] not in KNOWN_SPDX:
                raise Undetermined(f"  - {rel}/VERSION.txt: licence {fields['licence']} of {fields['package']} has no text in scripts/legal/license-texts")
            licence_file = path.parent / fields["licence-file"]
            if not licence_file.is_file():
                raise Undetermined(f"  - {rel}: licence file {fields['licence-file']} of {fields['package']} is missing")
            copyright_line = next((ln.strip() for ln in read_text(licence_file).splitlines() if ln.strip().lower().startswith("copyright")), "")
            if not copyright_line:
                raise Undetermined(f"  - {rel}/{fields['licence-file']} has no copyright line")
            rows.append({"dir": rel, "name": fields["name"], "package": fields["package"], "version": fields["version"], "licence": fields["licence"], "copyright": copyright_line})
    return rows


def image_rows():
    """Bundled images with the provenance this repository can state."""
    rows = []
    for path in walk(REPO):
        rel = path.relative_to(REPO).as_posix()
        if path.suffix.lower() not in IMAGE_EXT or rel.startswith(("docs/", "tests/", "tools/")):
            continue
        if rel.startswith("assets/"):
            origin = "OmpOS-authored brand artwork; AGPL-3.0-or-later, name and logo reserved (see NOTICE)"
        elif rel.endswith(("wwwroot/favicon.png", "wwwroot/icon-192.png")):
            origin = "provenance not recorded; the file names match the stock Blazor WebAssembly template [INFERENCE]; replace with OmpOS-authored artwork"
        else:
            origin = "provenance not recorded"
        rows.append((rel, hashlib.sha256(path.read_bytes()).hexdigest()[:12], origin))
    return rows


def containerfile_pins():
    """Return {file: {ARG: value}} for ARGs the notices quote."""
    pins = {}
    for path in sorted((REPO / "deploy").glob("*.Containerfile")):
        values = {}
        for line in path.read_text(encoding="utf-8").splitlines():
            m = re.match(r"ARG\s+(BUN_VERSION|OMP_VERSION)=(\S+)", line.strip())
            if m:
                values[m.group(1)] = m.group(2)
        pins[path.relative_to(REPO).as_posix()] = values
    return pins


def submodule_rows():
    rows = []
    gitmodules = REPO / ".gitmodules"
    if gitmodules.is_file():
        current: dict[str, str] = {}
        for line in gitmodules.read_text(encoding="utf-8").splitlines() + ["[end]"]:
            line = line.strip()
            if line.startswith("["):
                if current.get("path"):
                    rows.append(current)
                current = {}
            elif "=" in line:
                key, value = (s.strip() for s in line.split("=", 1))
                current[key] = value
    return sorted(rows, key=lambda r: r["path"])


def npm_section():
    """First-party npm package and its declared dependencies (none today)."""
    path = REPO / "plugins" / "gsd-core-omp" / "package.json"
    if not path.is_file():
        return None
    data = json.loads(path.read_text(encoding="utf-8"))
    deps = {}
    for field in ("dependencies", "devDependencies", "optionalDependencies", "peerDependencies"):
        deps.update(data.get(field, {}))
    if deps:
        raise Undetermined("  - plugins/gsd-core-omp/package.json declares npm dependencies; no licence source is wired for them: " + ", ".join(sorted(deps)))
    return data


def axe_rows():
    return sorted(p.name for p in (REPO / "scripts" / "web-smoke").glob("axe-core-*.sha256")) if (REPO / "scripts" / "web-smoke").is_dir() else []


def render(cache: Path) -> str:
    problems: list[str] = []
    try:
        rows, blobs = gather_nuget(cache)
    except Undetermined as error:
        problems.append(str(error))
        rows, blobs = [], {}
    fonts, font_problems = font_sections()
    problems.extend(f"  - {p}" for p in font_problems)
    try:
        bootstrap = bootstrap_rows()
        vendored = vendored_js_rows()
        npm = npm_section()
    except Undetermined as error:
        problems.append(str(error))
        bootstrap, vendored, npm = [], [], None
    if problems:
        raise Undetermined("\n".join(problems))

    out: list[str] = []
    w = out.append
    w("# Third-party notices")
    w("")
    w("OmpOS is licensed under AGPL-3.0-or-later (see [LICENSE](LICENSE) and [NOTICE](NOTICE)). It uses and conveys the third-party components below, each under its own licence. This file is generated; do not edit it by hand.")
    w("")
    w("Regenerate: `python3 scripts/legal/generate-third-party-notices.py` (check without writing: `--check`). Input: every `packages.lock.json` in the repository, the NuGet cache, the font and image directories, and `deploy/*.Containerfile`.")
    w("")
    w("Scope column: **shipped** reaches users in a desktop build, the Android build, the web payload or the API image; **build-time** is used only while building; **test** is a test-project dependency; **tools** belongs to a spike under `tools/` that is not shipped. A package used by several projects takes the widest scope.")
    w("")

    scopes = {s: [r for r in rows if r["scope"] == s] for s in ("shipped", "build-time", "tools", "test")}
    w("## 1. Summary")
    w("")
    w("| Scope | Packages |")
    w("|---|---|")
    for scope, items in scopes.items():
        w(f"| {scope} | {len(items)} |")
    w("")
    w("| Licence expression | Packages |")
    w("|---|---|")
    counts: dict[str, int] = {}
    for r in rows:
        counts[r["spdx"]] = counts.get(r["spdx"], 0) + 1
    for spdx in sorted(counts):
        w(f"| {spdx} | {counts[spdx]} |")
    w("")
    w("All of these licences are permissive and compatible with AGPL-3.0-or-later (`docs/plans/v0.15.0-research/11-agpl-audit.md`, section 0). Licence data is the `license` element of each package's nuspec as restored on the build host; no network is read.")
    w("")

    w("## 2. NuGet packages")
    w("")
    for scope, title in (("shipped", "2.1 Shipped"), ("build-time", "2.2 Build-time (not conveyed)"), ("tools", "2.3 Spikes under tools/ (not shipped)"), ("test", "2.4 Test-only (not shipped)")):
        items = scopes[scope]
        w(f"### {title}")
        w("")
        if not items:
            w("None.")
            w("")
            continue
        w("| Package | Version | Licence | Copyright | Project |")
        w("|---|---|---|---|---|")
        for r in items:
            holder = r["copyright"] or (f"Authors: {r['authors']}" if r["authors"] else "not stated in the package metadata")
            w(f"| {cell(r['name'])} | {r['version']} | {cell(r['spdx'])} | {cell(holder)} | {cell(r['project_url']) or '-'} |")
        w("")
    file_rows = [r for r in rows if r["file_text"] is not None]
    if file_rows:
        w("### 2.5 Packages whose licence is a file inside the package")
        w("")
        for r in file_rows:
            w(f"#### {r['name']} {r['version']} ({r['spdx']})")
            w("")
            w(r["note"])
            w("")
            open_fence, close_fence = fence(r["file_text"])
            w(open_fence)
            out.append(r["file_text"].rstrip("\n"))
            w(close_fence)
            w("")

    w("## 3. Licence and notice files shipped inside the packages")
    w("")
    w("Files named LICENSE, THIRD-PARTY-NOTICES, NOTICE or COPYING at the root of a **shipped** package, reproduced once per distinct content (SHA-256 prefix). They cover code embedded in the native binaries and managed libraries (for example Skia, HarfBuzz, FreeType, ICU and Oniguruma).")
    w("")
    ordered = sorted(blobs.items(), key=lambda kv: (sorted(kv[1]["names"])[0].lower(), kv[0]))
    for digest, blob in ordered:
        sources = sorted(blob["sources"])
        shown = ", ".join(sources[:6]) + (f" and {len(sources) - 6} more" if len(sources) > 6 else "")
        w(f"### {sorted(blob['names'])[0]} (sha256 {digest[:12]})")
        w("")
        w(f"Carried by: {shown}.")
        w("")
        open_fence, close_fence = fence(blob["text"])
        w(open_fence)
        out.append(blob["text"].rstrip("\n"))
        w(close_fence)
        w("")

    w("## 4. JavaScript and npm")
    w("")
    w("| Item | Version | Licence | Notes |")
    w("|---|---|---|---|")
    if npm is not None:
        w(f"| plugins/gsd-core-omp | {cell(str(npm.get('version', '')))} | {cell(str(npm.get('license', 'not declared')))} | first-party OmpOS code; declares no npm dependencies |")
    for b in bootstrap:
        w(f"| Bootstrap (`{b['dir']}`) | {b['version']} | MIT | {cell(b['copyright'])}; read from the banner of bootstrap.css; MIT text in section 8 |")
    for v in vendored:
        w(f"| {cell(v['name'])} (`{v['dir']}`) | {v['version']} | {v['licence']} | npm package {cell(v['package'])}, vendored unmodified, no CDN; {cell(v['copyright'])}; read from its licence file; MIT text in section 8 |")
    for axe in axe_rows():
        w(f"| axe-core (`scripts/web-smoke/{axe}`) | from the file name | MPL-2.0 | smoke-test tool downloaded at test time with `npm pack`; never committed or shipped, so its text is not reproduced |")
    w("")

    w("## 5. Fonts")
    w("")
    w("Fonts are under the SIL Open Font License 1.1 (text in section 8). Each directory keeps its own `OFL.txt`; the fonts are embedded unmodified and no Reserved Font Name is declared in the copyright lines below.")
    w("")
    w("| Family | Directory | Copyright | Files |")
    w("|---|---|---|---|")
    for f in fonts:
        w(f"| {cell(f['family'])} | `{f['dir']}` | {cell(f['copyright'])} | {cell(', '.join(f['files']))} |")
    w("")

    w("## 6. Images")
    w("")
    images = image_rows()
    if images:
        w("| File | SHA-256 prefix | Provenance |")
        w("|---|---|---|")
        for rel, digest, origin in images:
            w(f"| `{rel}` | {digest} | {cell(origin)} |")
    else:
        w("No bundled image files.")
    w("")

    w("## 7. Components conveyed outside the NuGet graph")
    w("")
    pins = containerfile_pins()
    w("| Component | Where | Licence | Notices |")
    w("|---|---|---|---|")
    for file, values in pins.items():
        bun = values.get("BUN_VERSION")
        omp = values.get("OMP_VERSION")
        if bun:
            w(f"| Bun {bun} | `{file}` final image (`/usr/local/bin/bun`) | MIT, plus statically linked components (JavaScriptCore under LGPL-2, tinycc under LGPL-2.1, zlib-ng, ICU, SQLite and others) | https://bun.sh/docs/project/licensing; the image carries `/licenses/bun-NOTICE.txt`. Bun's source is available at https://github.com/oven-sh/bun/tree/bun-v{bun}. Bun is a separate program that OmpOS does not link. |")
        if omp:
            w(f"| omp (`@oh-my-pi/pi-coding-agent`) {omp} | `{file}` final image (`/opt/omp`) | MIT, Copyright (c) 2025 Mario Zechner, 2025-2026 Can Bölük | the tarball's LICENSE is copied to `/licenses/omp-LICENSE`. omp's own npm dependency tree is resolved at image build time and is not audited here. |")
        w(f"| .NET runtime and ASP.NET Core shared framework | `{file}` base images; desktop and web payloads | MIT, with third-party notices (ICU and others) | the runtime's `LICENSE.txt` and `ThirdPartyNotices.txt` are copied to `/licenses` when the base image provides them (https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT) |")
        w(f"| Ubuntu 24.04 base image packages | `{file}` | per package, see `/usr/share/doc/*/copyright` inside the image | standard image distribution duties |")
    w("")
    w("Submodules under `plugins/` are separate programs pinned by reference; their files are not part of OmpOS source archives.")
    w("")
    w("| Submodule | Repository | Licence |")
    w("|---|---|---|")
    for s in submodule_rows():
        w(f"| `{s['path']}` | {s.get('url', '')} | {SUBMODULE_LICENCES.get(s['path'], 'not recorded')} |")
    w("")

    w("## 8. Licence texts")
    w("")
    w("Templates are given once; the copyright line of each package is in sections 2 and 3. OFL-1.1 is the text of the three font directories.")
    w("")
    for spdx in KNOWN_SPDX + ("OFL-1.1",):
        text = read_text(TEXTS / f"{spdx}.txt")
        w(f"### {spdx}")
        w("")
        open_fence, close_fence = fence(text)
        w(open_fence)
        out.append(text.rstrip("\n"))
        w(close_fence)
        w("")
    return "\n".join(out).rstrip("\n") + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--check", action="store_true", help="exit 1 when the committed file differs from a fresh render")
    parser.add_argument("--output", type=Path, default=REPO / OUTPUT_NAME)
    parser.add_argument("--cache", type=Path, default=Path(os.environ.get("NUGET_PACKAGES") or Path.home() / ".nuget" / "packages"))
    args = parser.parse_args()
    try:
        fresh = render(args.cache)
    except Undetermined as error:
        print("licence could not be determined or an input is missing:\n" + str(error), file=sys.stderr)
        return 2
    data = fresh.encode("utf-8")
    if args.check:
        if not args.output.is_file() or args.output.read_bytes() != data:
            print(f"{args.output.name} is stale; run: python3 scripts/legal/generate-third-party-notices.py", file=sys.stderr)
            return 1
        print(f"{args.output.name} is up to date")
        return 0
    args.output.write_bytes(data)
    print(f"wrote {args.output} ({len(data)} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
