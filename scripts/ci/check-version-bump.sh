#!/usr/bin/env bash
# Fails a pull request unless its VersionPrefix is greater than the base branch's and
# CHANGELOG.md has a section for that version. Every merge into main pins a new version,
# so this check makes the pin a merge requirement rather than a convention.
#
# Usage: scripts/ci/check-version-bump.sh --base <git-ref>
set -eu

base=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --base)
      [ "$#" -ge 2 ] || { echo "check-version-bump: --base needs a git ref" >&2; exit 2; }
      base="$2"
      shift 2
      ;;
    *)
      echo "check-version-bump: unknown argument: $1" >&2
      exit 2
      ;;
  esac
done
[ -n "$base" ] || { echo "usage: check-version-bump.sh --base <git-ref>" >&2; exit 2; }

# Prints the VersionPrefix declared in Directory.Build.props at a git ref.
version_at() {
  git show "$1:Directory.Build.props" | awk -F'[<>]' '/<VersionPrefix>/ { print $3; exit }'
}

base_version="$(version_at "$base")"
head_version="$(version_at HEAD)"
for version in "$base_version" "$head_version"; do
  if ! printf '%s\n' "$version" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$'; then
    echo "check-version-bump: VersionPrefix is not MAJOR.MINOR.PATCH (base '$base_version', head '$head_version')" >&2
    exit 1
  fi
done

newest="$(printf '%s\n%s\n' "$base_version" "$head_version" | sort -V | tail -n 1)"
if [ "$head_version" = "$base_version" ] || [ "$newest" != "$head_version" ]; then
  echo "check-version-bump: VersionPrefix must rise above the base. Base $base_version, head $head_version. Pin the next version in Directory.Build.props and add its CHANGELOG section." >&2
  exit 1
fi

if ! grep -Fq "## [$head_version] - " CHANGELOG.md; then
  echo "check-version-bump: CHANGELOG.md has no '## [$head_version] - <date>' section for the head version." >&2
  exit 1
fi

echo "check-version-bump: $base_version -> $head_version, with its CHANGELOG section."
