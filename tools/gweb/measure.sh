#!/usr/bin/env bash
# Re-measures G-WEB for Avalonia.Browser: publishes the stock and named heads, serves each wwwroot on
# localhost, drives headless Chromium over CDP and prints the accessibility roleNodes count per build.
# Raw output lands in the evidence folder (GWEB_EVIDENCE_DIR). Requires dotnet, node >= 22, python3, curl
# and a Chromium binary (CHROME_BIN, or the first of chromium, chromium-browser, google-chrome, chrome).
# GWEB_AVALONIA_VERSION=x.y.z publishes scratch copies whose Avalonia family pin is x.y.z; the repository pin is not modified.
# GWEB_PUBLISH_ARGS passes extra arguments to every dotnet publish.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
EVIDENCE="${GWEB_EVIDENCE_DIR:-$REPO/docs/runbooks/evidence/gweb-remeasure-2026-10-22}"
WORK="${GWEB_WORK_DIR:-$(mktemp -d /tmp/gweb-remeasure.XXXXXX)}"
CDP_PORT="${GWEB_CDP_PORT:-9444}"
STOCK_PORT="${GWEB_STOCK_PORT:-5411}"
NAMED_PORT="${GWEB_NAMED_PORT:-5412}"
WAIT_MS="${GWEB_WAIT_MS:-25000}"
AVALONIA_VERSION="${GWEB_AVALONIA_VERSION:-}"
PUBLISH_EXTRA="${GWEB_PUBLISH_ARGS:-}"

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

mkdir -p "$EVIDENCE"
exec > >(tee "$EVIDENCE/measure-output.txt") 2>&1

echo "== measure.sh started $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "repo=$REPO"
echo "evidence=$EVIDENCE"
echo "work=$WORK"

if ! node -e 'if (typeof WebSocket !== "function") process.exit(1)'; then
  echo "blocker: node lacks global WebSocket (need node >= 22)" >&2
  exit 2
fi

CHROME="${CHROME_BIN:-}"
if [[ -z "$CHROME" ]]; then
  for candidate in chromium chromium-browser google-chrome chrome; do
    if command -v "$candidate" >/dev/null 2>&1; then
      CHROME="$(command -v "$candidate")"
      break
    fi
  done
fi
if [[ -z "$CHROME" ]]; then
  echo "blocker: no Chromium binary found (chromium chromium-browser google-chrome chrome)" >&2
  exit 2
fi
echo "chrome=$CHROME"
echo "chrome_version=$("$CHROME" --version 2>/dev/null || echo unknown)"
echo "node_version=$(node --version)"
echo "dotnet_version=$(dotnet --version)"
echo "host=$(uname -srm)"

SRC="$HERE"
PUBLISH_ARGS=()
read -r -a PUBLISH_ARGS <<<"$PUBLISH_EXTRA"
if [[ -n "$AVALONIA_VERSION" ]]; then
  if [[ ! "$AVALONIA_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "blocker: GWEB_AVALONIA_VERSION must be a release number such as 12.1.5" >&2
    exit 2
  fi
  SRC="$WORK/src/tools/gweb"
  mkdir -p "$SRC"
  tar -C "$HERE" --exclude=bin --exclude=obj -cf - . | tar -C "$SRC" -xf -
  for f in global.json Directory.Build.props Directory.Packages.props BannedSymbols.txt BannedSymbols.NoProcess.txt .editorconfig; do
    cp "$REPO/$f" "$WORK/src/$f"
  done
  PKG="$WORK/src/Directory.Packages.props"
  pinned=$(grep -c -E 'Include="Avalonia[^"]*" Version="12\.1\.4"' "$PKG" || true)
  sed -i -E "s/(Include=\"Avalonia[^\"]*\" Version=\")12\.1\.4\"/\1$AVALONIA_VERSION\"/" "$PKG"
  left=$(grep -c -E 'Include="Avalonia[^"]*" Version="12\.1\.4"' "$PKG" || true)
  echo "avalonia_pin_override=$AVALONIA_VERSION scratch=$WORK/src pins_rewritten=$pinned pins_left_at_12.1.4=$left"
  if (( pinned == 0 || left != 0 )); then
    echo "blocker: Avalonia pin rewrite failed" >&2
    exit 2
  fi
  # The scratch copy's lock files record the pin under test; locked restore would reject that change.
  PUBLISH_ARGS+=(-p:RestoreLockedMode=false)
fi
PIDS=()
cleanup() {
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
}
trap cleanup EXIT

"$CHROME" --headless=new --no-sandbox --disable-gpu --remote-debugging-port="$CDP_PORT" \
  --user-data-dir="$WORK/chrome-profile" --window-size=1024,768 about:blank >"$WORK/chrome.log" 2>&1 &
PIDS+=("$!")
for _ in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$CDP_PORT/json/version" >/dev/null 2>&1; then
    break
  fi
  sleep 0.5
done
echo "cdp_version=$(curl -fsS "http://127.0.0.1:$CDP_PORT/json/version" | tr -d '\n')"

measure_build() {
  local name="$1" project="$2" port="$3"
  local out="$WORK/$name"
  echo
  echo "== build=$name project=$project"
  (cd "$SRC/$project" && dotnet publish -c Release -o "$out" "${PUBLISH_ARGS[@]}")
  node -e 'const deps = Object.values(JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")).dependencies)[0]; for (const name of ["Avalonia", "Avalonia.Browser", "Avalonia.Themes.Fluent"]) console.log(`resolved_${name}=${deps[name].resolved}`)' "$SRC/$project/packages.lock.json"
  local root="$out/wwwroot"
  echo "publish_files=$(find "$out" -type f | wc -l)"
  echo "publish_wwwroot_bytes=$(du -sb "$root" | cut -f1)"

  python3 -m http.server "$port" --bind 127.0.0.1 --directory "$root" >"$WORK/$name-http.log" 2>&1 &
  local server_pid="$!"
  PIDS+=("$server_pid")
  for _ in $(seq 1 40); do
    if curl -fsS "http://127.0.0.1:$port/" >/dev/null 2>&1; then
      break
    fi
    sleep 0.25
  done

  node "$HERE/cdp-ax.mjs" "$CDP_PORT" "http://127.0.0.1:$port/" "$EVIDENCE/$name" "$WAIT_MS"
  kill "$server_pid" 2>/dev/null || true
}

measure_build stock "stock" "$STOCK_PORT"
measure_build named "named" "$NAMED_PORT"

echo
echo "== measure.sh finished $(date -u +%Y-%m-%dT%H:%M:%SZ)"
