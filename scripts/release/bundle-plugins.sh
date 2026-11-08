#!/usr/bin/env bash
# Copies the omp plugins into one desktop publish folder at the commits the repository pins, so every package carries them.
#
# Usage: bash scripts/release/bundle-plugins.sh <publish-dir> <repo-root> <tag-or-empty>
#
#   <publish-dir>    The publish folder (publish/<rid>). The plugins land in <publish-dir>/plugins/<dir>, beside the executable.
#                    The folder must not already contain plugins/.
#   <repo-root>      The repository checkout. Each plugins/<dir> is a submodule checked out at its pinned commit, or an in-repo
#                    plugin. The plugin list is <repo-root>/packaging/plugins/manifest.json.
#   <tag-or-empty>   The release tag vMAJOR.MINOR.PATCH[-prerelease] for a tag build; empty for a workflow_dispatch build.
#
# Each manifest entry has dir, source ("in-repo" or the upstream URL), private, requiredForRelease, expectedLicenseFile,
# licenceAcceptedWithoutFile and an optional licenseNote (text for people; the script does not read it).
#
# For each entry, in this order:
#   1. Presence. The directory must be non-empty, and a submodule must have its .git. A missing plugin fails a tag build when
#      requiredForRelease is true. Otherwise it is skipped with a ::warning::, and the build goes on without it.
#   2. Pin. A submodule's HEAD must equal the commit the repository records for plugins/<dir> (git ls-tree HEAD). An in-repo
#      plugin must be a tracked directory at HEAD. A mismatch fails the build in every mode.
#   3. Clean. A submodule must have no local changes, and an in-repo plugin no uncommitted or untracked files under its
#      directory. Fails in every mode.
#   4. Copy. cp -a plugins/<dir> into <publish-dir>/plugins/<dir>. Then these are removed from the copy, at any depth:
#        .git                      a file or a directory
#        node_modules              a directory
#        test, tests, __tests__    directories
#      A symlink or special file left in the copy fails the build, because package-deb.sh and the Windows installer cannot
#      carry one. Nothing is installed.
#   5. Licence (tag builds, requiredForRelease true only). LICENSE or LICENSE.md must be at the root of the copy. When it is
#      missing, the build fails if expectedLicenseFile is true. If expectedLicenseFile is false, the build fails unless
#      licenceAcceptedWithoutFile is true for that entry, which is the owner's decision record.
#   6. Runtime dependencies. needsInstall is true when a package.json in the copy has a non-empty dependencies object. A
#      ::notice:: names those files. This script installs nothing.
#
# Output: <publish-dir>/plugins/bundle.json, an array of {dir, source, commit, licenseFile, needsInstall}. For a submodule,
# commit is the pinned commit. For an in-repo plugin it is the commit of <repo-root>, which carries the plugin.
# A summary table goes to $GITHUB_STEP_SUMMARY when that variable is set. No credential is read or printed.
# Exit status: 0 when every plugin is bundled or skipped as allowed; 1 when a check fails (each failure is a ::error:: line that
# names the plugin); 2 for bad usage.
# Needs bash, git and jq. The GitHub-hosted runners have all three, including the Windows leg (Git Bash).
set -euo pipefail

usage() {
  echo "usage: bash scripts/release/bundle-plugins.sh <publish-dir> <repo-root> <tag-or-empty>" >&2
  exit 2
}

die() {
  echo "error: $*" >&2
  exit 1
}

[ "$#" -eq 3 ] || { echo "error: expected 3 arguments, got $#" >&2; usage; }
publish="$1"
root="$2"
tag="$3"
[ -n "$publish" ] || die "the publish directory argument is empty"
[ -n "$root" ] || die "the repository root argument is empty"

