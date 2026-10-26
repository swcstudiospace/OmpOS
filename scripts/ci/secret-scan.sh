#!/usr/bin/env bash
# Secret scan shared by bootstrap.sh and .github/workflows/ci.yml, so the local gate and CI run the same
# pinned gitleaks with the same flags. The version and SHA-256 come from scripts/ci/gitleaks.env.
#
# Fails closed: the release tarball is used only when its SHA-256 matches the pin, and a download that is
# not possible (offline, blocked host) fails the scan with a clear message. The one deliberate escape is
# OMPOS_SKIP_SECRET_SCAN=1, which skips the scan with a warning. CI never sets it.
#
# Cache: the verified tarball is kept under $OMPOS_GITLEAKS_CACHE_DIR (default: $RUNNER_TEMP in CI, else
# ${XDG_CACHE_HOME:-$HOME/.cache}/ompos), so a repeat run works offline. The digest is re-checked on every run.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if [ "${OMPOS_SKIP_SECRET_SCAN:-}" = "1" ]; then
  echo "warning: secret scan skipped because OMPOS_SKIP_SECRET_SCAN=1; CI still runs it" >&2
  exit 0
fi

# shellcheck source=scripts/ci/gitleaks.env
source scripts/ci/gitleaks.env

fail() {
  echo "error: secret scan: $1" >&2
  echo "       set OMPOS_SKIP_SECRET_SCAN=1 to skip it explicitly (CI still runs it)." >&2
  exit 1
}

[ "$(uname -s)" = "Linux" ] && [ "$(uname -m)" = "x86_64" ] \
  || fail "the pinned gitleaks digest covers linux x64 only, this host is $(uname -s) $(uname -m)"
git rev-parse --git-dir >/dev/null 2>&1 || fail "not inside a git work tree, gitleaks scans history"

CACHE_DIR="${OMPOS_GITLEAKS_CACHE_DIR:-${RUNNER_TEMP:-${XDG_CACHE_HOME:-$HOME/.cache}/ompos}}"
ARCHIVE="$CACHE_DIR/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz"
mkdir -p "$CACHE_DIR"

digest_ok() {
  [ -f "$ARCHIVE" ] && echo "${GITLEAKS_SHA256}  $ARCHIVE" | sha256sum -c - >/dev/null 2>&1
}

if ! digest_ok; then
  PARTIAL="$ARCHIVE.part"
  rm -f "$ARCHIVE" "$PARTIAL"
  curl -fsSL -o "$PARTIAL" "https://github.com/gitleaks/gitleaks/releases/download/v${GITLEAKS_VERSION}/gitleaks_${GITLEAKS_VERSION}_linux_x64.tar.gz" \
    || { rm -f "$PARTIAL"; fail "cannot download gitleaks ${GITLEAKS_VERSION} (offline?)"; }
  mv "$PARTIAL" "$ARCHIVE"
  digest_ok || { rm -f "$ARCHIVE"; fail "gitleaks ${GITLEAKS_VERSION} tarball does not match the pinned SHA-256 in scripts/ci/gitleaks.env"; }
fi

BIN_DIR="$(mktemp -d)"
trap 'rm -rf "$BIN_DIR"' EXIT
tar -xzf "$ARCHIVE" -C "$BIN_DIR" gitleaks

# Scan the checked-out commit and all of its history: on a pull request that is main plus the PR's commits
# (the merge ref), on a push it is that branch. gitleaks' default log options include --all, which with
# fetch-depth: 0 also scans every other branch, so one branch's findings failed every PR. Each branch is still
# scanned by its own push run. Fingerprint allowlist: .gitleaksignore.
"$BIN_DIR/gitleaks" git --redact --exit-code 1 --log-level warn --log-opts="--full-history HEAD" .
echo "secret scan: OK (gitleaks ${GITLEAKS_VERSION})"
