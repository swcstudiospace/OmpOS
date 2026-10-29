#!/usr/bin/env bash
# Publishes the web head as the real application (no Demo code) as static files that can be deployed anywhere
# (Vercel static, a bucket, any web server).
#
# The head calls the Autonogrammer API at exactly one origin, AUTONOGRAMMER_API_ORIGIN (https://host[:port]; http://127.0.0.1:<port>
# only with AUTONOGRAMMER_API_ORIGIN_ALLOW_LOOPBACK=1, for a local check). That origin is written into Api:BaseUrl of
# wwwroot/appsettings.json and is the only origin the Content-Security-Policy adds to connect-src (scripts/vercel/render-csp.sh fails
# closed on an invalid or missing origin). The API must allow the web origin in Api:Cors:Origins.
#
# Fail closed: the bundle must carry neither Demo code nor appsettings.demo.json (scripts/vercel/check-no-demo.sh), and every asset must
# match the service-worker manifest after the origin is written (scripts/vercel/finish-web-bundle.sh, which vercel.ts also runs). The
# output directory holds the published wwwroot plus a static vercel.json with the same headers and SPA rewrite as vercel.ts.
#
# Usage:  AUTONOGRAMMER_API_ORIGIN=https://api.example.com scripts/vercel/build-web.sh [output-dir]      default: output/web
# Needs:  the .NET SDK pinned in global.json (DOTNET_ROOT, default $HOME/.dotnet) and Node (scripts/vercel/finish-web-bundle.sh).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

# A real build never inherits the Demo switch from the environment: only the literal 1 selects the Demo, and this script refuses it.
if [[ "${AUTONOGRAMMER_DEMO-}" == "1" ]]; then
  printf 'build-web: AUTONOGRAMMER_DEMO=1 selects the Demo; use scripts/vercel/build-demo.sh for that\n' >&2
  exit 1
fi

if ! command -v node >/dev/null 2>&1; then
  printf 'build-web: node is required (finish-web-bundle.sh refreshes the service-worker asset manifest) but was not found on PATH\n' >&2
  exit 1
fi

OUT="${1:-$ROOT/output/web}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Validates the origin before anything is built (exit 1 on a missing or malformed origin).
CONNECT_SRC="$(bash "$ROOT/scripts/vercel/render-csp.sh")"
CSP="default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; ${CONNECT_SRC}; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'"

dotnet publish apps/Autonogrammer.Web -c Release -o "$WORK/publish"

rm -rf "$OUT"
mkdir -p "$OUT"
cp -R "$WORK/publish/wwwroot/." "$OUT/"

bash "$ROOT/scripts/vercel/finish-web-bundle.sh" "$OUT"

cat > "$OUT/vercel.json" <<JSON
{
  "rewrites": [{ "source": "/(.*)", "destination": "/index.html" }],
  "headers": [
    {
      "source": "/_framework/(.*)",
      "headers": [{ "key": "Cache-Control", "value": "public, max-age=0, must-revalidate" }]
    },
    {
      "source": "/(.*)",
      "headers": [
        { "key": "Content-Security-Policy", "value": "${CSP}" },
        { "key": "X-Content-Type-Options", "value": "nosniff" },
        { "key": "Referrer-Policy", "value": "strict-origin-when-cross-origin" },
        { "key": "X-Frame-Options", "value": "DENY" }
      ]
    }
  ]
}
JSON

printf 'Web bundle written to %s (API origin is in appsettings.json and the CSP)\n' "$OUT"
