#!/usr/bin/env bash
# Serves the published Demo bundle on 127.0.0.1 only (ADR-0017), with the same SPA fallback and headers as the static deployment.
#
# Usage:  scripts/web-demo/serve-demo.sh [bundle-dir]      default: output/demo (built by scripts/vercel/build-demo.sh)
# Env:    DEMO_PORT (default 5190)
# Needs:  python3. The server binds the loopback interface and nothing else.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DIR="${1:-$ROOT/output/demo}"
PORT="${DEMO_PORT:-5190}"

test -f "$DIR/index.html" || { echo "serve-demo: $DIR/index.html not found; run scripts/vercel/build-demo.sh first" >&2; exit 1; }

printf 'Demo on http://127.0.0.1:%s/ (Ctrl+C to stop)\n' "$PORT"
exec python3 - "$DIR" "$PORT" <<'PY'
import http.server
import os
import sys

directory, port = sys.argv[1], int(sys.argv[2])
CSP = ("default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; "
       "connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'")


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {**http.server.SimpleHTTPRequestHandler.extensions_map, ".wasm": "application/wasm", ".js": "text/javascript", ".mjs": "text/javascript", ".json": "application/json"}

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=directory, **kwargs)

    def translate_path(self, path):
        translated = super().translate_path(path)
        if os.path.exists(translated):
            return translated
        return os.path.join(directory, "index.html")

    def end_headers(self):
        self.send_header("Content-Security-Policy", CSP)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "strict-origin-when-cross-origin")
        self.send_header("X-Frame-Options", "DENY")
        super().end_headers()


http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler).serve_forever()
PY
