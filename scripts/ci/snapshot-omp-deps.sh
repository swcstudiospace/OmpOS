#!/usr/bin/env bash
# Snapshot (or verify) the resolved top-level dependency list of the omp install.
# The omp npm package ships no lockfile, so `bun install` resolves ranges at image build time. This script turns
# `bun pm ls` (run inside the installed omp directory) into a stable JSON list, so the build can fail when the
# resolved set drifts from the committed deploy/omp-deps.lock.json.
#
# Usage:
#   snapshot-omp-deps.sh write <omp-dir> <out.json>      write the snapshot (run once in the omp-runtime stage)
#   snapshot-omp-deps.sh check <omp-dir> <committed.json>  compare; exit 1 on drift
# Producing the committed file (podman only):
#   podman build -f deploy/api.Containerfile --target omp-runtime --build-arg OMP_DEPS_MODE=write -t omp-deps:tmp .
#   podman run --rm omp-deps:tmp cat /tmp/omp-deps.lock.json > deploy/omp-deps.lock.json
# While the committed file carries "pending": true the check prints the resolved list and passes with a warning.
set -euo pipefail

mode="${1:?usage: snapshot-omp-deps.sh write|check <omp-dir> <json>}"
dir="${2:?omp dir required}"
file="${3:?json file required}"
bun="${BUN:-bun}"

render() {
  local version
  version="$(sed -n 's/.*"version":[[:space:]]*"\([^"]*\)".*/\1/p' "$dir/package.json" | head -n 1)"
  # `bun pm ls` prints a tree: "├── name@version" per top-level dependency (the project line has no tree glyph).
  local list
  list="$(cd "$dir" && "$bun" pm ls | sed -n 's/^[^[:alnum:]@]*\(@\{0,1\}[[:alnum:]][^[:space:]]*@[^[:space:]]*\)$/\1/p' | LC_ALL=C sort -u)"
  [ -n "$list" ] || { echo "snapshot-omp-deps: bun pm ls returned no dependencies" >&2; return 1; }
  printf '{\n  "omp": "%s",\n  "pending": false,\n  "dependencies": [\n' "$version"
  printf '%s\n' "$list" | awk 'BEGIN{n=0} {a[n++]=$0} END{for(i=0;i<n;i++) printf "    \"%s\"%s\n", a[i], (i<n-1?",":"")}'
  printf '  ]\n}\n'
}

case "$mode" in
  write)
    render > "$file"
    ;;
  check)
    # Pending is decided before listing. `bun pm ls` exits 1 when the install wrote no lockfile,
    # and that must not fail the first image build the snapshot is waiting on.
    if grep -q '"pending":[[:space:]]*true' "$file"; then
      echo "::warning::deploy/omp-deps.lock.json is pending: produce it with OMP_DEPS_MODE=write (OQ-176)." >&2
      actual="$(mktemp)"
      if render > "$actual"; then
        echo "Resolved omp dependencies:" >&2
        cat "$actual" >&2
      else
        echo "snapshot-omp-deps: bun pm ls could not list dependencies. The pending snapshot still passes." >&2
      fi
      rm -f "$actual"
      exit 0
    fi
    actual="$(mktemp)"
    trap 'rm -f "$actual"' EXIT
    render > "$actual"
    if ! diff -u "$file" "$actual"; then
      echo "snapshot-omp-deps: resolved omp dependencies differ from deploy/omp-deps.lock.json (above). Review, then regenerate the snapshot." >&2
      exit 1
    fi
    echo "snapshot-omp-deps: ok"
    ;;
  *) echo "unknown mode $mode" >&2; exit 2 ;;
esac
