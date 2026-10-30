#!/usr/bin/env bash
# Installs the pinned trivy binary used by scripts/ci/scan-images.sh.
# Pin: v0.76.1, Linux-64bit.tar.gz sha256
# c7cbe3137fb8a48a17574bede3dd6beb16742d1e9772929940fc90082c39d9c0
# (read from the release checksums file on 2026-10-30).
set -euo pipefail

version=0.76.1
sha=c7cbe3137fb8a48a17574bede3dd6beb16742d1e9772929940fc90082c39d9c0

if command -v trivy >/dev/null 2>&1 && trivy --version | grep -q "$version"; then
  trivy --version
  exit 0
fi

dest="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/trivy-install"
mkdir -p "$dest"
archive="$dest/trivy.tar.gz"
curl -fsSL -o "$archive" "https://github.com/aquasecurity/trivy/releases/download/v${version}/trivy_${version}_Linux-64bit.tar.gz"
echo "$sha  $archive" | sha256sum -c -
tar -xzf "$archive" -C "$dest" trivy

install_dir="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/autonogrammer-bin"
mkdir -p "$install_dir"
mv "$dest/trivy" "$install_dir/trivy"
chmod 755 "$install_dir/trivy"

if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$install_dir" >> "$GITHUB_PATH"
fi
export PATH="$install_dir:$PATH"
"$install_dir/trivy" --version
