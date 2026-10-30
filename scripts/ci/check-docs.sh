#!/usr/bin/env bash
# Offline check of the documentation structure (GitBook Git Sync layout): .gitbook.yaml, docs/SUMMARY.md, orphans,
# links, anchors, front matter, one H1 and a Status line per page, the pinned screenshots (see scripts/docs/check_gitbook.py).
# Usage: bash scripts/ci/check-docs.sh [--list-outside-root]
# Environment: AUTONOGRAMMER_RELEASE=1 turns the missing pinned screenshots from warnings into errors (the release gate).
# Python 3 (standard library only) is required. No network access. Exits non-zero on any error.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if ! command -v python3 >/dev/null 2>&1; then
  echo "error: python3 is required to check the documentation" >&2
  exit 1
fi

args=("$@")
if [ "${AUTONOGRAMMER_RELEASE:-}" = "1" ]; then
  args+=("--require-screenshots")
fi

python3 scripts/docs/check_gitbook.py . "${args[@]}"
python3 -m json.tool docs/api/openapi.json >/dev/null || { echo "error: docs/api/openapi.json is not valid JSON" >&2; exit 1; }
