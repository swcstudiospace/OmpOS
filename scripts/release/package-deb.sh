#!/usr/bin/env bash
# Packages one self-contained linux-x64 Autonogrammer publish directory into a Debian package.
#
# Usage: bash scripts/release/package-deb.sh <version> <publish-dir> <out-dir>
#
#   <version>      MAJOR.MINOR.PATCH with an optional -prerelease suffix. The Debian version is the same string with the first '-'
#                  replaced by '~', so a prerelease sorts before its release.
#   <publish-dir>  the `dotnet publish` output for linux-x64; it must contain the Autonogrammer.Native executable.
#   <out-dir>      receives Autonogrammer-<version>-linux-x64.deb. An existing file there is refused; remove it first.
#
# Installed layout:
#   /opt/autonogrammer/                                        the whole publish directory
#   /usr/bin/autonogrammer                                     packaging/linux/autonogrammer.in
#   /usr/share/applications/autonogrammer.desktop              packaging/linux/autonogrammer.desktop
#   /usr/share/icons/hicolor/scalable/apps/autonogrammer.svg   assets/brand/autonogrammer-mark.svg
#   /usr/share/doc/autonogrammer/copyright
#
# Honours SOURCE_DATE_EPOCH: when set, every staged path gets that mtime.
# Prints the absolute .deb path as the last line of stdout. Everything else goes to stderr.
set -euo pipefail

usage() {
  echo "usage: bash scripts/release/package-deb.sh <version> <publish-dir> <out-dir>" >&2
  exit 2
}

die() {
  echo "error: $*" >&2
  exit 1
}

# Depends: only the system libraries the publish loads. Evidence is from readelf -d and ldd on the release binaries, plus the
# library names that the managed and native binaries reference (dlopen / DllImport), mapped on Ubuntu 24.04 with dpkg -S.
#   libc6 (>= 2.34)  NEEDED libc.so.6, libm.so.6, libdl.so.2, libpthread.so.0, librt.so.1, libutil.so.1. glibc 2.34 merged the last
#                    four into libc6. Highest GLIBC symbol version used by the binaries: 2.27.
#   libstdc++6       NEEDED libstdc++.so.6 (Autonogrammer.Native, libcoreclr.so and others).
#   libgcc-s1        NEEDED libgcc_s.so.1 (same binaries).
#   libfontconfig1   NEEDED by libSkiaSharp.so.
#   libx11-6, libxext6, libxi6, libxcursor1, libxrandr2, libxfixes3
#                    names loaded by Avalonia.X11.dll, the X11 windowing backend.
#   libgl1           libGL.so.1, loaded by Avalonia.X11.dll and referenced by libSkiaSharp.so.
#   libicu74         libicuuc and libicui18n are loaded by libSystem.Globalization.Native.so. The runtime config does not set
#                    InvariantGlobalization.
#   libssl3t64       libssl.so.3 and libcrypto.so.3 are loaded by libSystem.Security.Cryptography.Native.OpenSsl.so (TLS).
# Not listed: libraries that only optional features load (libsecret, GTK, WebKit/WPE, AT-SPI, XTest for computer use, LTTng
# tracing, Kerberos, Vulkan, GLib).
DEPENDS='libc6 (>= 2.34), libstdc++6, libgcc-s1, libfontconfig1, libx11-6, libxext6, libxi6, libxcursor1, libxrandr2, libxfixes3, libgl1, libicu74, libssl3t64'

[ "$#" -eq 3 ] || { echo "error: expected 3 arguments, got $#" >&2; usage; }
version="$1"
publish="$2"
out_dir="$3"

for value in "$version" "$publish" "$out_dir"; do
  [ -n "$value" ] || die "an argument is empty"
done

# Fail closed: only MAJOR.MINOR.PATCH with an optional prerelease of dot-separated alphanumerics. No '-' inside the prerelease,
# so the only '-' is the separator that becomes '~'.
if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.]+)?$ ]]; then
  die "version '$version' is not MAJOR.MINOR.PATCH[-prerelease]"
fi
deb_version="${version/-/'~'}"

command -v dpkg-deb >/dev/null 2>&1 || die "dpkg-deb is required"

[ -d "$publish" ] || die "publish directory not found: $publish"
publish="$(cd "$publish" && pwd -P)"

exe_path="$publish/Autonogrammer.Native"
[ -f "$exe_path" ] || die "$exe_path not found; publish linux-x64 first"
magic="$(head -c 4 -- "$exe_path" | od -An -tx1 | tr -d ' \n')"
[ "$magic" = "7f454c46" ] || die "$exe_path is not an ELF executable"

bad_entry="$(find "$publish" ! -type f ! -type d ! -type l -print -quit)"
[ -z "$bad_entry" ] || die "unsupported file type in publish directory: $bad_entry"

