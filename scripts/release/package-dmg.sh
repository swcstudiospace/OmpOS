#!/usr/bin/env bash
# Packages one built Autonogrammer.app bundle into a compressed macOS disk image (osx-arm64).
#
# Usage: bash scripts/release/package-dmg.sh <version> <app-dir> <output-dir>
#
#   <version>     MAJOR.MINOR.PATCH with an optional -prerelease suffix (the rule package-desktop.sh uses).
#   <app-dir>     The Autonogrammer.app bundle. It must contain Contents/Info.plist and Contents/MacOS/Autonogrammer.Native.
#   <output-dir>  Receives Autonogrammer-<version>-osx-arm64.dmg. An existing image of that name is replaced.
#
# The image holds a copy of the app and a link to /Applications. It is UDZO-compressed and checked with hdiutil verify before it
# gets its final name. The app is ad-hoc signed by package-desktop.sh (identity "-", not a Developer ID signature, no hardened runtime,
# not notarized). This script checks, with verify_bundle_signature from macos-adhoc-sign.sh (no codesign --deep, see that file), that
# the signatures are still intact on the copy it puts in the image.
# Prints the absolute image path as the last line of stdout. Everything else goes to stderr.
# hdiutil exists only on macOS, so this script cannot run elsewhere.
set -euo pipefail

usage() {
  echo "usage: bash scripts/release/package-dmg.sh <version> <app-dir> <output-dir>" >&2
  exit 2
}

die() {
  echo "error: $*" >&2
  exit 1
}

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

[ "$#" -eq 3 ] || { echo "error: expected 3 arguments, got $#" >&2; usage; }
version="$1"
app="$2"
out_dir="$3"

for value in "$version" "$app" "$out_dir"; do
  [ -n "$value" ] || die "an argument is empty"
done

if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  die "version '$version' is not MAJOR.MINOR.PATCH[-prerelease]"
fi

app="${app%/}"
case "$app" in
  *.app) ;;
  *) die "app directory '$app' is not an .app bundle" ;;
esac
[ -d "$app" ] || die "app bundle not found: $app"
[ -f "$app/Contents/Info.plist" ] || die "$app has no Contents/Info.plist"
[ -f "$app/Contents/MacOS/Autonogrammer.Native" ] || die "$app has no Contents/MacOS/Autonogrammer.Native"

command -v hdiutil >/dev/null 2>&1 || die "hdiutil is required and ships only with macOS"
command -v codesign >/dev/null 2>&1 || die "codesign is required and ships only with macOS"

mkdir -p "$out_dir"
out_abs="$(cd "$out_dir" && pwd)"
name="Autonogrammer-${version}-osx-arm64.dmg"
final="$out_abs/$name"
partial="$out_abs/.partial-$name"

stage="$(mktemp -d)"
cleanup() {
  rm -rf -- "${stage:-}"
  rm -f -- "${partial:-}"
}
trap cleanup EXIT

ditto "$app" "$stage/Autonogrammer.app"
# shellcheck source=scripts/release/macos-adhoc-sign.sh
source "$script_dir/macos-adhoc-sign.sh"
verify_bundle_signature "$stage/Autonogrammer.app" || die "the ad-hoc signature of the app did not survive the copy (package-desktop.sh signs it before zipping)"
ln -s /Applications "$stage/Applications"

hdiutil create -volname "Autonogrammer ${version}" -srcfolder "$stage" -ov -format UDZO "$partial" >&2
hdiutil verify "$partial" >&2

mv -f -- "$partial" "$final"
printf '%s\n' "$final"
