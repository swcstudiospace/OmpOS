#!/usr/bin/env bash
# Installs SDK 10.0.402 into a private directory and points later steps at it.
# GitHub-hosted images already have runtime 10.0.14. actions/setup-dotnet installs
# 10.0.402 beside that runtime, and locked restore then sees Microsoft.NET.Sdk.WebAssembly.Pack
# [10.0.14, ) instead of the [10.0.13, ) written by SDK 10.0.402 (NU1004).
# DOTNET_MULTILEVEL_LOOKUP=0 keeps the preinstalled runtime invisible.
# The installer is the same script setup-dotnet downloads; the SDK version is pinned.
set -euo pipefail

version=10.0.402
root="${RUNNER_TEMP:-${TMPDIR:-/tmp}}/dotnet-pinned"
mkdir -p "$root"

case "$(uname -s)" in
  Linux | Darwin)
    script="${RUNNER_TEMP:-/tmp}/dotnet-install.sh"
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$script"
    bash "$script" --version "$version" --install-dir "$root"
    dotnet_bin="$root/dotnet"
    ;;
  MINGW* | MSYS* | CYGWIN*)
    script="${RUNNER_TEMP:-/tmp}/dotnet-install.ps1"
    curl -fsSL https://dot.net/v1/dotnet-install.ps1 -o "$script"
    powershell -NoProfile -ExecutionPolicy Bypass -File "$script" -Version "$version" -InstallDir "$root"
    dotnet_bin="$root/dotnet.exe"
    ;;
  *)
    echo "error: unsupported OS $(uname -s)" >&2
    exit 1
    ;;
esac

[ -x "$dotnet_bin" ] || { echo "error: $dotnet_bin was not installed" >&2; exit 1; }

if [ -n "${GITHUB_PATH:-}" ]; then
  echo "$root" >> "$GITHUB_PATH"
fi
if [ -n "${GITHUB_ENV:-}" ]; then
  {
    echo "DOTNET_ROOT=$root"
    echo "DOTNET_MULTILEVEL_LOOKUP=0"
    echo "DOTNET_ROLL_FORWARD=Disable"
  } >> "$GITHUB_ENV"
fi
export PATH="$root:$PATH" DOTNET_ROOT="$root" DOTNET_MULTILEVEL_LOOKUP=0 DOTNET_ROLL_FORWARD=Disable

got="$("$dotnet_bin" --version)"
echo "pinned SDK $got"
"$dotnet_bin" --list-runtimes
test "$got" = "$version"
if "$dotnet_bin" --list-runtimes | grep -q 'Microsoft.NETCore.App 10\.0\.14'; then
  echo "error: runtime 10.0.14 is visible to the pinned SDK; locked restore would drift" >&2
  exit 1
fi
