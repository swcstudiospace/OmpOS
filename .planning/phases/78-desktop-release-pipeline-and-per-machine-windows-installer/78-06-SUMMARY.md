---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "06"
subsystem: integration, release-evidence, verification
tags: [pr, rehearsal, rc-release, human-checklist, 78-verification]
dependency_graph:
  requires: [78-01, 78-02, 78-03, 78-04, 78-05]
  provides: [78-verification, v0.20.0-rc.1, v0.20.0]
  affects: [release]
tech_stack:
  added: []
  patterns: [rehearsal-first, rc-then-release, observed-evidence-only, human-verification-not-claimed]
key_files:
  created:
    - docs/runbooks/evidence/v0.20.0-release-2026-11-19/README.md
    - .planning/phases/78-desktop-release-pipeline-and-per-machine-windows-installer/78-VERIFICATION.md
decisions:
  - "No tag before the rehearsal is green and SUBSTRATE_DEPLOY_KEY exists; no tag without the owner's instruction; no force-push or release deletion to hide failure"
  - "78-VERIFICATION status follows observed evidence only; Windows/macOS human installs stay human-verification items until recorded"
metrics:
  tasks: 4
  status: complete
---

# Phase 78 Plan 06: Integration + Release Evidence Summary

**One-liner:** PR → CI green → rehearsal `44524125822` success → `v0.20.0-rc.1` (6 desktop files verified) → `v0.20.0` (same assets verified) → `78-VERIFICATION.md` written from observations; human checklist handed over as human-verification item.

## Tasks Executed

**Task 1 (checkpoint:human-action):** PR from `fix/phase35-review-closure` with template completed; local `repo-checks.sh` + verifier tests run first; CI waited to green on required checks; threads answered. Merged only on owner instruction.

**Task 2 (checkpoint:human-action):** `gh workflow run desktop-release.yml --ref fix/phase35-review-closure -f version_suffix=rehearsal.1` with `SUBSTRATE_DEPLOY_KEY` present; all three legs pass (private-plugin checkout, `bundle-plugins.sh` tag gates, per-leg verifier, Windows installer smoke test). Run id **44524125822** recorded on the evidence page.

**Task 3 (checkpoint:human-action):** On owner instruction: `git tag -s v0.20.0-rc.1`, watched `release` + `desktop-release`; release stayed hidden draft until all assets attached, then published pre-release. Downloaded all assets: six desktop files + `SHA256SUMS-desktop` + image archives + `SHA256SUMS-images` + SBOM; every size > 0 (54.6M–111.8M); `verify_desktop_assets.py --dir <dir> --version 0.20.0-rc.1` → `6 files verified` exit 0; `sha256sum -c` pass. Tag proof `44551891120` success.

**Task 4 (checkpoint:human-verify):** rc link + install.md checklist handed to owner for real Windows + macOS; `v0.20.0` tagged the same way, same asset check with `--version 0.20.0` (tag proof `44554031690` success); `78-VERIFICATION.md` written with honest status: **PASS** on the six ROADMAP criteria with the two manual installs as human-verification items.

## Verification Performed (cited, not re-run this batch)

78-VERIFICATION.md itself is the evidence record (criterion-by-criterion, observed values, run ids) and was **confirmed current** on HEAD `c04095ba` — it remains the authoritative file. `docs/runbooks/evidence/v0.20.0-release-2026-11-19/` exists on disk (`README.md` recorded). Live `gh` calls were not re-run in this docs batch (branded as evidence-stable); no run id, date or asset list was fabricated here.

## Open (by design, not gaps)

- Human checklist result unrecorded (owner action; AG-78-11).
- Next: owner records the checklist, then any follow-on release.

**Status:** 78-06 complete.
