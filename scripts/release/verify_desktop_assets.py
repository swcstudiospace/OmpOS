#!/usr/bin/env python3
"""Verify the Autonogrammer desktop release files in one directory and optionally write SHA256SUMS-desktop.

Usage:
    python3 scripts/release/verify_desktop_assets.py --dir <dist-dir> --version <VERSION>
        [--platform all|linux-x64|win-x64|osx-arm64] [--write-sums <path>]

VERSION is MAJOR.MINOR.PATCH with an optional prerelease (the tag without its leading "v"). The expected files are
Autonogrammer-<VERSION>-<rid>.<ext> for the platforms selected. The check fails when a file is missing, empty, of a
different version, unexpected, too small to be a real installer, or does not start (or, for the .dmg, end) with the magic
bytes of its format. --write-sums writes a `sha256sum -c` compatible file for the verified files and re-reads it.

Exit codes: 0 verified, 1 a problem was found (one `::error::` line each), 2 bad usage. Stdlib only, no network.
"""
import argparse
import hashlib
import os
import re
import sys

VERSION_RE = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$")
PLATFORMS = ("linux-x64", "win-x64", "osx-arm64")
SUMS_NAME = "SHA256SUMS-desktop"
ONE_MIB = 1024 * 1024
DMG_TRAILER_SIZE = 512

# (platform, file-name suffix after the version, kind, the size must exceed this many bytes)
FILE_SPECS = (
    ("linux-x64", "linux-x64.tar.gz", "tar.gz", 0),
    ("linux-x64", "linux-x64.deb", "deb", 0),
    ("win-x64", "win-x64.zip", "zip", 0),
    ("win-x64", "win-x64-Setup.exe", "exe", ONE_MIB),
    ("osx-arm64", "osx-arm64.zip", "zip", 0),
    ("osx-arm64", "osx-arm64.dmg", "dmg", ONE_MIB),
)
PREFIX = "Autonogrammer-"


def expected_files(version, platform):
    """Return {file name: (kind, size must exceed)} for the selected platform ("all" or one RID)."""
    return {
        f"{PREFIX}{version}-{suffix}": (kind, min_size)
        for rid, suffix, kind, min_size in FILE_SPECS
        if platform in ("all", rid)
    }


def name_version(name):
    """Return the version in a name built from a known file-name template (Autonogrammer-<version>-<suffix>), else None.

    The suffix is matched exactly at the end of the name and the rest must be a whole valid version, so a prerelease
    pattern cannot swallow part of the suffix."""
    if not name.startswith(PREFIX):
        return None
    for _rid, suffix, _kind, _min in FILE_SPECS:
        tail = "-" + suffix
        if name.endswith(tail):
            middle = name[len(PREFIX):-len(tail)]
            if VERSION_RE.fullmatch(middle):
                return middle
    return None


def human_size(n):
    value = float(n)
    for unit in ("B", "KiB", "MiB"):
        if value < 1024:
            return f"{int(value)} {unit}" if unit == "B" else f"{value:.2f} {unit}"
        value /= 1024
    return f"{value:.2f} GiB"


