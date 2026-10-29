#!/usr/bin/env bash
# SBOM freshness gate: docs/legal/sbom.spdx.json must equal what scripts/legal/generate-sbom.py produces from the
# packages.lock.json files and the container pins. Needs the restored NuGet cache for licence data (run after dotnet restore).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
python3 scripts/legal/generate-sbom.py --check
