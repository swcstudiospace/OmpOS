#!/usr/bin/env bash
# Runs the hosted end-to-end harness on this machine, from nothing to a report:
#
#   scripted model (loopback)  <-  omp acp (real, isolated agent directory)  <-  Autonogrammer API (owner-token mode)  <-  checks
#
# What it proves and what it does not is written in scripts/hosted/README.md: the real engine loop, streaming, approvals, resume, the
# owner token and the rate limiter work end to end; nothing is learned about a real model. No credential and no network egress are used,
# and the real ~/.omp is never read or written.
#
# Usage:   scripts/hosted/run-local.sh [--web-bundle <dir>] [--out <dir>] [--no-approvals] [--skip <steps>]
#            --web-bundle  a published real web bundle (scripts/vercel/build-web.sh with AUTONOGRAMMER_API_ORIGIN=http://127.0.0.1:5187
#                          and AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1); the browser check runs only when it is given
#            --out         report directory (default: a temporary directory that is kept and printed)
#            --no-approvals  leave Harness:Acp:Approval at its default (deny everything) to check the default-deny behaviour
#            --skip        steps of e2e-api.mjs to skip, comma separated (for example rate-limit)
# Needs:   the Release build of services/Autonogrammer.Api, omp on PATH (or OMP_PATH), node 24, the .NET SDK in DOTNET_ROOT (default
#          $HOME/.dotnet); Chrome only for the browser check. Ports: MODEL_PORT (18080), API_PORT (5187), WEB_PORT (5180).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"

MODEL_PORT="${MODEL_PORT:-18080}"
API_PORT="${API_PORT:-5187}"
WEB_PORT="${WEB_PORT:-5180}"
OMP="${OMP_PATH:-$(command -v omp || true)}"
API_DLL="${API_DLL:-$ROOT/services/Autonogrammer.Api/bin/Release/net10.0/Autonogrammer.Api.dll}"
WEB_BUNDLE=""
OUT=""
APPROVALS=1
SKIP=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --web-bundle) WEB_BUNDLE="$2"; shift 2 ;;
    --out) OUT="$2"; shift 2 ;;
    --no-approvals) APPROVALS=0; shift ;;
    --skip) SKIP="$2"; shift 2 ;;
    *) printf 'run-local: unknown argument %s\n' "$1" >&2; exit 2 ;;
  esac
done

[[ -n "$OMP" && -x "$OMP" ]] || { printf 'run-local: omp not found; put it on PATH or set OMP_PATH\n' >&2; exit 1; }
[[ -f "$API_DLL" ]] || { printf 'run-local: %s not found; build services/Autonogrammer.Api in Release first\n' "$API_DLL" >&2; exit 1; }
[[ -z "$WEB_BUNDLE" || -f "$WEB_BUNDLE/index.html" ]] || { printf 'run-local: %s has no index.html\n' "$WEB_BUNDLE" >&2; exit 1; }

WORK="$(mktemp -d "${TMPDIR:-/tmp}/autonogrammer-hosted.XXXXXX")"
OUT="${OUT:-$WORK/report}"
mkdir -p "$OUT"
PIDS=()
cleanup() {
  for pid in "${PIDS[@]:-}"; do
    kill "$pid" 2>/dev/null || true
  done
  wait 2>/dev/null || true
  printf 'run-local: reports in %s (the temporary agent directory %s is removed)\n' "$OUT" "$WORK/omp"
  rm -rf "$WORK/omp" "$WORK/work"
}
trap cleanup EXIT

waitfor() {
  local url="$1" label="$2"
  for _ in $(seq 1 120); do
    if curl -fsS "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  printf 'run-local: %s did not become ready (%s)\n' "$label" "$url" >&2
  return 1
}

# 1. The scripted model.
node scripts/hosted/fake-model-server.mjs --port "$MODEL_PORT" --verbose >"$OUT/model.log" 2>&1 &
PIDS+=("$!")
waitfor "http://127.0.0.1:$MODEL_PORT/v1/models" "the scripted model"

