#!/usr/bin/env bash
# Licence gate for OmpOS (AGPL-3.0-or-later). Checks, in this order:
#   1. LICENSE is the canonical GNU AGPL v3 text (length and SHA-256), and LICENSES/AGPL-3.0-or-later.txt is identical;
#   2. NOTICE exists and names the licence and the source URL;
#   3. THIRD-PARTY-NOTICES.md is fresh (scripts/legal/generate-third-party-notices.py --check; needs the NuGet cache,
#      so run it after bootstrap.sh or dotnet restore);
#   4. every directory holding font files has an OFL.txt;
#   5. plugins/gsd-core-omp/package.json declares license AGPL-3.0-or-later;
#   6. README.md has a License section that names AGPL-3.0-or-later;
#   7. Directory.Build.props holds PackageLicenseExpression AGPL-3.0-or-later;
#   8. no *.csproj sets a different PackageLicenseExpression or a PackageLicenseFile.
# Every failure is printed; the exit status is 1 when any check failed.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

EXPECTED_BYTES=34523
EXPECTED_SHA256=0d96a4ff68ad6d4b6f1f30f713b18d5184912ba8dd389f86aa7710db079abcb0
LICENCE_ID=AGPL-3.0-or-later
failures=0

fail() {
  echo "licence check failed: $*" >&2
  failures=$((failures + 1))
}

# 1. LICENSE text
if [[ ! -f LICENSE ]]; then
  fail "LICENSE is missing"
else
  head -n 1 LICENSE | grep -q 'GNU AFFERO GENERAL PUBLIC LICENSE' || fail "LICENSE does not start with the GNU AFFERO GENERAL PUBLIC LICENSE header"
  grep -q 'Version 3, 19 November 2007' LICENSE || fail "LICENSE is not version 3 of the GNU AGPL"
  actual_bytes=$(wc -c < LICENSE | tr -d ' ')
  [[ "$actual_bytes" == "$EXPECTED_BYTES" ]] || fail "LICENSE is $actual_bytes bytes, expected $EXPECTED_BYTES"
  actual_sha=$(sha256sum LICENSE | cut -d ' ' -f 1)
  [[ "$actual_sha" == "$EXPECTED_SHA256" ]] || fail "LICENSE sha256 is $actual_sha, expected $EXPECTED_SHA256"
  if [[ -f LICENSES/$LICENCE_ID.txt ]]; then
    cmp -s LICENSE "LICENSES/$LICENCE_ID.txt" || fail "LICENSES/$LICENCE_ID.txt differs from LICENSE"
  else
    fail "LICENSES/$LICENCE_ID.txt is missing"
  fi
fi

# 2. NOTICE
if [[ ! -f NOTICE ]]; then
  fail "NOTICE is missing"
else
  grep -q 'AGPL-3.0-or-later' NOTICE || fail "NOTICE does not name AGPL-3.0-or-later"
  grep -q 'https://github.com/swcstudiospace/OmpOS' NOTICE || fail "NOTICE does not give the source URL"
  grep -q 'THIRD-PARTY-NOTICES.md' NOTICE || fail "NOTICE does not point at THIRD-PARTY-NOTICES.md"
fi

# 3. Generated third-party notices
if ! python3 scripts/legal/generate-third-party-notices.py --check; then
  fail "THIRD-PARTY-NOTICES.md is stale or a licence is undetermined (see the message above)"
fi

# 4. Fonts need their OFL.txt
while IFS= read -r font_dir; do
  [[ -f "$font_dir/OFL.txt" ]] || fail "font directory without OFL.txt: $font_dir"
done < <(find . \( -name .git -o -name bin -o -name obj -o -name node_modules -o -path ./plugins/claude-ultrathink -o -path ./plugins/agent-swarm -o -path ./plugins/agent-substrate \) -prune -o \
  -type f \( -iname '*.ttf' -o -iname '*.otf' -o -iname '*.woff' -o -iname '*.woff2' \) -printf '%h\n' | sort -u)

# 5. First-party npm package
plugin_licence=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("license", ""))' plugins/gsd-core-omp/package.json)
[[ "$plugin_licence" == "$LICENCE_ID" ]] || fail "plugins/gsd-core-omp/package.json license is '$plugin_licence', expected $LICENCE_ID"

# 6. README
grep -q '^## License' README.md || fail "README.md has no License section"
grep -q "$LICENCE_ID" README.md || fail "README.md does not mention $LICENCE_ID"

# 7. Package metadata
grep -q "<PackageLicenseExpression>$LICENCE_ID</PackageLicenseExpression>" Directory.Build.props || fail "Directory.Build.props does not set PackageLicenseExpression $LICENCE_ID"

# 8. No project overrides the licence
while IFS= read -r project; do
  if grep -E '<PackageLicenseExpression>' "$project" | grep -vq "<PackageLicenseExpression>$LICENCE_ID</PackageLicenseExpression>"; then
    fail "$project sets a different PackageLicenseExpression"
  fi
  if grep -q '<PackageLicenseFile>' "$project"; then
    fail "$project sets PackageLicenseFile, which conflicts with PackageLicenseExpression"
  fi
done < <(find . \( -name .git -o -name bin -o -name obj -o -name node_modules \) -prune -o -type f -name '*.csproj' -print | sort)

if ((failures > 0)); then
  echo "$failures licence check(s) failed" >&2
  exit 1
fi
echo "licence checks passed"
