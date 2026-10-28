#!/usr/bin/env bash
# Applies the GitHub presentation and security settings of Autonogrammer with the gh CLI (Phase 52).
#
#   bash scripts/github/apply-repo-settings.sh [--dry-run]            print every command, change nothing (default)
#   bash scripts/github/apply-repo-settings.sh --apply                run the commands
#   Options: --repo OWNER/NAME  --homepage URL  --description TEXT
#
# Environment: AUTONOGRAMMER_REPO (default swcstudiospace/OmpOS), AUTONOGRAMMER_HOMEPAGE (default the repository URL until the
# docs site exists). Authentication is gh's own (GH_TOKEN or `gh auth login`); this script never prints a token.
#
# Every command is idempotent: running --apply twice changes nothing the second time.
# Deliberately NOT done here: branch protection and rulesets (they could block the owner's fast-forward pushes
# to main; the optional minimal ruleset is described in docs/runbooks/github-setup.md), the social preview
# image, discussion categories and the About "include in the home page" toggles (no API exists).
# Merge methods (squash, merge commit, rebase) are left as they are today.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
repo="${AUTONOGRAMMER_REPO:-swcstudiospace/OmpOS}"
homepage="${AUTONOGRAMMER_HOMEPAGE:-}"
description="Web, Desktop & Android agent workspace on the Omp Engine using GSD-Core's USSR Stack. An ADE harness, Cursor-like. AGPL-3.0-or-later."
mode="dry-run"

# The 15 repository topics (lowercase letters, digits and hyphens; at most 20 are allowed by GitHub).
topics=(autonogrammer omp ai-agents coding-agent agent-workspace cursor-alternative ai-ide dotnet csharp avalonia blazor-webassembly aspnet-core mcp kanban agpl-3)

usage() {
  sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --apply) mode="apply" ;;
    --dry-run) mode="dry-run" ;;
    --repo) repo="${2:?--repo needs OWNER/NAME}"; shift ;;
    --homepage) homepage="${2:?--homepage needs a URL}"; shift ;;
    --description) description="${2:?--description needs text}"; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown argument '$1'" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

homepage="${homepage:-https://github.com/$repo}"
labels_file="$root/.github/labels.yml"
[ -f "$labels_file" ] || { echo "error: $labels_file not found" >&2; exit 1; }

if [ "$mode" = "apply" ]; then
  command -v gh >/dev/null || { echo "error: the gh CLI is not on PATH" >&2; exit 1; }
  gh auth status >/dev/null 2>&1 || { echo "error: gh is not authenticated (set GH_TOKEN or run gh auth login)" >&2; exit 1; }
  echo "Applying settings to $repo"
else
  echo "Dry run for $repo: no command is executed. Re-run with --apply to change the repository."
fi

# run prints the command; it executes only with --apply.
run() {
  printf '+'
  printf ' %q' "$@"
  printf '\n'
  if [ "$mode" = "apply" ]; then
    "$@"
  fi
}

echo "## About: description, homepage, features, merge hygiene"
run gh repo edit "$repo" \
  --description "$description" \
  --homepage "$homepage" \
  --enable-discussions \
  --enable-wiki=false \
  --delete-branch-on-merge

echo "## Topics (replaces the whole list, so repeating the command changes nothing)"
topic_args=()
for t in "${topics[@]}"; do
  topic_args+=(-f "names[]=$t")
done
run gh api -X PUT "repos/$repo/topics" "${topic_args[@]}"

echo "## Security: private vulnerability reporting, Dependabot alerts and security updates"
run gh api -X PUT "repos/$repo/private-vulnerability-reporting"
run gh api -X PUT "repos/$repo/vulnerability-alerts"
run gh api -X PUT "repos/$repo/automated-security-fixes"

echo "## Security: secret scanning and push protection"
run gh api -X PATCH "repos/$repo" \
  -f 'security_and_analysis[secret_scanning][status]=enabled' \
  -f 'security_and_analysis[secret_scanning_push_protection][status]=enabled'

echo "## Labels from .github/labels.yml (gh label create --force updates an existing label)"
label_count=0
while IFS='|' read -r name color desc; do
  [ -n "$name" ] || continue
  run gh label create "$name" --repo "$repo" --color "$color" --description "$desc" --force
  label_count=$((label_count + 1))
done < <(awk '
  function flush() { if (name != "") printf "%s|%s|%s\n", name, color, desc; name = ""; color = ""; desc = "" }
  /^- name:/ { flush(); sub(/^- name:[ ]*/, ""); name = $0; next }
  /^  color:/ { sub(/^  color:[ ]*/, ""); color = $0; next }
  /^  description:/ { sub(/^  description:[ ]*/, ""); desc = $0; next }
  END { flush() }
' "$labels_file")

if [ "$label_count" -eq 0 ]; then
  echo "error: no labels parsed from $labels_file" >&2
  exit 1
fi

echo "Done ($mode): $label_count labels, ${#topics[@]} topics."
echo "Manual steps with no API: social preview upload, discussion categories, About home-page toggles (docs/runbooks/github-setup.md)."
