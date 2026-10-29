#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 SWC Studio
"""Generate docs/legal/sbom.spdx.json, an SPDX 2.3 SBOM of the .NET dependency graph (stdlib only, offline, deterministic).

Inputs: every packages.lock.json (resolved versions, per-project DEPENDS_ON), the NuGet cache nuspec files for the licence
expression (the same source as generate-third-party-notices.py), and the Bun and omp pins in deploy/*.Containerfile.
Determinism: all lists are sorted; --check ignores the creation time; created is SOURCE_DATE_EPOCH, else the HEAD commit time, else the epoch.

Usage:
  python3 scripts/legal/generate-sbom.py            write docs/legal/sbom.spdx.json
  python3 scripts/legal/generate-sbom.py --check    exit 1 when the committed file is stale
  options: --output PATH, --cache DIR (default $NUGET_PACKAGES or ~/.nuget/packages), --root DIR (default repository)
A package whose licence cannot be read from the cache gets NOASSERTION (never a guess). Exit 2 on invalid output or inputs.
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUTPUT = Path("docs/legal/sbom.spdx.json")
SKIP_DIRS = {".git", "bin", "obj", "node_modules", ".planning", ".swarm", ".dotnet", ".nuget", "plugins"}
NOASSERTION = "NOASSERTION"
SPDX_ID_RE = re.compile(r"^SPDXRef-[A-Za-z0-9.\-]+$")


def spdx_id(text: str) -> str:
    """Return a valid SPDXRef identifier for text."""
    return "SPDXRef-" + re.sub(r"[^A-Za-z0-9.\-]+", "-", text).strip("-")


def lock_files(root: Path) -> list[Path]:
    """Every packages.lock.json under root, sorted by relative path."""
    found = []
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS)
        if "packages.lock.json" in filenames:
            found.append(Path(dirpath) / "packages.lock.json")
    return sorted(found, key=lambda p: p.relative_to(root).as_posix())


def nuspec_licence(cache: Path, name: str, version: str) -> str:
    """SPDX expression from the cached nuspec, or NOASSERTION."""
    spec = cache / name.lower() / version / f"{name.lower()}.nuspec"
    if not spec.is_file():
        return NOASSERTION
    root = ET.fromstring(spec.read_bytes().decode("utf-8-sig"))
    for meta in root:
        if meta.tag.endswith("metadata"):
            for el in meta:
                if el.tag.split("}")[-1] == "license" and el.get("type") == "expression" and (el.text or "").strip():
                    return el.text.strip()
    return NOASSERTION


def container_pins(root: Path) -> dict[str, str]:
    """Read BUN_VERSION and OMP_VERSION (ARG defaults) from deploy/*.Containerfile; names must agree across files."""
    pins: dict[str, str] = {}
    for path in sorted((root / "deploy").glob("*.Containerfile")):
        for line in path.read_text(encoding="utf-8").splitlines():
            m = re.match(r"ARG (BUN_VERSION|OMP_VERSION)=(\S+)", line)
            if m:
                if pins.get(m.group(1), m.group(2)) != m.group(2):
                    raise SystemExit(f"error: {m.group(1)} differs between Containerfiles")
                pins[m.group(1)] = m.group(2)
    return pins


def created_timestamp(root: Path) -> str:
    """SOURCE_DATE_EPOCH, else the HEAD commit time, else the Unix epoch, as UTC ISO-8601 with Z."""
    epoch = os.environ.get("SOURCE_DATE_EPOCH")
    if not epoch:
        try:
            epoch = subprocess.run(["git", "log", "-1", "--format=%ct"], cwd=root, check=True, capture_output=True, text=True).stdout.strip()
        except (OSError, subprocess.CalledProcessError):
            epoch = "0"
    moment = datetime.datetime.fromtimestamp(int(epoch or "0"), datetime.timezone.utc)
    return moment.strftime("%Y-%m-%dT%H:%M:%SZ")


def build_sbom(root: Path, cache: Path, created: str, name: str = "Autonogrammer") -> dict:
    """Build the SPDX document from lock files under root."""
    packages: dict[str, dict] = {}
    relationships: set[tuple[str, str, str]] = set()
    projects: dict[str, str] = {}
    for lock in lock_files(root):
        rel = lock.parent.relative_to(root).as_posix() or "."
        pid = spdx_id("Project-" + rel)
        projects[rel] = pid
        data = json.loads(lock.read_text(encoding="utf-8"))
        for deps in data.get("dependencies", {}).values():
            for dep, info in deps.items():
                if info.get("type") == "Project" or "resolved" not in info:
                    continue
                version = info["resolved"]
                did = spdx_id(f"Package-{dep}-{version}")
                if did not in packages:
                    packages[did] = {
                        "SPDXID": did,
                        "name": dep,
                        "versionInfo": version,
                        "downloadLocation": f"https://www.nuget.org/api/v2/package/{dep}/{version}",
                        "filesAnalyzed": False,
                        "licenseConcluded": NOASSERTION,
                        "licenseDeclared": nuspec_licence(cache, dep, version),
                        "copyrightText": NOASSERTION,
                        "externalRefs": [{"referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": f"pkg:nuget/{dep}@{version}"}],
                    }
                relationships.add((pid, "DEPENDS_ON", did))
    for rel, pid in projects.items():
        packages[pid] = {
            "SPDXID": pid, "name": rel, "downloadLocation": NOASSERTION, "filesAnalyzed": False,
            "licenseConcluded": NOASSERTION, "licenseDeclared": "AGPL-3.0-or-later", "copyrightText": NOASSERTION,
        }
    pins = container_pins(root)
    components = (
        ("BUN_VERSION", "bun", "https://github.com/oven-sh/bun/releases/download/bun-v{v}/bun-linux-x64.zip", "pkg:github/oven-sh/bun@bun-v{v}", "MIT"),
        ("OMP_VERSION", "@oh-my-pi/pi-coding-agent", "https://registry.npmjs.org/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-{v}.tgz", "pkg:npm/%40oh-my-pi/pi-coding-agent@{v}", NOASSERTION),
    )
    for key, cname, url, purl, lic in components:
        if key not in pins:
            continue
        v = pins[key]
        cid = spdx_id(f"Package-{cname}-{v}")
        packages[cid] = {
            "SPDXID": cid, "name": cname, "versionInfo": v, "downloadLocation": url.format(v=v), "filesAnalyzed": False,
            "licenseConcluded": NOASSERTION, "licenseDeclared": lic, "copyrightText": NOASSERTION,
            "externalRefs": [{"referenceCategory": "PACKAGE-MANAGER", "referenceType": "purl", "referenceLocator": purl.format(v=v)}],
        }
        for rel, pid in projects.items():
            if rel.startswith("services/Autonogrammer.Api") or rel.startswith("services/Autonogrammer.AgentWorker"):
                relationships.add((pid, "DEPENDS_ON", cid))
    root_id = "SPDXRef-DOCUMENT"
    rels = [{"spdxElementId": root_id, "relationshipType": "DESCRIBES", "relatedSpdxElement": pid} for pid in sorted(projects.values())]
    rels += [{"spdxElementId": a, "relationshipType": r, "relatedSpdxElement": b} for a, r, b in sorted(relationships)]
    return {
        "spdxVersion": "SPDX-2.3",
        "dataLicense": "CC0-1.0",
        "SPDXID": root_id,
        "name": name,
        "documentNamespace": "https://github.com/swcstudiospace/OmpOS/sbom/" + namespace_digest(packages, rels),
        "creationInfo": {"created": created, "creators": ["Tool: generate-sbom.py"]},
        "packages": [packages[k] for k in sorted(packages)],
        "relationships": rels,
    }


def namespace_digest(packages: dict[str, dict], rels: list[dict]) -> str:
    """Content hash used in the document namespace, so it changes only when the dependency graph changes."""
    blob = json.dumps([packages, rels], sort_keys=True).encode("utf-8")
    return hashlib.sha256(blob).hexdigest()


def comparable(text: str) -> dict:
    """Parse an SBOM and drop the creation time: --check compares content, not when it was generated."""
    doc = json.loads(text)
    doc.get("creationInfo", {}).pop("created", None)
    return doc


def validate(doc: dict) -> list[str]:
    """Structural validation of the SPDX 2.3 required fields; returns the list of problems."""
    errors: list[str] = []
    for field in ("spdxVersion", "dataLicense", "SPDXID", "name", "documentNamespace", "creationInfo", "packages", "relationships"):
        if field not in doc:
            errors.append(f"document missing {field}")
    if doc.get("spdxVersion") != "SPDX-2.3":
        errors.append("spdxVersion must be SPDX-2.3")
    if doc.get("dataLicense") != "CC0-1.0":
        errors.append("dataLicense must be CC0-1.0")
    info = doc.get("creationInfo", {})
    if not re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", str(info.get("created", ""))) or not info.get("creators"):
        errors.append("creationInfo needs created (YYYY-MM-DDThh:mm:ssZ) and creators")
    ids = set()
    for pkg in doc.get("packages", []):
        pid = pkg.get("SPDXID", "")
        if not SPDX_ID_RE.match(pid):
            errors.append(f"bad SPDXID {pid!r}")
        if pid in ids:
            errors.append(f"duplicate SPDXID {pid}")
        ids.add(pid)
        for field in ("name", "downloadLocation", "filesAnalyzed", "licenseConcluded", "licenseDeclared", "copyrightText"):
            if field not in pkg:
                errors.append(f"{pid} missing {field}")
    ids.add("SPDXRef-DOCUMENT")
    for rel in doc.get("relationships", []):
        for end in ("spdxElementId", "relatedSpdxElement"):
            if rel.get(end) not in ids:
                errors.append(f"relationship references unknown {rel.get(end)!r}")
        if not rel.get("relationshipType"):
            errors.append("relationship without type")
    return errors


def render(doc: dict) -> str:
    """Stable JSON text with a trailing newline."""
    return json.dumps(doc, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def main(argv: list[str] | None = None) -> int:
    """CLI entry point."""
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--output", type=Path)
    ap.add_argument("--root", type=Path, default=REPO)
    ap.add_argument("--cache", type=Path, default=Path(os.environ.get("NUGET_PACKAGES") or Path.home() / ".nuget" / "packages"))
    args = ap.parse_args(argv)
    root = args.root.resolve()
    output = args.output or root / OUTPUT
    doc = build_sbom(root, args.cache, created_timestamp(root))
    problems = validate(doc)
    if problems:
        print("\n".join(problems), file=sys.stderr)
        return 2
    text = render(doc)
    if args.check:
        if not output.is_file() or comparable(output.read_text(encoding="utf-8")) != comparable(text):
            print(f"error: {output} is stale; run python3 scripts/legal/generate-sbom.py", file=sys.stderr)
            return 1
        print("sbom: ok")
        return 0
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8")
    print(f"wrote {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
