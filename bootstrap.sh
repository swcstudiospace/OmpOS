#!/usr/bin/env bash
# OmpOS one-command bootstrap (Phase 1, decision D-05).
# Installs the SDK pinned in global.json when missing, then runs a locked
# restore, a warnings-as-errors Release build, the test suite, and a format
# verification. Exits non-zero on any failing step. Never installs containers.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

SDK_VERSION=$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])' 2>/dev/null || sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json | head -1)
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

if [ ! -d "$DOTNET_ROOT/sdk/$SDK_VERSION" ]; then
  echo "Installing .NET SDK $SDK_VERSION into $DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$DOTNET_ROOT"
fi

dotnet --version
dotnet restore OmpOS.Linux.slnf --locked-mode
dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror
dotnet test OmpOS.Linux.slnf -c Release --no-build
dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore

command -v podman-compose >/dev/null || echo "hint: podman-compose not found; install podman plus 'pipx install podman-compose==1.6.0' to run deploy/compose.yaml"

echo "bootstrap: OK (SDK $(dotnet --version), locked restore, Release build, tests, format)"