# 2. An isolated omp home (never ~/.omp). Prints PI_CODING_AGENT_DIR=..., HOME=... and PI_NO_TITLE=1, one per line.
mapfile -t OMP_ENV < <(node scripts/hosted/prepare-omp-home.mjs --dir "$WORK/omp" --fake-url "http://127.0.0.1:$MODEL_PORT/v1")

# 3. An owner token and its digest: the API only ever sees the digest.
TOKEN="$(head -c 32 /dev/urandom | base64 | tr '+/' '-_' | tr -d '=\n')"
DIGEST="$(printf %s "$TOKEN" | sha256sum | cut -d' ' -f1)"

# 4. The API: real omp over ACP, the engine environment on the allow-list, a per-session workspace, owner-token sign-in.
API_ENV=(
  "ASPNETCORE_ENVIRONMENT=Development"
  "ASPNETCORE_URLS=http://127.0.0.1:$API_PORT"
  "DOTNET_ROOT=$DOTNET_ROOT"
  "Sessions__Store=InMemory" "Coordination__Store=InMemory"
  "Harness__Adapter=Acp" "Harness__Acp__OmpPath=$OMP"
  "Harness__Acp__Environment__Mode=Allowlist"
  "Harness__Acp__WorkspaceRoot=$WORK/work"
  "Api__Auth__Mode=OwnerToken" "Api__Auth__OwnerToken__Sha256__0=$DIGEST"
  "Api__Cors__Origins__0=http://127.0.0.1:$WEB_PORT"
)
if [[ "$APPROVALS" -eq 1 ]]; then
  API_ENV+=("Harness__Acp__Approval__Kinds__0=execute" "Harness__Acp__Approval__Kinds__1=edit" "Harness__Acp__Approval__GrantableTools__0=*")
fi
env "${OMP_ENV[@]}" "${API_ENV[@]}" dotnet "$API_DLL" >"$OUT/api.log" 2>&1 &
PIDS+=("$!")
waitfor "http://127.0.0.1:$API_PORT/health/ready" "the API"

# 5. The API checks. The expected approval outcome depends on the policy that was started. The rate-limit step drains the session-creation bucket for
#    about a minute, so it runs last, after the browser check, which creates a session of its own.
EXPECT="approved"
[[ "$APPROVALS" -eq 0 ]] && EXPECT="policy-denied"
FIRST_SKIP="rate-limit"
[[ -n "$SKIP" ]] && FIRST_SKIP="rate-limit,$SKIP"
STATUS=0
AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-api.mjs --api "http://127.0.0.1:$API_PORT" --approval-expect "$EXPECT" --skip "$FIRST_SKIP" --out "$OUT/e2e-api.json" || STATUS=$?

# 6. The browser check, only with a bundle.
if [[ -n "$WEB_BUNDLE" ]]; then
  python3 -m http.server "$WEB_PORT" --bind 127.0.0.1 --directory "$WEB_BUNDLE" >"$OUT/web.log" 2>&1 &
  PIDS+=("$!")
  waitfor "http://127.0.0.1:$WEB_PORT/" "the static server"
  AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-browser.mjs --url "http://127.0.0.1:$WEB_PORT/" --out "$OUT/browser" || STATUS=$?
fi

# 7. The rate limit, last.
if [[ ",$SKIP," != *",rate-limit,"* ]]; then
  AUTONOGRAMMER_E2E_TOKEN="$TOKEN" node scripts/hosted/e2e-api.mjs --api "http://127.0.0.1:$API_PORT" --skip "auth-config,unauthenticated,bad-token,text-turn,tool-turn,resume" --out "$OUT/e2e-api-rate-limit.json" || STATUS=$?
fi

printf 'run-local: model requests logged: %s\n' "$(grep -c . "$OUT/model.log" || true)"
exit "$STATUS"
