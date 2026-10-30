#!/bin/sh
# Writes one Chromium manifest row. Does not download and does not edit the embedded manifest.
# Usage: pin-chromium.sh <version> <platform> <url> <sha256> <sizeBytes> [out-file]
set -eu

if [ "$#" -lt 5 ] || [ "$#" -gt 6 ]; then
  echo "usage: pin-chromium.sh <version> <platform> <url> <sha256> <sizeBytes> [out-file]" >&2
  exit 2
fi

version=$1
platform=$2
url=$3
sha256=$4
sizeBytes=$5
out=${6:-}

case "$url" in
  https://storage.googleapis.com/*) ;;
  *)
    echo "error: url host must be storage.googleapis.com over https" >&2
    exit 2
    ;;
esac

case "$sha256" in
  [0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f][0-9a-f]) ;;
  *)
    echo "error: sha256 must be 64 lowercase hex characters" >&2
    exit 2
    ;;
esac

case "$sizeBytes" in
  ''|*[!0-9]*)
    echo "error: sizeBytes must be a positive integer" >&2
    exit 2
    ;;
esac

row=$(printf '{"version":"%s","platform":"%s","url":"%s","sha256":"%s","sizeBytes":%s}\n' \
  "$version" "$platform" "$url" "$sha256" "$sizeBytes")

if [ -n "$out" ]; then
  printf '%s\n' "$row" > "$out"
else
  printf '%s\n' "$row"
fi
