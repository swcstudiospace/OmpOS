---
phase: 77-hardening-docs-and-integration
plan: "04"
subsystem: accessibility, ui-tests, native-tests, docs
tags: [accessibility, a11y-matrix, bunit, headless, axe]
dependency_graph:
  requires: [77-01]
  provides: [accessibility-matrix, cowork-surface-tests, headless-accessibility-tests]
  affects: [77-10]
tech_stack:
  added: []
  patterns: [matrix cell cites test or file or cannot-be-checked-here, view-code-only fixes with regression tests, existing 66.3 axe harness extended not replaced]
key_files:
  created:
    - docs/quality/v0.16.0-accessibility-pass.md
    - tests/Autonogrammer.UI.Tests/CoworkSurfaceAccessibilityTests.cs
    - tests/Autonogrammer.Native.Tests/CoworkHeadlessAccessibilityTests.cs
  modified:
    - scripts/web-smoke/a11y/routes.mjs
    - scripts/web-smoke/a11y/states.mjs
    - docs/accessibility/known-issues.md
decisions:
  - "Screen readers stay not-checked (OQ-483); matrix says so explicitly, never claims a live run"
  - "No service-side edits here (security plan owns shared files); view-code defects only, each with a named regression test"
  - "axe already runs in scripts/web-smoke/run-smoke.sh; this plan extends routes/states rows, it does not add an axe step"
metrics:
  tasks: 2
  status: gaps_found
---

# Phase 77 Plan 04: Accessibility Pass Summary

**Status:** gaps_found — matrix and test classes exist on HEAD, but the matrix still carries Pending/Not-established cells (honest, no empty cells), and no recorded axe/a11y gate-run output includes this milestone's surfaces yet.

## Tasks Executed

**Task 1 (auto, tdd=true):** Matrix skeleton `docs/quality/v0.16.0-accessibility-pass.md` (frontmatter, H1, Status line): 7 surfaces (Cowork shell/switch, account menu/sign-in, dock tab, approval prompt, artifact panel, Devices/Dispatch views) × 7 checks (accessible name+role, keyboard no trap + visible focus, focus return, aria-live assertive, theme-token contrast, reduced motion, 44px Android touch target). New test classes created: `tests/Autonogrammer.UI.Tests/CoworkSurfaceAccessibilityTests.cs` (bUnit) and `tests/Autonogrammer.Native.Tests/CoworkHeadlessAccessibilityTests.cs` (Avalonia.Headless, `asserts SubWithin` style). Both files exist on HEAD.

**Task 2 (auto):** View-code defect fixes with regression tests; every matrix cell cites a test class or file, or states cannot-be-checked-here with reason. `scripts/web-smoke/a11y/routes.mjs` / `states.mjs` gained rows for the new surfaces. New findings added to `docs/accessibility/known-issues.md`.

## Verification Performed

- `grep -c "cannot-be-checked"` on the matrix: **0** — observed cells use Pending / Not established / Cannot be checked here wording, i.e. no empty cell (`| |`) observed (spot-read of Devices-view row confirms citations per cell)
- Files exist on HEAD: matrix, both new test classes, `ThemeContrastTests`, `HeadMotionTests`, `WebShellAccessibilityTests` all present
- `git log --oneline -3 -- docs/quality/v0.16.0-accessibility-pass.md` tip `0d1f6f37` (snapshot v0.16.0)

## Remaining Gaps (honest, not hidden)

- Matrix cells on populated-fixture surfaces (Devices view, artifact dismissal) read Pending / Not established: they name the missing fixture/browser run, not a failure to write.
- No recorded `bash scripts/web-smoke/run-smoke.sh --a11y-report <dir>` output for this milestone's surfaces exists on HEAD (gate of 77-07/77-10).
- Screen readers: not checked (OQ-483) — remains a human-verification item.

**Status:** gaps_found.
