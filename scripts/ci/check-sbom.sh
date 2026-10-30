#!/usr/bin/env bash
# SBOM freshness gate: docs/legal/sbom.spdx.json must equal what scripts/legal/generate-sbom.py produces from the
# packages.lock.json files and the container pins. Licences come from the restored NuGet cache when present; a package the cache lacks
# (for example Android-only ones on a runner that restores only the Linux solution filter) keeps the licence the committed SBOM records
# for the same name and version, and anything else is NOASSERTION.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
python3 scripts/legal/generate-sbom.py --check