mode=dispatch
if [ -n "$tag" ]; then
  if ! [[ "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
    die "tag '$tag' is not vMAJOR.MINOR.PATCH[-prerelease]"
  fi
  mode=tag
fi

command -v git >/dev/null 2>&1 || die "git is required"
command -v jq >/dev/null 2>&1 || die "jq is required"

[ -d "$root" ] || die "repository root not found: $root"
root="$(cd "$root" && pwd)"
[ -d "$publish" ] || die "publish directory not found: $publish (publish the app first)"
publish="$(cd "$publish" && pwd)"
[ ! -e "$publish/plugins" ] || die "$publish/plugins already exists; bundle into a clean publish directory"

manifest="$root/packaging/plugins/manifest.json"
[ -f "$manifest" ] || die "manifest not found: $manifest"
jq -e . "$manifest" >/dev/null 2>&1 || die "$manifest is not valid JSON"
jq -e '.plugins | type == "array" and length > 0' "$manifest" >/dev/null 2>&1 || die "$manifest needs a non-empty plugins array"

invalid="$(jq -r '
  .plugins[]
  | select(
      ((.dir | type) != "string")
      or ((.dir | type) == "string" and ((.dir | test("^[A-Za-z0-9][A-Za-z0-9._-]*$")) | not))
      or ((.source | type) != "string")
      or (.source == "")
      or ([.private, .requiredForRelease, .expectedLicenseFile, .licenceAcceptedWithoutFile] | any(type != "boolean"))
      or (.licenceAcceptedWithoutFile == true and .expectedLicenseFile == true)
    )
  | (.dir | tostring)
' "$manifest")"
if [ -n "$invalid" ]; then
  die "invalid entries in $manifest (dir is a plain name; source is a non-empty string; the four flags are booleans; licenceAcceptedWithoutFile true needs expectedLicenseFile false): $(printf '%s' "$invalid" | tr '\n' ' ')"
fi
duplicates="$(jq -r '[.plugins[].dir] | group_by(.) | map(select(length > 1) | .[0]) | .[]' "$manifest")"
if [ -n "$duplicates" ]; then
  die "duplicate plugin directories in $manifest: $(printf '%s' "$duplicates" | tr '\n' ' ')"
fi

errors=()
skipped=()
records=()
rows=()

fail_plugin() {
  errors+=("$1: $2")
  echo "::error::bundle-plugins: $1: $2"
}

skip_plugin() {
  skipped+=("$1: $2")
  echo "::warning::bundle-plugins: $1 is not bundled: $2"
}

mkdir -p "$publish/plugins"

while IFS=$'\t' read -r dir source private required expected accepted <&3; do
  src="$root/plugins/$dir"
  dest="$publish/plugins/$dir"
  in_repo=false
  if [ "$source" = "in-repo" ]; then
    in_repo=true
  fi

  reason=""
  if [ ! -d "$src" ] || [ -z "$(ls -A -- "$src" 2>/dev/null)" ]; then
    reason="plugins/$dir is missing or empty"
  elif [ "$in_repo" = false ] && [ ! -e "$src/.git" ]; then
    reason="plugins/$dir is not checked out (no .git)"
  fi
  if [ -n "$reason" ]; then
    if [ "$private" = true ]; then
      reason="$reason; the private submodule needs the SUBMODULE_TOKEN secret"
    fi
    if [ "$mode" = tag ] && [ "$required" = true ]; then
      fail_plugin "$dir" "$reason. A tag build needs every plugin whose requiredForRelease is true"
    else
      skip_plugin "$dir" "$reason"
    fi
    continue
  fi

  if [ "$in_repo" = true ]; then
    tree_entry="$(git -C "$root" ls-tree HEAD "plugins/$dir")"
    case "$tree_entry" in
      "040000 tree "*) ;;
      *)
        fail_plugin "$dir" "is not a tracked directory at HEAD. An in-repo plugin must be committed"
        continue
        ;;
    esac
    if ! commit="$(git -C "$root" rev-parse HEAD 2>&1)"; then
      die "git rev-parse HEAD failed in $root: $commit"
    fi
    if ! changes="$(git -C "$root" status --porcelain -- "plugins/$dir" 2>&1)"; then
      fail_plugin "$dir" "git status failed: $changes"
      continue
    fi
    if [ -n "$changes" ]; then
      fail_plugin "$dir" "has uncommitted or untracked changes, so its bundle would not equal the commit"
      continue
    fi
  else
    gitlink="$(git -C "$root" ls-tree HEAD "plugins/$dir")"
    case "$gitlink" in
      "160000 commit "*) ;;
      *)
        fail_plugin "$dir" "is not a submodule at HEAD, so the repository pins no commit for it"
        continue
        ;;
    esac
    read -r _ _ pinned _ <<< "$gitlink"
    if ! commit="$(git -C "$src" rev-parse HEAD 2>&1)"; then
      fail_plugin "$dir" "git rev-parse failed: $commit"
      continue
    fi
    if [ "$commit" != "$pinned" ]; then
      fail_plugin "$dir" "is checked out at $commit, but the repository pins $pinned"
      continue
    fi
    if ! changes="$(git -C "$src" status --porcelain 2>&1)"; then
      fail_plugin "$dir" "git status failed: $changes"
      continue
    fi
    if [ -n "$changes" ]; then
      fail_plugin "$dir" "has local changes, so its bundle would not equal the pinned commit"
      continue
    fi
  fi

  cp -a -- "$src" "$dest"
  find "$dest" \( -name .git -o -name node_modules \) -prune -exec rm -rf -- {} +
  find "$dest" -type d \( -name test -o -name tests -o -name __tests__ \) -prune -exec rm -rf -- {} +
  unusual="$(find "$dest" ! -type f ! -type d -print -quit)"
  if [ -n "$unusual" ]; then
    fail_plugin "$dir" "the copy holds a symlink or a special file (${unusual#"$publish/"}), which a package cannot carry"
    continue
  fi

  licence=false
  if [ -f "$dest/LICENSE" ] || [ -f "$dest/LICENSE.md" ]; then
    licence=true
  fi
  if [ "$licence" = false ]; then
    if [ "$mode" = tag ] && [ "$required" = true ]; then
      if [ "$expected" = true ]; then
        fail_plugin "$dir" "expectedLicenseFile is true, but the copy has no LICENSE or LICENSE.md at its root"
        continue
      fi
      if [ "$accepted" != true ]; then
        fail_plugin "$dir" "has no LICENSE or LICENSE.md at its root. Add one upstream (or here, for an in-repo plugin), or set licenceAcceptedWithoutFile to true in packaging/plugins/manifest.json with a decision record"
        continue
      fi
      echo "::notice::bundle-plugins: $dir ships without a LICENSE file, accepted by licenceAcceptedWithoutFile in the manifest"
    else
      echo "::warning::bundle-plugins: $dir has no LICENSE or LICENSE.md at its root (a tag build fails on this for a required plugin)"
    fi
  fi

  needs=false
  deps_list=""
  while IFS= read -r -d '' pj; do
    if ! jq -e . "$pj" >/dev/null 2>&1; then
      fail_plugin "$dir" "${pj#"$publish/"} is not valid JSON"
      continue
    fi
    if jq -e '(.dependencies // {}) | type == "object" and length > 0' "$pj" >/dev/null 2>&1; then
      needs=true
      deps_list="${deps_list}${deps_list:+, }${pj#"$dest/"}"
    fi
  done < <(find "$dest" -name package.json -type f -print0)
  if [ "$needs" = true ]; then
    echo "::notice::bundle-plugins: $dir declares runtime dependencies in $deps_list. Nothing is installed by this build."
  fi

  records+=("$(jq -cn --arg dir "$dir" --arg source "$source" --arg commit "$commit" --argjson licenseFile "$licence" --argjson needsInstall "$needs" '{dir: $dir, source: $source, commit: $commit, licenseFile: $licenseFile, needsInstall: $needsInstall}')")
  licence_text=no
  if [ "$licence" = true ]; then
    licence_text=yes
  fi
  needs_text=no
  if [ "$needs" = true ]; then
    needs_text=yes
  fi
  rows+=("| $dir | $source | ${commit:0:12} | $licence_text | $needs_text |")
