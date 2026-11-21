---
phase: 77-hardening-docs-and-integration
plan: "06"
subsystem: release, version-bump, locks
tags: [version-bump, relock, locked-restore, 0.16.0]
dependency_graph:
  requires: [77-05]
  provides: [0.16.0-bump-commit]
  affects: [77-07, 77-10]
tech_stack:
  added: []
  patterns: [edit-version-first-then-relock, single last commit, locked-mode assert, ContractVersion untouched]
key_files:
  modified:
    - Directory.Build.props
    - deploy/api.Containerfile
decisions:
  - "ORDER: set VersionPrefix + ARG first, then relock once (82 locks embed [0.15.0, ), so relock-before-edit breaks locked restore)"
  - "Derived files (THIRD-PARTY-NOTICES, sbom.spdx.json) join the single commit only if their own check reports drift"
  - "Freeze point: later relocks only via another owner-decided cut; no lock touched after the bump commit"
metrics:
  tasks: 2
  status: gaps_found
---

# Phase 77 Plan 06: U1 Version Bump Summary

**Status:** gaps_found (superseded bookkeeping) — the 0.16.0 cut landed and locked restore passed at the time, but the "exactly one commit" history assertion cannot be re-established on the current tree (count is 3), because later cuts legitimately re-touched the version line.

## Tasks Executed

**Task 1 (auto):** Single bump commit set `<VersionPrefix>0.16.0</VersionPrefix>` in `Directory.Build.props` and `ARG AUTONOGRAMMER_VERSION=0.16.0` in `deploy/api.Containerfile` (`deploy/worker.Containerfile` carries no version ARG — asserted only, nothing invented). Relocked once on a clean tree. Anchor commit for the cut: `b234bb98` (feat(release): cut 0.16.0 and attach desktop packages). sbom drift regenerated `docs/legal/sbom.spdx.json` in the same commit per the plan rule.

**Task 2 (auto):** Post-bump assertions: `dotnet restore Autonogrammer.Linux.slnf --locked-mode` passed; `scripts/ci/check-changelog.sh` and `check-licences.sh` passed; `ContractVersion.cs` shows no post-scaffold touch (scaffold `fccd5f5a` lineage).

## Verification Performed (this batch, current HEAD `c04095ba`)

- `grep -m1 '<VersionPrefix>' Directory.Build.props` → **0.20.11** (seven further patch bumps landed after 0.16.0)
- `git log -S'<VersionPrefix>0.16.0' --format=%H -- Directory.Build.props | wc -l` → **3** commits: `b234bb98` (0.16.0 cut), `1f27b917` (land integration branch + desktop packages), `a0bb806c` (runtime 10.0.14 fix). The plan's "exactly 1" assertion was true at the 0.16.0 cut and is honestly **not** re-derivable today; later owners re-cut the version deliberately.
- `CHANGELOG.md` has `[0.16.0]` (2026-11-07) and `[0.20.0]` sections with compare links — present on HEAD.

## Deviations

- Later commit `1f27b917` re-touched the version line when the integration branch landed desktop packages, and `a0bb806c` bumped for the runtime fix. These are not faults; they retire the assertion, which is recorded instead of hidden.

**Status:** gaps_found (history-count criterion retired by superseding cuts).
