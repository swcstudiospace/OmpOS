---
phase: 78-desktop-release-pipeline-and-per-machine-windows-installer
plan: "03"
subsystem: release-pipeline, asset-verifier
tags: [release-yml, desktop-release, verifier, rehearsal]
dependency_graph:
  requires: [78-01, 78-04]
  provides: [draft-first-release, gated-desktop-release, asset-verifier]
  affects: [78-05, 78-06]
tech_stack:
  added: []
  patterns: [draft-then-publish, version_suffix rehearsal mode, sha256sum-compatible verifier, repo-checks gating]
key_files:
  modified:
    - .github/workflows/release.yml
    - .github/workflows/desktop-release.yml
    - scripts/release/package-desktop.sh
  created:
    - scripts/release/verify_desktop_assets.py
    - scripts/release/test_verify_desktop_assets.py
decisions:
  - "release.yml never publishes (gh release create --draft); desktop-release release job publishes last behind the six-file verifier"
  - "workflow_dispatch with version_suffix is a rehearsal: no release created or touched"
metrics:
  tasks: 2
  status: complete
---

# Phase 78 Plan 03: Release Pipeline + Verifier Summary

**One-liner:** Draft-first `release.yml`, gated `desktop-release.yml` (three legs + Windows smoke + macOS ad-hoc codesign), and `verify_desktop_assets.py` with its 58 tests wired into `scripts/ci/repo-checks.sh`.

## Tasks Executed

**Task 1 (auto, tdd):** Verifier + tests: six files present/non-empty/right-version/magic-bytes/installer >1MiB → exit 0 with `SHA256SUMS-desktop` written and re-read; each defect class (missing/empty/wrong-version/unexpected/too-small/wrong-signature/truncated archive) → exit 1 with one `::error::` line; bad usage → exit 2. 58 cases in `test_verify_desktop_assets.py`; `repo-checks.sh` invokes it before settings parity.

**Task 2 (auto):** Workflows: `release.yml` creates draft (`gh release create "$TAG" --draft --verify-tag`) and attaches images/SBOM/checksums to the draft only. `desktop-release.yml` builds linux-x64/win-x64/osx-arm64 legs, per-leg `verify_desktop_assets.py --platform`, Windows smoke on windows-2025, private-plugin checkout prefers `SUBSTRATE_DEPLOY_KEY` over `SUBMODULE_TOKEN`, `version_suffix` validated `[0-9A-Za-z.-]+`, release job tag-only `needs: package`, bounded wait for draft + release.yml assets, publish (`--latest` / `--prerelease` for suffix tags) last. No step echoes either secret.

## Verification Performed

- `python3 scripts/release/test_verify_desktop_assets.py` — ran this batch: **Ran 58 tests, OK** (1.512s)
- `bash scripts/ci/check-docs.sh` — ran this batch: 224 files, 0 errors
- `bash scripts/ci/repo-checks.sh` — cited from 78-VERIFICATION criterion 1 (`repo-checks: ok`); **not re-run this batch** (full gate out of scope)
- Workflow YAML + `--draft` + `SUBSTRATE_DEPLOY_KEY` presence — cited from 78-VERIFICATION criterion 4 run `44524125822` (all three legs success), not re-probed this batch

## Deviations

None. Workflow proof is the rehearsal/rc runs (78-06), per plan rule.

**Status:** 78-03 complete.
