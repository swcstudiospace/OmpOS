#!/usr/bin/env bash
# Checks docs/settings-parity.md: every row id S01..S50 is present exactly once with a known status.
# Exit 0 when the table is complete, 1 with the problems printed otherwise.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
doc="$root/docs/settings-parity.md"
status=0

for n in $(seq 1 50); do
  id="$(printf 'S%02d' "$n")"
  count="$(awk -F'|' -v id="$id" '{ gsub(/ /, "", $2); if ($2 == id) c++ } END { print c + 0 }' "$doc")"
  if [ "$count" -ne 1 ]; then
    echo "row $id appears $count times (expected 1)"
    status=1
    continue
  fi
  row_status="$(awk -F'|' -v id="$id" '{ k = $2; gsub(/ /, "", k); if (k == id) { s = $4; gsub(/ /, "", s); print s } }' "$doc")"
  case "$row_status" in
    Built | Wired | Partial | Omitted) ;;
    *)
      echo "row $id has an unknown status '$row_status'"
      status=1
      ;;
  esac
done

exit "$status"
