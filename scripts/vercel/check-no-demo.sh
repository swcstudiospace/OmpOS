#!/usr/bin/env bash
# Fails when a published web bundle contains Demo code or the Demo settings file (ADR-0017).
#
# A real (non-Demo) bundle must answer every API route through the API origin. The Demo variant is a compile-time switch
# (AutonogrammerDemo in apps/Autonogrammer.Web): Demo/** is only compiled into a Demo bundle, so its type names must be absent from a
# real one. Type names live in the metadata string heap of the published assembly as plain UTF-8, so a byte search finds them.
#
# Usage:  scripts/vercel/check-no-demo.sh <bundle-dir>          exit 0 = no Demo code found; exit 1 = Demo code or Demo settings found
#         scripts/vercel/check-no-demo.sh --expect-demo <bundle-dir>    the inverse, used as the positive control: exit 0 only when the Demo markers ARE present
set -euo pipefail

expect_demo=0
if [[ "${1-}" == "--expect-demo" ]]; then
  expect_demo=1
  shift
fi

dir="${1-}"
[[ -n "$dir" && -d "$dir" ]] || { printf 'check-no-demo: usage: check-no-demo.sh [--expect-demo] <bundle-dir>\n' >&2; exit 2; }

# Type names of the Demo handler and its fixtures; a rename of those types must update this list (the self-test below fails otherwise).
markers=(DemoApiHandler DemoFixtures DemoRegistration)

found=0
if [[ -e "$dir/appsettings.demo.json" ]]; then
  found=1
fi

if [[ -d "$dir/_framework" ]]; then
  while IFS= read -r -d '' file; do
    for marker in "${markers[@]}"; do
      if grep -a -q -- "$marker" "$file"; then
        found=1
      fi
    done
  done < <(find "$dir/_framework" -type f \( -name '*.wasm' -o -name '*.dll' \) -print0)
fi

if [[ "$expect_demo" -eq 1 ]]; then
  if [[ "$found" -eq 1 ]]; then
    printf 'check-no-demo: Demo markers present, as expected for a Demo bundle\n'
    exit 0
  fi
  printf 'check-no-demo: expected Demo markers but found none in %s\n' "$dir" >&2
  exit 1
fi

if [[ "$found" -eq 1 ]]; then
  printf 'check-no-demo: %s carries Demo code or appsettings.demo.json; a real bundle must not\n' "$dir" >&2
  exit 1
fi

printf 'check-no-demo: %s carries no Demo code\n' "$dir"
