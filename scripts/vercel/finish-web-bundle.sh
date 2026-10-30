#!/usr/bin/env bash
# Finishes a published real web bundle in place: the steps that must follow `dotnet publish` of apps/Autonogrammer.Web, in this order.
#
#   1. Writes the one validated API origin (AUTONOGRAMMER_API_ORIGIN) into Api:BaseUrl of appsettings.json (scripts/render-web-config.sh).
#      The head calls the API only at that origin; without this step a bundle keeps the checked-in loopback default and the browser
#      would call 127.0.0.1 while the Content-Security-Policy allows only the real origin.
#   2. Refreshes the service-worker asset manifest for that edit (scripts/vercel/refresh-web-manifest.mjs). The worker pre-caches
#      appsettings.json with the hash from service-worker-assets.js, so a stale hash stops the worker from installing (no offline
#      shell, no update notice); the manifest version, which names the worker's cache, changes with it.
#   3. Verifies every asset against the manifest, then fails when the bundle carries Demo code (scripts/vercel/check-no-demo.sh).
#
# scripts/vercel/build-web.sh and vercel.ts both call this script, so a local build and a build on Vercel cannot drift apart.
#
# Usage:  AUTONOGRAMMER_API_ORIGIN=https://api.example.com scripts/vercel/finish-web-bundle.sh <published-wwwroot>
# Needs:  Node (the manifest refresh). Exit 2 on a usage error, 1 when a step fails; the bundle is then not fit to deploy.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [[ $# -ne 1 || ! -d "$1" ]]; then
  printf 'usage: AUTONOGRAMMER_API_ORIGIN=<origin> scripts/vercel/finish-web-bundle.sh <published-wwwroot>\n' >&2
  exit 2
fi
BUNDLE="$1"

if ! command -v node >/dev/null 2>&1; then
  printf 'finish-web-bundle: node is required (it refreshes the service-worker asset manifest) but was not found on PATH\n' >&2
  exit 1
fi

sh "$ROOT/scripts/render-web-config.sh" "$BUNDLE/appsettings.json"
node "$ROOT/scripts/vercel/refresh-web-manifest.mjs" "$BUNDLE" appsettings.json
node "$ROOT/scripts/vercel/refresh-web-manifest.mjs" --verify "$BUNDLE"
bash "$ROOT/scripts/vercel/check-no-demo.sh" "$BUNDLE"
