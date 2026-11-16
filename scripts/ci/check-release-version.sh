#!/usr/bin/env bash
# Resolves the release version from Directory.Build.props and, when a tag is in play, checks that tag.
# Both release.yml and desktop-release.yml call this script. A workflow_dispatch build has no tag: the
# version is VersionPrefix and the ancestry check does not run.
#
# Usage: bash scripts/ci/check-release-version.sh
# Environment:
#   TAG or, when GITHUB_REF_TYPE=tag, GITHUB_REF_NAME — the tag to check (vMAJOR.MINOR.PATCH[-prerelease])
#   GITHUB_SHA — the tagged commit (default HEAD)
#   GITHUB_ENV — when set, VERSION and BASE_VERSION are appended for later steps
#   AUTONOGRAMMER_PROPS_FILE — Directory.Build.props path (default: the repository file)
# Prints VERSION and BASE_VERSION. BASE_VERSION is the version without a prerelease suffix.
# Exits non-zero with one "::error::" line. A tag value is the only input that can appear in that line.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
props="${AUTONOGRAMMER_PROPS_FILE:-$root/Directory.Build.props}"

if [ ! -f "$props" ]; then
  echo "::error::$props not found" >&2
  exit 1
fi

prefix="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$props" | head -n 1)"
if [ -z "$prefix" ]; then
  echo "::error::Directory.Build.props has no <VersionPrefix>" >&2
  exit 1
fi

tag="${TAG:-}"
if [ -z "$tag" ] && [ "${GITHUB_REF_TYPE:-}" = "tag" ]; then
  tag="${GITHUB_REF_NAME:-}"
fi

version="$prefix"
base="$prefix"
if [ -n "$tag" ]; then
  if ! [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    echo "::error::tag '$tag' is not vMAJOR.MINOR.PATCH[-prerelease]" >&2
    exit 1
  fi
  version="${tag#v}"
  base="${version%%-*}"
  if [ "$prefix" != "$base" ]; then
    echo "::error::tag $tag does not match VersionPrefix $prefix in Directory.Build.props" >&2
    exit 1
  fi
  sha="${GITHUB_SHA:-HEAD}"
  if ! git -C "$root" merge-base --is-ancestor "$sha" origin/main; then
    echo "::error::the tagged commit is not reachable from origin/main" >&2
    exit 1
  fi
fi

if [ -n "${GITHUB_ENV:-}" ]; then
  printf 'VERSION=%s\nBASE_VERSION=%s\n' "$version" "$base" >> "$GITHUB_ENV"
fi
printf 'VERSION=%s\nBASE_VERSION=%s\n' "$version" "$base"
