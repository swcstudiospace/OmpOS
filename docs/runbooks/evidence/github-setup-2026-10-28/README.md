# GitHub setup read-back, 2026-10-28

**What this folder holds:** `read-back.json`, the state of the repository `swcstudiospace/OmpOS` as GitHub reported it at 2026-10-28T19:53:50Z, a minute after the fast-forward push of `main` to `9d4e0075` and the push of the tag `v0.15.0`.

## How it was produced

1. `bash scripts/github/apply-repo-settings.sh --apply` set the description, the homepage (<https://ompos-web.vercel.app>), the 15 topics, Discussions on, wiki off, delete-branch-on-merge, private vulnerability reporting, Dependabot alerts and security updates, secret scanning and push protection, and the 22 labels of `.github/labels.yml`. It creates no ruleset and no branch protection.
2. `bash scripts/github/read-back.sh` (GET requests only) printed the settings as one JSON document with the sections `repo`, `topics`, `community_profile`, `private_vulnerability_reporting`, `automated_security_fixes`, `labels`, `rulesets` and `branch_protection_main`. A section whose request fails is reported as `{"error": ...}`.

## What it shows

| Item | Value in `read-back.json` |
|---|---|
| Licence | `license.key` `agpl-3.0`, `spdx_id` `AGPL-3.0`: GitHub detects the licence of `LICENSE` |
| Community profile | `health_percentage` 100; readme, licence, contributing, code of conduct and pull request template present; `issue_template` is `null` (the repository has three issue forms; UNVERIFIED why the profile does not list them) |
| Features | Discussions on, wiki off, delete-branch-on-merge on |
| Security | private vulnerability reporting enabled; automated security fixes enabled and not paused; Dependabot security updates, secret scanning and push protection `enabled` |
| Topics | 15 |
| Labels | 26 (the 22 of `.github/labels.yml` plus GitHub's defaults) |
| Rulesets, branch protection | none, on purpose: the owner pushes fast-forward to `main` |

The release itself is not in this file: the tag `v0.15.0` (annotated, at `9d4e0075`) and the GitHub Release https://github.com/swcstudiospace/OmpOS/releases/tag/v0.15.0 were created by the `release` workflow (run 40758275112, 31 s) and read with `gh release view v0.15.0` (recorded 2026-10-28).

## What the owner still does by hand

GitHub offers no API for these (`docs/runbooks/github-setup.md`, "Manual steps"):

- Upload `assets/brand/ompos-social-preview.png` as the social preview (OQ-170).
- Create the discussion categories (OQ-109).
- Optionally tag the older milestones `v0.9.1` to `v0.14.0` so the `CHANGELOG.md` compare links resolve (OQ-108).

Not covered here: the `ci` workflow failed for both pushes at 'Start test services' (rootless podman in the runner container, OQ-168); the local gate log `docs/runbooks/evidence/bootstrap-gate-2026-10-28-e24e6851.log` is the gate of record.
