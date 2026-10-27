#!/usr/bin/env bash
# Runs the node:test suite of the GSD-Core omp plugin (plugins/gsd-core-omp). The plugin is OmpOS-owned JavaScript with no
# .NET project, so this script is its gate: bootstrap.sh and the CI workflow both run it. Node 20 or newer is required.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if ! command -v node >/dev/null 2>&1; then
  echo "error: node (20 or newer) is required to run the plugins/gsd-core-omp tests" >&2
  exit 1
fi

node --test plugins/gsd-core-omp/test/*.test.mjs
