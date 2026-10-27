#!/usr/bin/env bash
# Validates CHANGELOG.md against Keep a Changelog 1.1.0 and Semantic Versioning (Phase 52).
# Usage: bash scripts/ci/check-changelog.sh [path-to-CHANGELOG.md]
# Environment: OMPOS_PROPS_FILE overrides the Directory.Build.props path (default: next to CHANGELOG.md).
# Checks:
#   1. the "# Changelog" title and a Keep a Changelog reference in the header;
#   2. a "## [Unreleased]" section;
#   3. every other "## [" heading is "## [x.y.z] - YYYY-MM-DD" (an optional -prerelease suffix is accepted) with a real date;
#   4. versions are strictly descending (no duplicates);
#   5. a link reference "[x.y.z]: https://..." for every version and for Unreleased;
#   6. VersionPrefix in Directory.Build.props (when present) has a matching section, or is newer than every
#      section and [Unreleased] has entries.
# Exits non-zero with one "error:" line per problem.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
changelog="${1:-$root/CHANGELOG.md}"
props="${OMPOS_PROPS_FILE:-$(dirname "$changelog")/Directory.Build.props}"
errors=0

fail() {
  echo "error: $*" >&2
  errors=$((errors + 1))
}

[ -f "$changelog" ] || { echo "error: $changelog not found" >&2; exit 1; }

# 1. Header
first_title="$(grep -m1 -n '^# ' "$changelog" || true)"
case "$first_title" in
  *"# Changelog") ;;
  *) fail "the first heading must be '# Changelog'" ;;
esac
header_end="$(grep -m1 -n '^## ' "$changelog" | cut -d: -f1 || true)"
header_end="${header_end:-0}"
if ! head -n "$header_end" "$changelog" | grep -q 'keepachangelog.com'; then
  fail "the header must reference Keep a Changelog (https://keepachangelog.com/en/1.1.0/)"
fi
if ! head -n "$header_end" "$changelog" | grep -qi 'semver.org'; then
  fail "the header must reference Semantic Versioning (https://semver.org/spec/v2.0.0.html)"
fi

# 2. Unreleased
grep -qx '## \[Unreleased\]' "$changelog" || fail "missing '## [Unreleased]' section"

# 3. Version headings
version_re='^## \[([0-9]+)\.([0-9]+)\.([0-9]+)(-[0-9A-Za-z.-]+)?\] - ([0-9]{4}-[0-9]{2}-[0-9]{2})$'
versions=()
while IFS= read -r line; do
  [ "$line" = '## [Unreleased]' ] && continue
  if [[ "$line" =~ $version_re ]]; then
    v="${BASH_REMATCH[1]}.${BASH_REMATCH[2]}.${BASH_REMATCH[3]}${BASH_REMATCH[4]}"
    d="${BASH_REMATCH[5]}"
    if ! date -u -d "$d" +%Y-%m-%d 2>/dev/null | grep -qx "$d"; then
      fail "version $v has an invalid date '$d'"
    fi
    versions+=("$v")
  else
    fail "heading '$line' is not '## [x.y.z] - YYYY-MM-DD'"
  fi
done < <(grep '^## \[' "$changelog" || true)

[ "${#versions[@]}" -gt 0 ] || fail "no version sections found"

# 4. Strictly descending: returns 0 when $1 is a lower precedence than $2
lower() {
  local a="$1" b="$2" a_core b_core a_pre b_pre
  a_core="${a%%-*}"
  b_core="${b%%-*}"
  a_pre=""
  b_pre=""
  [ "$a" != "$a_core" ] && a_pre="${a#*-}"
  [ "$b" != "$b_core" ] && b_pre="${b#*-}"
  if [ "$a_core" != "$b_core" ]; then
    [ "$(printf '%s\n%s\n' "$a_core" "$b_core" | sort -V | head -n1)" = "$a_core" ]
    return
  fi
  # Same core: a release is newer than any prerelease of it.
  if [ -n "$a_pre" ] && [ -z "$b_pre" ]; then return 0; fi
  if [ -z "$a_pre" ] && [ -n "$b_pre" ]; then return 1; fi
  [ "$(printf '%s\n%s\n' "$a_pre" "$b_pre" | sort -V | head -n1)" = "$a_pre" ] && [ "$a_pre" != "$b_pre" ]
}

previous=""
for v in "${versions[@]}"; do
  if [ -n "$previous" ]; then
    if [ "$v" = "$previous" ]; then
      fail "version $v appears twice"
    elif ! lower "$v" "$previous"; then
      fail "versions must be in descending order: $v is listed after $previous"
    fi
  fi
  previous="$v"
done

# 5. Link references
grep -Eq '^\[Unreleased\]: https?://' "$changelog" || fail "missing link reference '[Unreleased]: https://...'"
for v in "${versions[@]}"; do
  grep -Fq "[$v]: http" "$changelog" || fail "missing link reference '[$v]: https://...'"
done

# 6. VersionPrefix
if [ -f "$props" ]; then
  prefix="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$props" | head -n 1)"
  if [ -n "$prefix" ]; then
    matched=0
    for v in "${versions[@]}"; do
      [ "${v%%-*}" = "$prefix" ] && matched=1
    done
    if [ "$matched" -eq 0 ]; then
      newest="${versions[0]:-0.0.0}"
      if lower "$prefix" "${newest%%-*}" || [ "$prefix" = "${newest%%-*}" ]; then
        fail "VersionPrefix $prefix in $props has no section and is not newer than $newest"
      else
        unreleased_entries="$(awk '
          /^## \[Unreleased\]/ { in_u = 1; next }
          /^## \[/ { in_u = 0 }
          /^\[[^]]+\]: / { in_u = 0 }
          in_u && /^- / { n++ }
          END { print n + 0 }
        ' "$changelog")"
        [ "$unreleased_entries" -gt 0 ] || fail "VersionPrefix $prefix is newer than $newest but [Unreleased] has no entries"
      fi
    fi
  fi
fi

if [ "$errors" -gt 0 ]; then
  echo "check-changelog: $errors problem(s) in $changelog" >&2
  exit 1
fi
echo "check-changelog: $changelog is valid (${#versions[@]} versions)"
