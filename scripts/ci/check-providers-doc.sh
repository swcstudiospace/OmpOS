#!/usr/bin/env bash
# Fails when docs/reference/providers.md is stale or the provider catalogue data is invalid. The page is generated from
# src/Autonogrammer.Harness.Providers/Data/catalogue-*.json by scripts/docs/generate-providers-md.py; this wrapper runs its --check mode.
# Python 3 (standard library only) is required. No network access.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if ! command -v python3 >/dev/null 2>&1; then
  echo "error: python3 is required to check docs/reference/providers.md" >&2
  exit 1
fi

python3 scripts/docs/generate-providers-md.py --check