def sha256_of(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def magic_problem(path, kind, size):
    """Return a description of the magic-number failure for a non-empty file, or None when it passes."""
    with open(path, "rb") as handle:
        head = handle.read(8 + 13)
        if kind == "zip":
            return None if head.startswith(b"PK") else "does not start with the zip signature PK"
        if kind == "exe":
            return None if head.startswith(b"MZ") else "does not start with the PE signature MZ"
        if kind == "tar.gz":
            return None if head.startswith(b"\x1f\x8b") else "does not start with the gzip bytes 1f 8b"
        if kind == "deb":
            if head[:8] != b"!<arch>\n":
                return "does not start with the ar header !<arch>"
            if head[8:8 + 13] != b"debian-binary":
                return "has no debian-binary member after the ar header"
            return None
        if kind == "dmg":
            if size < DMG_TRAILER_SIZE:
                return "is smaller than the 512-byte UDIF trailer"
            handle.seek(size - DMG_TRAILER_SIZE)
            return None if handle.read(4) == b"koly" else "does not end with the UDIF trailer (koly)"
    return f"has unknown kind {kind}"


def verify(directory, version, platform):
    """Return (problems, rows). rows is [(name, size)] for every expected file that passed every check."""
    problems = []
    rows = []
    if not os.path.isdir(directory):
        return [f"{directory} is not a directory"], rows
    expected = expected_files(version, platform)
    present = sorted(os.listdir(directory))
    for name in present:
        if name.startswith(".") or name == SUMS_NAME or name in expected:
            continue
        found = name_version(name)
        if found is not None and found != version:
            problems.append(f"{name} carries version {found} but the release version is {version}")
        else:
            problems.append(f"unexpected file {name} in {directory}")
    for name in sorted(expected):
        kind, min_size = expected[name]
        path = os.path.join(directory, name)
        if not os.path.lexists(path):
            problems.append(f"missing {name} in {directory}")
            continue
        if not os.path.isfile(path):
            problems.append(f"{name} is not a regular file")
            continue
        size = os.path.getsize(path)
        if size == 0:
            problems.append(f"{name} is empty")
            continue
        found = magic_problem(path, kind, size)
        if found is not None:
            problems.append(f"{name} {found}")
            continue
        if min_size and size <= min_size:
            problems.append(f"{name} is only {size} bytes; an installer or disk image must be larger than {human_size(min_size)}")
            continue
        rows.append((name, size))
    return problems, rows


def write_sums(directory, names, sums_path):
    lines = "".join(f"{sha256_of(os.path.join(directory, name))}  {name}\n" for name in sorted(names))
    with open(sums_path, "wb") as handle:
        handle.write(lines.encode("utf-8"))


def check_sums(directory, names, sums_path):
    """Re-read sums_path like `sha256sum -c`: return a list of problems (empty when every line verifies)."""
    problems = []
    seen = []
    with open(sums_path, "rb") as handle:
        text = handle.read().decode("utf-8")
    for number, line in enumerate(text.splitlines(), start=1):
        match = re.fullmatch(r"([0-9a-f]{64})  (\S+)", line)
        if match is None:
            problems.append(f"{sums_path} line {number} is not in sha256sum format")
            continue
        digest, name = match.groups()
        seen.append(name)
        path = os.path.join(directory, name)
        if not os.path.isfile(path):
            problems.append(f"{sums_path} lists {name}, which is not in {directory}")
        elif sha256_of(path) != digest:
            problems.append(f"{name} does not match its SHA-256 in {sums_path}")
    if sorted(seen) != sorted(names):
        problems.append(f"{sums_path} lists {', '.join(sorted(seen)) or 'nothing'} instead of {', '.join(sorted(names))}")
    return problems


def report(problems):
    for problem in problems:
        print(f"::error::verify_desktop_assets: {problem}")


def build_parser():
    parser = argparse.ArgumentParser(
        prog="verify_desktop_assets.py",
        description="Verify the Autonogrammer desktop release files and optionally write SHA256SUMS-desktop.",
    )
    parser.add_argument("--dir", required=True, dest="directory", help="directory holding the downloaded release files")
    parser.add_argument("--version", required=True, help="MAJOR.MINOR.PATCH[-prerelease], the tag without the leading v")
    parser.add_argument("--platform", default="all", choices=("all",) + PLATFORMS, help="platform whose files are expected")
    parser.add_argument("--write-sums", default=None, dest="sums", metavar="PATH", help="write a sha256sum -c file here")
    return parser


def main(argv=None):
    parser = build_parser()
    args = parser.parse_args(argv)  # argparse exits 2 with the usage text on bad usage
    if not VERSION_RE.match(args.version):
        parser.error(f"invalid --version {args.version!r}: expected MAJOR.MINOR.PATCH[-prerelease]")

    problems, rows = verify(args.directory, args.version, args.platform)
    if problems:
        report(problems)
        return 1

    names = [name for name, _size in rows]
    if args.sums is not None:
        write_sums(args.directory, names, args.sums)
        problems = check_sums(args.directory, names, args.sums)
        if problems:
            report(problems)
            return 1

    width = max(len(name) for name in names)
    print(f"verify_desktop_assets: {len(rows)} files verified for version {args.version} (platform {args.platform})")
    for name, size in rows:
        print(f"  {name.ljust(width)}  {human_size(size):>10}  {size} bytes")
    if args.sums is not None:
        print(f"verify_desktop_assets: wrote and re-verified {args.sums}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
