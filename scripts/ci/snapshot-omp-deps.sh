#!/usr/bin/env bash
# Snapshot (or verify) the resolved top-level dependency list of the omp install.
# The omp npm package ships no lockfile, so `bun install` resolves ranges at image build time. This script turns
# the direct dependencies of the installed omp directory (package.json "dependencies" plus the resolved versions
# bun actually installed in node_modules) into a stable JSON list, so the build can fail when the
# resolved set drifts from the committed deploy/omp-deps.lock.json.
#
# render() derives the list itself instead of using `bun pm ls`: with a --production install bun writes no
# lockfile, and `bun pm ls` then refuses to list anything ("missing lockfile"). The direct-dependency set and
# versions are read from the installed node_modules, which the image always has by check time.
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
  version="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$dir/package.json" | head -n 1)"
  # Direct dependencies: the keys of package.json "dependencies". Dev-only and optional entries never reach
  # the --production install, so they are not part of the snapshot.
  local deps
  deps="$(sed -n '/"dependencies": *{/,/^[[:space:]]*}[, ]*$/p' "$dir/package.json" | sed -n 's/^[[:space:]]*"\([^"]*\)": .*/\1/p' | grep -vx 'dependencies' | sort -u)"
  [ -n "$deps" ] || { echo "snapshot-omp-deps: no dependencies found in $dir/package.json" >&2; return 1; }
  # Resolve each direct dependency to the version bun installed. Printed through a subshell pipeline so the
  # list stays line-separated across iterations.
  local list
  list="$(printf '%s\n' "$deps" | while read -r name; do
    [ -n "$name" ] || continue
    local pkg="$dir/node_modules/$name/package.json"
    [ -f "$pkg" ] || { echo "snapshot-omp-deps: $name is not installed in $dir/node_modules" >&2; exit 1; }
    local ver
    ver="$(sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' "$pkg" | head -n 1)"
    [ -n "$ver" ] || { echo "snapshot-omp-deps: cannot read version of $name" >&2; exit 1; }
    printf '%s@%s\n' "$name" "$ver"
  done)"
  list="$(printf '%s\n' "$list" | LC_ALL=C sort -u)"
  [ -n "$list" ] || { echo "snapshot-omp-deps: rendered dependency list is empty" >&2; return 1; }

  printf '{\n  "omp": "%s",\n  "pending": false,\n  "dependencies": [\n' "$version"
  printf '%s\n' "$list" | awk 'BEGIN{n=0} {a[n++]=$0} END{for(i=0;i<n;i++) printf "    \"%s\"%s\n", a[i], (i<n-1?",":"")}'
  printf '  ]\n}\n'
}

case "$mode" in
  write)
    render > "$file"
    ;;
  check)
    if grep -q '"pending":[[:space:]]*true' "$file"; then
      echo "::warning::deploy/omp-deps.lock.json is pending: produce it with OMP_DEPS_MODE=write (OQ-176)." >&2
      actual="$(mktemp)"
      if render > "$actual"; then
        echo "Resolved omp dependencies:" >&2
        cat "$actual" >&2
      else
        echo "snapshot-omp-deps: could not render the dependency list. The pending snapshot still passes." >&2
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
