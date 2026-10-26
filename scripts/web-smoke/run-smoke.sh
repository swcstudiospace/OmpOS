#!/usr/bin/env bash
# WEB-01 browser smoke. Repeats docs/runbooks/local-dev.md: "Serve the API (in-memory adapter)",
# "Serve the published web head", "Browser smoke" and "Re-run of axe-core".
#
# Deliberate differences from the runbook text:
#   - The API and the web head are published with --no-restore, under a 300 s timeout each, and
#     run from their output directories. The API is not started with `dotnet run`, so every child
#     process can be stopped cleanly.
#   - The API runs with ASPNETCORE_ENVIRONMENT=Development, Sessions:Store=InMemory, Coordination:Store=InMemory and
#     Api:Auth:AllowDevelopmentPrincipal=true. Outside Development the API refuses to start without Api:Auth:Authority
#     and Api:Auth:Audience (OD-03), refuses AllowDevelopmentPrincipal, and requires Sessions:Store. The runbook's serve
#     command sets none of these, so its chat flow returns 401 (every endpoint requires a bearer token by default).
#   - The browser is the first of chromium, chromium-browser, google-chrome and chrome on PATH,
#     or CHROME_BIN when set, with the Playwright chrome-linux64/chrome as the last fallback.
#   - The browser is driven over CDP by scripts/web-smoke/smoke.mjs, not by the browser.open tool.
#
# Every run uses a fresh, empty --user-data-dir. Output (JSON, log, screenshots, server and build
# logs) goes to OUT_DIR, default docs/runbooks/evidence/quality-audit-2026-10-22. Exit status is the
# smoke's: 0 only when every assertion holds.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

COMMIT="$(git rev-parse --short HEAD)"
RUN_DATE="${RUN_DATE:-$(date -u +%F)}"
STAMP="$COMMIT-$RUN_DATE"
OUT_DIR="${OUT_DIR:-$ROOT/docs/runbooks/evidence/quality-audit-2026-10-22}"
API_PORT="${API_PORT:-5187}"
WEB_PORT="${WEB_PORT:-5180}"
AXE_VERSION="4.14.0"

mkdir -p "$OUT_DIR"
WORK="$(mktemp -d)"
PIDS=()
cleanup() {
  local pid
  for pid in "${PIDS[@]:-}"; do
    kill "$pid" 2>/dev/null || true
  done
  for pid in "${PIDS[@]:-}"; do
    wait "$pid" 2>/dev/null || true
  done
  rm -rf "$WORK"
}
trap cleanup EXIT

find_chrome() {
  if [ -n "${CHROME_BIN:-}" ]; then
    printf '%s\n' "$CHROME_BIN"
    return 0
  fi
  local name
  for name in chromium chromium-browser google-chrome chrome; do
    if command -v "$name" >/dev/null 2>&1; then
      command -v "$name"
      return 0
    fi
  done
  local candidate
  for candidate in "$HOME"/.cache/ms-playwright/chromium-*/chrome-linux64/chrome; do
    if [ -x "$candidate" ]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  echo "error: no Chromium binary found; install one or set CHROME_BIN" >&2
  return 1
}

wait_http() {
  local url="$1" name="$2" deadline=$((SECONDS + 120))
  until curl -s -o /dev/null "$url"; do
    if [ "$SECONDS" -ge "$deadline" ]; then
      echo "error: $name did not answer at $url" >&2
      return 1
    fi
    sleep 1
  done
}

CHROME="$(find_chrome)"
echo "commit=$COMMIT date=$RUN_DATE chrome=$CHROME"

# Serve the published web head (runbook: "Serve the published web head").
timeout 300 dotnet publish apps/OmpOS.Web/OmpOS.Web.csproj -c Release --no-restore -nodeReuse:false -o "$WORK/web" \
  >"$OUT_DIR/web-publish-$STAMP.log" 2>&1

# Serve the API with the in-memory adapter (runbook: "Serve the API (in-memory adapter)").
timeout 300 dotnet publish services/OmpOS.Api/OmpOS.Api.csproj -c Release --no-restore -nodeReuse:false -o "$WORK/api" \
  >"$OUT_DIR/api-publish-$STAMP.log" 2>&1
env ASPNETCORE_ENVIRONMENT=Development Harness__Adapter=InMemory Sessions__Store=InMemory Coordination__Store=InMemory Api__Auth__AllowDevelopmentPrincipal=true "Api__Cors__Origins__0=http://127.0.0.1:$WEB_PORT" \
  dotnet "$WORK/api/OmpOS.Api.dll" --urls "http://127.0.0.1:$API_PORT" \
  >"$OUT_DIR/api-$STAMP.log" 2>&1 &
PIDS+=($!)

python3 -m http.server "$WEB_PORT" --bind 127.0.0.1 --directory "$WORK/web/wwwroot" \
  >"$OUT_DIR/web-static-$STAMP.log" 2>&1 &
PIDS+=($!)

wait_http "http://127.0.0.1:$WEB_PORT/" "static web head"
wait_http "http://127.0.0.1:$API_PORT/" "API"

# Owned headless Chrome with an empty profile (runbook: "Browser smoke").
PROFILE="$WORK/chrome-profile"
mkdir -p "$PROFILE"
"$CHROME" --version >"$OUT_DIR/chrome-version-$STAMP.txt" 2>&1 || true
"$CHROME" --headless --no-sandbox --disable-dev-shm-usage --remote-debugging-port=0 \
  --user-data-dir="$PROFILE" about:blank >"$OUT_DIR/chrome-$STAMP.log" 2>&1 &
PIDS+=($!)
for _ in $(seq 1 60); do
  [ -s "$PROFILE/DevToolsActivePort" ] && break
  sleep 0.5
done
if [ ! -s "$PROFILE/DevToolsActivePort" ]; then
  echo "error: Chrome exposed no DevTools port" >&2
  exit 1
fi
IFS= read -r CDP_PORT <"$PROFILE/DevToolsActivePort"

# Pinned axe-core engine (runbook: "Re-run of axe-core", step 3).
AXE_DIR="$WORK/axe"
mkdir -p "$AXE_DIR"
(cd "$AXE_DIR" && npm pack "axe-core@$AXE_VERSION" >/dev/null 2>&1 && tar -xzf "axe-core-$AXE_VERSION.tgz" package/axe.min.js)
# Fail closed: the engine must match the committed digest before the smoke run uses it.
if ! (cd "$AXE_DIR/package" && sha256sum -c "$ROOT/scripts/web-smoke/axe-core-$AXE_VERSION.sha256"); then
  echo "error: axe-core $AXE_VERSION axe.min.js does not match scripts/web-smoke/axe-core-$AXE_VERSION.sha256" >&2
  exit 1
fi
sha256sum "$AXE_DIR/package/axe.min.js" >"$OUT_DIR/axe-core-$AXE_VERSION-$STAMP.sha256"

set +e
CDP_PORT="$CDP_PORT" APP_URL="http://127.0.0.1:$WEB_PORT/" API_ORIGIN="http://127.0.0.1:$API_PORT" \
  OUT_DIR="$OUT_DIR" COMMIT="$COMMIT" RUN_DATE="$RUN_DATE" AXE_JS="$AXE_DIR/package/axe.min.js" \
  node "$ROOT/scripts/web-smoke/smoke.mjs" \
  >"$OUT_DIR/web-smoke-$STAMP.json" 2>"$OUT_DIR/web-smoke-$STAMP.log"
STATUS=$?
set -e

echo "smoke exit=$STATUS output=$OUT_DIR/web-smoke-$STAMP.json"
exit "$STATUS"
