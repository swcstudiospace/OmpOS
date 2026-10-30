#!/usr/bin/env bash
# Prints the repository settings as one JSON document, for evidence (Phase 52). Read-only: only GET requests.
#
#   bash scripts/github/read-back.sh [--repo OWNER/NAME] [--out FILE]
#
# Sections: repo (About, features, merge settings, licence, security_and_analysis), topics, community_profile,
# private_vulnerability_reporting, automated_security_fixes, labels, rulesets, branch_protection_main.
# A section whose request fails (for example 404 "Branch not protected") is reported as {"error": "..."}.
# Save the result under docs/runbooks/evidence/ (the orchestrator owns that folder).
set -euo pipefail

repo="${AUTONOGRAMMER_REPO:-swcstudiospace/OmpOS}"
out=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --repo) repo="${2:?--repo needs OWNER/NAME}"; shift ;;
    --out) out="${2:?--out needs a file}"; shift ;;
    -h|--help) sed -n '2,9p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "error: unknown argument '$1'" >&2; exit 2 ;;
  esac
  shift
done

command -v gh >/dev/null || { echo "error: the gh CLI is not on PATH" >&2; exit 1; }

# section NAME ENDPOINT [JQ]: prints one JSON member, with the error text when the request fails.
section() {
  local name="$1" endpoint="$2" jq="${3:-.}" body
  if body="$(gh api "$endpoint" --jq "$jq" 2>&1)"; then
    printf '  "%s": %s' "$name" "$body"
  else
    printf '  "%s": {"error": %s}' "$name" "$(printf '%s' "$body" | head -n 1 | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().strip()))')"
  fi
}

emit() {
  printf '{\n  "repository": "%s",\n  "read_at": "%s",\n' "$repo" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  section repo "repos/$repo" '{full_name, description, homepage, visibility, private, default_branch, license, topics, has_issues, has_projects, has_wiki, has_discussions, delete_branch_on_merge, allow_squash_merge, allow_merge_commit, allow_rebase_merge, allow_auto_merge, security_and_analysis}'
  printf ',\n'
  section topics "repos/$repo/topics" '.names'
  printf ',\n'
  section community_profile "repos/$repo/community/profile" '{health_percentage, description, documentation, files: (.files | with_entries(.value |= (if . == null then null else (.html_url // .url) end))), content_reports_enabled, updated_at}'
  printf ',\n'
  section private_vulnerability_reporting "repos/$repo/private-vulnerability-reporting"
  printf ',\n'
  section automated_security_fixes "repos/$repo/automated-security-fixes"
  printf ',\n'
  section labels "repos/$repo/labels?per_page=100" '[.[] | {name, color, description}]'
  printf ',\n'
  section rulesets "repos/$repo/rulesets" '[.[] | {id, name, enforcement}]'
  printf ',\n'
  section branch_protection_main "repos/$repo/branches/main/protection" '{url, required_status_checks: (.required_status_checks != null), enforce_admins: .enforce_admins.enabled}'
  printf '\n}\n'
}

if [ -n "$out" ]; then
  emit > "$out"
  echo "wrote $out" >&2
else
  emit
fi
