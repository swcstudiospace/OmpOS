#!/usr/bin/env bash
# Publishes the web head in Demo mode (ADR-0017) as static files that can be deployed anywhere (Vercel static, a bucket, any web server).
#
# The Demo answers every API route inside the browser, so the bundle has no API origin and the Content-Security-Policy only allows the page's
# own origin. The output directory holds the published wwwroot plus a static vercel.json with the same headers and SPA rewrite as vercel.ts.
#
# Usage:  scripts/vercel/build-demo.sh [output-dir]      default: output/demo
# Needs:  the .NET SDK pinned in global.json (DOTNET_ROOT, default $HOME/.dotnet).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1

OUT="${1:-$ROOT/output/demo}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

CONNECT_SRC="$(OMPOS_DEMO=1 bash "$ROOT/scripts/vercel/render-csp.sh")"
CSP="default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; ${CONNECT_SRC}; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'"

dotnet publish apps/OmpOS.Web -c Release -p:OmpOSDemo=true -o "$WORK/publish"

rm -rf "$OUT"
mkdir -p "$OUT"
cp -R "$WORK/publish/wwwroot/." "$OUT/"

# Fail closed: a Demo bundle must carry the Demo marker and the Demo settings file.
test -f "$OUT/appsettings.demo.json" || { echo "build-demo: appsettings.demo.json is missing from the Demo bundle" >&2; exit 1; }

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

printf 'Demo bundle written to %s\n' "$OUT"
