#!/usr/bin/env bash
# Vulnerability scan of a built image with trivy, gating on severity. NOT wired as required: ci.yml runs it only when
# `command -v trivy` succeeds. Written without trivy installed on the authoring host; it has not been run.
# Usage: bash scripts/ci/scan-images.sh <image> [more images]
# Environment:
#   TRIVY_SEVERITY      comma list that fails the scan (default HIGH,CRITICAL)
#   TRIVY_OFFLINE_DB    directory holding a pre-fetched trivy DB (a cache dir with db/trivy.db, e.g. ~/.cache/trivy);
#                       when set the scan runs with --skip-db-update --offline-scan, so no network is needed.
#                       Fetch the DB once with: trivy image --download-db-only --cache-dir <dir>
#   TRIVY_IGNOREFILE    optional .trivyignore path
# Exit: 0 clean, 1 findings at or above the severity, 2 usage/trivy missing.
set -euo pipefail
[ "$#" -ge 1 ] || { echo "usage: scan-images.sh <image>..." >&2; exit 2; }
command -v trivy >/dev/null || { echo "scan-images: trivy is not installed" >&2; exit 2; }
args=(image --exit-code 1 --severity "${TRIVY_SEVERITY:-HIGH,CRITICAL}" --ignore-unfixed --no-progress)
if [ -n "${TRIVY_OFFLINE_DB:-}" ]; then
  args+=(--cache-dir "$TRIVY_OFFLINE_DB" --skip-db-update --skip-java-db-update --offline-scan)
fi
[ -n "${TRIVY_IGNOREFILE:-}" ] && args+=(--ignorefile "$TRIVY_IGNOREFILE")
status=0
for image in "$@"; do
  echo "scan-images: $image"
  trivy "${args[@]}" "$image" || status=1
done
exit "$status"
