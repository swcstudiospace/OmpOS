#!/usr/bin/env bash
# OmpOS one-command bootstrap (Phase 1, decision D-05).
# Installs the SDK pinned in global.json when missing, then runs a locked
# restore, a warnings-as-errors Release build, the test suite, and a format
# verification. Exits non-zero on any failing step. Never installs containers;
# the test step starts throwaway test services and stops them on exit.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

SDK_VERSION=$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])' 2>/dev/null || sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json | head -1)
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

if [ ! -d "$DOTNET_ROOT/sdk/$SDK_VERSION" ]; then
  echo "Installing .NET SDK $SDK_VERSION into $DOTNET_ROOT"
  # Fail closed: the SDK archive is installed only when its SHA-512 matches the pin
  # below. Digest source: releases.json for channel 10.0 (release 10.0.13), reached from
  # https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json,
  # entry dotnet-sdk-linux-x64.tar.gz for SDK 10.0.402.
  case "$SDK_VERSION" in
    10.0.402) SDK_SHA512=1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2 ;;
    *)
      echo "error: no pinned SHA-512 for .NET SDK $SDK_VERSION; add it to bootstrap.sh from releases.json" >&2
      exit 1
      ;;
  esac
  INSTALLER_DIR="$(mktemp -d)"
  trap 'rm -rf "$INSTALLER_DIR"' EXIT
  SDK_ARCHIVE="$INSTALLER_DIR/dotnet-sdk.tar.gz"
  curl -fsSL "https://builds.dotnet.microsoft.com/dotnet/Sdk/$SDK_VERSION/dotnet-sdk-$SDK_VERSION-linux-x64.tar.gz" -o "$SDK_ARCHIVE"
  echo "$SDK_SHA512  $SDK_ARCHIVE" | sha512sum -c -
  mkdir -p "$DOTNET_ROOT"
  tar -xzf "$SDK_ARCHIVE" -C "$DOTNET_ROOT"
fi

dotnet --version
dotnet restore OmpOS.Linux.slnf --locked-mode
dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror

# Cleanup runs on every exit path. `down` is idempotent; the export file holds test passwords and is removed.
TEST_SERVICES_ENV=""
cleanup() {
  bash scripts/test-services.sh down
  if [ -n "$TEST_SERVICES_ENV" ]; then rm -f "$TEST_SERVICES_ENV"; fi
  if [ -n "${INSTALLER_DIR:-}" ]; then rm -rf "$INSTALLER_DIR"; fi
}
trap cleanup EXIT
TEST_SERVICES_ENV="$(bash scripts/test-services.sh up)"
source "$TEST_SERVICES_ENV"
dotnet test OmpOS.Linux.slnf -c Release --no-build
dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore

command -v podman-compose >/dev/null || echo "hint: podman-compose not found; install podman plus 'pipx install podman-compose==1.6.0' to run deploy/compose.yaml"

echo "bootstrap: OK (SDK $(dotnet --version), locked restore, Release build, tests, format)"
