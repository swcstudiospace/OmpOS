---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "04"
subsystem: plugin-pins, licensing, deploy-key
tags: [agent-swarm, agent-substrate, agpl, deploy-key]
dependency_graph:
  requires: []
  provides: [license-only-pins, deploy-key-checkout]
  affects: [78-03, 78-06]
tech_stack:
  added: []
  patterns: [LICENSE-only upstream commits, read-only deploy key, SSH strict-host-key checkout, no secret echo]
key_files:
  modified:
    - plugins/agent-swarm
    - plugins/agent-substrate
    - docs/licensing.md
    - docs/open-questions.md
    - docs/decisions/ADR-0018-licence-agpl-and-source-offer.md
    - .github/workflows/desktop-release.yml
decisions:
  - "Pins move only to the two named upstream commits ( agent-swarm#49, agent-substrate#221 ); no other submodule change rides along"
  - "Deploy key is read-only, scoped to swcstudiospace/substrate; secret value never recorded"
metrics:
  tasks: 2
  status: complete
---

# Phase 78 Plan 04: Plugin Licences + Deploy Key Summary

**One-liner:** Plugin pins moved by LICENSE-only upstream commits to AGPL-3.0-or-later, licence expectations aligned (OQ-102), private plugin checked out with the read-only `SUBSTRATE_DEPLOY_KEY` — root cause of the v0.17.0/v0.19.1 tag failures, fixed and proven by tag runs `44551891120` (rc.1) and `44554031690` (v0.20.0).

## Tasks Executed

**Task 1 (auto):** Verified submodule commits: `plugins/agent-swarm` = `59f12c8d3a28265652bb153b55157b1`, `plugins/agent-substrate` = `70da6d80109eaea1dfdf3c55dff8847e090b46d4`; each differs from its previous pin by one added LICENSE file only. `docs/licensing.md` + OQ-102 state AGPL-3.0-or-later for both; `bundle-plugins.sh` manifest + licence checks aligned.

**Task 2 (auto):** `desktop-release.yml` checkout step: with `SUBSTRATE_DEPLOY_KEY`, key (CRs stripped) written to a mode-600 temp file, Ed25519 host key of github.com verified against GitHub's published fingerprint, `GIT_SSH_COMMAND` with IdentitiesOnly + strict checking; with `SUBMODULE_TOKEN`, masked HTTPS header; neither → error on tag/rehearsal, warn otherwise. Secrets passed via `env:`, never echoed. Owner key-creation documented in `docs/runbooks/desktop-release.md` (78-05).

## Verification Performed

- `git ls-tree HEAD plugins/agent-swarm plugins/agent-substrate` — ran this batch: **exactly** `70da6d80...` (substrate) and `59f12c8d...` (swarm), matching must_haves
- Secret-echo grep `echo.*(SUBSTRATE_DEPLOY_KEY|SUBMODULE_TOKEN)` — cited from plan; **not re-run this batch** (no workflow lint here)
- Tag proof — cited from 78-VERIFICATION "Prior gaps closed": tag runs `44551891120` and `44554031690` both success (vs v0.17.0/v0.19.1 checkout failures)

## Deviations

None. No pin moved to any other commit; no force-push; plugin default branches untouched from this repo.

**Status:** 78-04 complete.