while IFS= read -r -d '' link; do
  target="$(readlink -f -- "$link")"
  case "$target" in
    "$publish" | "$publish"/*) ;;
    *) die "symlink $link points outside the publish directory ($target)" ;;
  esac
done < <(find "$publish" -type l -print0)

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$script_dir/../.." && pwd)"
launcher_src="$repo/packaging/linux/autonogrammer.in"
desktop_src="$repo/packaging/linux/autonogrammer.desktop"
icon_src="$repo/assets/brand/autonogrammer-mark.svg"
props="$repo/Directory.Build.props"
for input in "$launcher_src" "$desktop_src" "$icon_src" "$props"; do
  [ -f "$input" ] || die "missing packaging input: $input"
done

# The launcher and desktop entry must carry the lines this package relies on.
grep -qxF -- 'exec /opt/autonogrammer/Autonogrammer.Native "$@"' "$launcher_src" || die "$launcher_src does not exec the installed executable"
for line in 'Type=Application' 'Name=Autonogrammer' 'Exec=autonogrammer' 'Icon=autonogrammer' 'Terminal=false' 'Categories=Development;'; do
  grep -qxF -- "$line" "$desktop_src" || die "$desktop_src lacks '$line'"
done
grep -q '<svg' "$icon_src" || die "$icon_src is not an SVG"

props_value() {
  local value
  value="$(sed -n "s|.*<$1>\(.*\)</$1>.*|\1|p" "$props" | head -n 1)"
  [ -n "$value" ] || die "$1 is not set in Directory.Build.props"
  printf '%s' "$value"
}
copyright_line="$(props_value Copyright)"
homepage="$(props_value PackageProjectUrl)"
license="$(props_value PackageLicenseExpression)"

if [ -n "${SOURCE_DATE_EPOCH:-}" ]; then
  [[ "$SOURCE_DATE_EPOCH" =~ ^[0-9]+$ ]] || die "SOURCE_DATE_EPOCH is not an integer: $SOURCE_DATE_EPOCH"
fi

mkdir -p -- "$out_dir"
out_abs="$(cd "$out_dir" && pwd -P)"
name="Autonogrammer-${version}-linux-x64.deb"
archive="$out_abs/$name"
partial="$out_abs/.partial-$name"
[ ! -e "$archive" ] || die "$archive already exists; remove it first"

work=""
cleanup() {
  rm -rf -- "${work:-}"
  rm -f -- "${partial:-}"
}
trap cleanup EXIT
work="$(mktemp -d)"
pkg="$work/pkg"

mkdir -p -- "$pkg/DEBIAN" "$pkg/opt/autonogrammer" "$pkg/usr/bin" "$pkg/usr/share/applications" \
  "$pkg/usr/share/icons/hicolor/scalable/apps" "$pkg/usr/share/doc/autonogrammer"
cp -a -- "$publish/." "$pkg/opt/autonogrammer/"

find "$pkg/opt/autonogrammer" -type d -exec chmod 0755 {} +
find "$pkg/opt/autonogrammer" -type f -exec chmod 0644 {} +
chmod 0755 "$pkg/opt/autonogrammer/Autonogrammer.Native"
if [ -f "$pkg/opt/autonogrammer/createdump" ]; then
  chmod 0755 "$pkg/opt/autonogrammer/createdump"
fi

install -m 0755 -- "$launcher_src" "$pkg/usr/bin/autonogrammer"
install -m 0644 -- "$desktop_src" "$pkg/usr/share/applications/autonogrammer.desktop"
install -m 0644 -- "$icon_src" "$pkg/usr/share/icons/hicolor/scalable/apps/autonogrammer.svg"

cat > "$pkg/usr/share/doc/autonogrammer/copyright" <<EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: Autonogrammer
Source: $homepage

Files: *
Copyright: $copyright_line
License: $license

License: $license
 This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public
 License as published by the Free Software Foundation, version 3, or (at your option) any later version.
 .
 The full licence text is the LICENSE file in the source repository: $homepage
 .
 This package does not include omp or Bun.
EOF

installed_kib="$(du -sk -- "$pkg/opt" "$pkg/usr" | awk '{ total += $1 } END { print total }')"
cat > "$pkg/DEBIAN/control" <<EOF
Package: autonogrammer
Version: $deb_version
Architecture: amd64
Maintainer: SWC Studio
Section: utils
Priority: optional
Homepage: $homepage
Installed-Size: $installed_kib
Depends: $DEPENDS
Description: Autonogrammer desktop application
 Autonogrammer desktop app with its own .NET runtime. The package does not include omp or Bun.
EOF

find "$pkg" -type d -exec chmod 0755 {} +
chmod 0644 "$pkg/DEBIAN/control" "$pkg/usr/share/doc/autonogrammer/copyright" \
  "$pkg/usr/share/applications/autonogrammer.desktop" "$pkg/usr/share/icons/hicolor/scalable/apps/autonogrammer.svg"
chmod 0755 "$pkg/usr/bin/autonogrammer"

if [ -n "${SOURCE_DATE_EPOCH:-}" ]; then
  find "$pkg" -exec touch -h -d "@$SOURCE_DATE_EPOCH" {} +
fi

dpkg-deb --root-owner-group --build "$pkg" "$partial" >&2
mv -f -- "$partial" "$archive"
printf '%s\n' "$archive"
