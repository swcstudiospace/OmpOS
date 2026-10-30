#!/usr/bin/env bash
# Installs Google Chrome so scripts/web-smoke/run-smoke.sh finds `google-chrome` on PATH.
# GitHub-hosted ubuntu-24.04 does not ship a Chrome or Chromium binary.
set -euo pipefail

if command -v google-chrome >/dev/null 2>&1; then
  google-chrome --version
  exit 0
fi

if ! command -v sudo >/dev/null 2>&1; then
  echo "error: google-chrome is not on PATH and sudo is not available" >&2
  exit 1
fi

deb="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/google-chrome-stable_current_amd64.deb"
curl -fsSL -o "$deb" https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y "$deb"
google-chrome --version
