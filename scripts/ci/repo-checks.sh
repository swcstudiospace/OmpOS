#!/usr/bin/env bash
# Static repository checks that need only bash and python3 and no NuGet cache: the brand asset set, the changelog against
# the version, the documentation structure, the generated provider reference and the settings parity table. bootstrap.sh and
# ci.yml run this one script, so the local gate and CI cannot drift. The licence check needs the restored NuGet cache and is
# run by both callers after the restore (scripts/ci/check-licences.sh).
# Usage: bash scripts/ci/repo-checks.sh   (run from anywhere; stops at the first failing check)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

bash scripts/ci/check-brand.sh
bash scripts/ci/check-changelog.sh
bash scripts/ci/check-docs.sh
bash scripts/ci/check-providers-doc.sh
bash scripts/check-settings-parity.sh
echo "repo-checks: ok"
