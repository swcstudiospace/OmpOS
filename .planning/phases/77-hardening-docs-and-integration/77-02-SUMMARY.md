---
phase: 77-hardening-docs-and-integration
plan: "02"
subsystem: docs, architecture-tests, parity-gate
tags: [parity, cowork-audit, tdd, gate]
dependency_graph:
  requires: [77-01]
  provides: [parity-counts-script, cowork-audit-parity-part, check-docs-parity-gate]
  affects: [77-09, 77-10]
tech_stack:
  added: []
  patterns: [RED fixtures per D-12 failure mode, partial class CoworkAuditTests, stdlib-only parity script, first-word status counting with bracketed qualifier]
key_files:
  created:
    - scripts/docs/parity_counts.py
    - tests/Autonogrammer.ArchitectureTests/CoworkAuditTests.cs
  modified:
    - scripts/ci/check-docs.sh
decisions:
  - "Parity_counts.py is stdlib-only; --check compares the counts block in docs/parity/cowork-parity.md and exits 1 on mismatch"
  - "CoworkAuditTests declares the public sealed partial class; Security and Docs parts extend it and reuse its table reader and RepoRoot helper"
  - "No MET-LIVE row is accepted on the real file per D-14 (OD-07 not approved)"
metrics:
  tasks: 2
  status: complete
---

# Phase 77 Plan 02: Parity Audit Gate Summary

**One-liner:** Delivered the Cowork parity machine gate: `scripts/docs/parity_counts.py` (stdlib, `--check`), wired into `scripts/ci/check-docs.sh`, plus the parity part of `CoworkAuditTests` with one RED fixture per D-12 failure mode; `docs/parity/cowork-parity.md` graded with 51 rows (C1-C31, K01-K20) and no MET-LIVE.

## Tasks Executed

**Task 1 (auto, tdd=true):** RED fixtures for every D-12 failure mode. STOP gate (`git ls-files docs/parity/cowork-parity.md`) passed — the file exists on the post-77-01 tree. Declared `public sealed partial class CoworkAuditTests` in `tests/Autonogrammer.ArchitectureTests/CoworkAuditTests.cs`; fixtures live under `tests/Autonogrammer.ArchitectureTests/Fixtures/CoworkAudit/` (outside `docs/`, so `check_gitbook.py` never sees them): `bad-empty-cell.md`, `bad-status.md`, `bad-empty-evidence.md`, `bad-empty-gap-nonmet.md`, `bad-met-no-class.md`, `bad-met-live-no-path.md`, `bad-counts-mismatch.md`, plus a good fixture. Root commit touching the file: `ef479d72` (test(77-02): RED fixtures for every D-12 failure mode + partial CoworkAuditTests base).

**Task 2 (auto):** Implemented `scripts/docs/parity_counts.py` (argparse/re/sys/pathlib only): parses the parity tables by first status word, tolerant of one bracketed qualifier, emits the counts block, `--check` exits 0 on match. Wired `python3 scripts/docs/parity_counts.py --check` into `scripts/ci/check-docs.sh` (after the gitbook call). `CoworkAuditTests` parity part replicates the `--check` comparison and greps `tests/` for each MET evidence class name; GREEN on the real file.

## Verification Performed

- `python3 scripts/docs/parity_counts.py --check` → `OK: parity counts match` (observed in this batch, exit 0)
- `bash scripts/ci/check-docs.sh` → `224 markdown files, 121 listed in SUMMARY.md, 0 errors, 0 warnings` (observed in this batch)
- Fixture dir present on HEAD: 10 fixture files including all 7 D-12 bad cases and 3 docs fixtures (bad-docs-*)
- `git log --oneline -3 -- scripts/docs/parity_counts.py` → tip `0d1f6f37` (snapshot pre-autonomous-run, v0.16.0); parity test tip `ef479d72`

## Deviations from Plan

None recorded. Shared helpers (Markdown table reader, `RepoRoot` via `ProjectGraphChecker.FindRepoRoot()`) are owned here and reused by `CoworkAuditTests.Security.cs` and `CoworkAuditTests.Docs.cs` via the partial class — confirmed present on HEAD.

## Open Items

Split-redo: 77-09 later re-ran the same gap (parity gate) as gap-closure (see 77-09-SUMMARY). The elevation page `docs/quality/v0.16.0-parity-audit-elevation.md` was planned here and is **missing** on HEAD; see 77-VERIFICATION gaps.

**Status:** 77-02 complete (gate green on HEAD); elevation gap tracked in 77-VERIFICATION.
