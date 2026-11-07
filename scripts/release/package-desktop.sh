#!/usr/bin/env bash
# Packages one self-contained Autonogrammer desktop publish directory into a release archive.
#
# Usage: bash scripts/release/package-desktop.sh [--force] <runtime-identifier> <publish-dir> <version> <output-dir>
#
#   linux-x64   Autonogrammer-<version>-linux-x64.tar.gz   the publish directory under one top-level folder
#   win-x64     Autonogrammer-<version>-win-x64.zip        the publish directory under one top-level folder (7z, else zip)
#   osx-arm64   Autonogrammer-<version>-osx-arm64.zip      an Autonogrammer.app bundle, zipped with ditto --keepParent
#
# <version> is MAJOR.MINOR.PATCH with an optional -prerelease suffix. An existing archive is replaced only with --force.
# The macOS bundle is not signed with a Developer ID and not notarized. Its version keys carry the MAJOR.MINOR.PATCH part only, because
# CFBundleShortVersionString is a dotted-number string. The whole publish directory goes into Contents/MacOS, because a
# self-contained app loads its runtime from beside its executable.
# Prints the absolute archive path as the last line of stdout. Everything else goes to stderr.
set -euo pipefail

usage() {
  echo "usage: bash scripts/release/package-desktop.sh [--force] <runtime-identifier> <publish-dir> <version> <output-dir>" >&2
  exit 2
}

die() {
  echo "error: $*" >&2
  exit 1
}

force=0
while [ "$#" -gt 0 ]; do
  case "$1" in
    --force)
      force=1
      shift
      ;;
    -*)
      echo "error: unknown option: $1" >&2
      usage
      ;;
    *)
      break
      ;;
  esac
done

[ "$#" -eq 4 ] || { echo "error: expected 4 arguments after options, got $#" >&2; usage; }
rid="$1"
publish="$2"
version="$3"
out_dir="$4"

for value in "$rid" "$publish" "$version" "$out_dir"; do
  [ -n "$value" ] || die "an argument is empty"
done

case "$rid" in
  linux-x64 | osx-arm64) exe="Autonogrammer.Native" ;;
  win-x64) exe="Autonogrammer.Native.exe" ;;
  *) die "unsupported runtime identifier '$rid' (expected linux-x64, win-x64 or osx-arm64)" ;;
esac

if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  die "version '$version' is not MAJOR.MINOR.PATCH[-prerelease]"
fi
short="${version%%-*}"

[ -d "$publish" ] || die "publish directory not found: $publish"
[ -f "$publish/$exe" ] || die "$publish/$exe not found; publish $rid first"

packer=""
case "$rid" in
  linux-x64)
    command -v tar >/dev/null 2>&1 || die "tar is required"
    ;;
  win-x64)
    if command -v 7z >/dev/null 2>&1; then
      packer="7z"
    elif command -v zip >/dev/null 2>&1; then
      packer="zip"
    else
      die "neither 7z nor zip is on PATH"
    fi
    ;;
  osx-arm64)
    command -v ditto >/dev/null 2>&1 || die "ditto is required for osx-arm64 (it ships with macOS)"
    ;;
esac

base="Autonogrammer-${version}-${rid}"
case "$rid" in
  linux-x64) name="$base.tar.gz" ;;
  *) name="$base.zip" ;;
esac

mkdir -p "$out_dir"
out_abs="$(cd "$out_dir" && pwd)"
archive="$out_abs/$name"
partial="$out_abs/.partial-$name"

if [ -e "$archive" ] && [ "$force" -ne 1 ]; then
  die "$archive already exists; pass --force to replace it"
fi

stage="$(mktemp -d)"
cleanup() {
  rm -rf -- "${stage:-}"
  rm -f -- "${partial:-}"
}
trap cleanup EXIT

case "$rid" in
  osx-arm64)
    bundle="$stage/Autonogrammer.app"
    mkdir -p "$bundle/Contents/MacOS"
    cp -a "$publish/." "$bundle/Contents/MacOS/"
    cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>$exe</string>
  <key>CFBundleIdentifier</key>
  <string>com.swcstudiospace.autonogrammer</string>
  <key>CFBundleName</key>
  <string>Autonogrammer</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$short</string>
  <key>CFBundleVersion</key>
  <string>$short</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
</dict>
</plist>
PLIST
    (cd "$stage" && ditto -c -k --keepParent Autonogrammer.app "$partial")
    ;;
  *)
    mkdir -p "$stage/$base"
    cp -a "$publish/." "$stage/$base/"
    ;;
esac

case "$rid" in
  linux-x64)
    tar -C "$stage" -czf "$partial" "$base"
    ;;
  win-x64)
    if [ "$packer" = "7z" ]; then
      (cd "$stage" && 7z a -tzip -y -bd -bso0 "$partial" "$base" >/dev/null)
    else
      (cd "$stage" && zip -q -r "$partial" "$base")
    fi
    ;;
esac

mv -f -- "$partial" "$archive"
printf '%s\n' "$archive"