done 3< <(jq -r '.plugins[] | [.dir, .source, (.private | tostring), (.requiredForRelease | tostring), (.expectedLicenseFile | tostring), (.licenceAcceptedWithoutFile | tostring)] | @tsv' "$manifest")

bundle_json="$publish/plugins/bundle.json"
if [ "${#records[@]}" -gt 0 ]; then
  printf '%s\n' "${records[@]}" | jq -s . > "$bundle_json"
else
  printf '[]\n' > "$bundle_json"
fi

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Plugins in this package ($mode build)"
    echo ""
    echo "| Plugin | Source | Commit | LICENSE at root | Needs install |"
    echo "| --- | --- | --- | --- | --- |"
    if [ "${#rows[@]}" -gt 0 ]; then
      printf '%s\n' "${rows[@]}"
    fi
    echo ""
    echo "In-repo plugins show the commit of this checkout. Needs install yes: the tree declares dependencies, and this build installed none."
    if [ "${#skipped[@]}" -gt 0 ]; then
      printf -- '- Not bundled: %s\n' "${skipped[@]}"
    fi
    if [ "${#errors[@]}" -gt 0 ]; then
      printf -- '- Failed: %s\n' "${errors[@]}"
    fi
  } >> "$GITHUB_STEP_SUMMARY"
fi

if [ "${#errors[@]}" -gt 0 ]; then
  die "${#errors[@]} plugin check(s) failed; the package must not ship without them (see the ::error:: lines)"
fi
echo "bundle-plugins: ${#records[@]} plugin(s) bundled into $publish/plugins ($mode build), ${#skipped[@]} skipped" >&2
