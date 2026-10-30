# Phase 20, Plan 04 Summary: Avalonia Foundation (wave 4 records)

**Status: complete** at the gate (HEAD 9e5913d). Gate: bootstrap OK; 1596 tests passed, 0 failed, 0 skipped.

Wave 4 records the AT-SPI2 X11 evidence, the architecture rows and the open-question status for Phase 20.

## Success criteria (ROADMAP Phase 20)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Native Avalonia 12.1.4 project builds with warnings as errors | Met | Gate at 9e5913d; apps/Autonogrammer.Native |
| 2 | Presentation layer has no UI-framework reference | Met | Gate contract tests in tests/Autonogrammer.ArchitectureTests |
| 3 | Avalonia and Blazor heads both consume the presentation layer | Met | tests/Autonogrammer.Native.Tests/PresentationBindingTests.cs; gate at 9e5913d |
| 4 | Named controls; prompt, send and transcript exposed through AT-SPI2 | Met | docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt (prompt "Message", send "Send message", transcript "Session transcript") |
| 5 | Keyboard reachable, focus style, reduced motion | Met | tests/Autonogrammer.Native.Tests/KeyboardFocusTests.cs; tests/Autonogrammer.Native.Tests/ReducedMotionTests.cs |
| 6 | One token dictionary; at least 4.5:1 contrast | Met | tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs |
| 7 | Plugin host contracts; in-process sample plugin | Met | Gate at 9e5913d; plan record .planning/phases/20-avalonia-foundation/20-01-SUMMARY.md |

## Open items

- OQ-39: LiveSetting on session status is not confirmed; the element is hidden at idle. Owner: engineering. Route: docs/open-questions.md#OQ-39.

## Commits

- f73faed: Phase 20 Avalonia native shell and Phase 21 session chat (waves 1 to 3)
- 98fc3f2: Phase 20 AT-SPI2 X11 evidence, architecture rows and open-question status
- 9e5913d: gate-green HEAD used for the wave 4 records (commit subject covers ADE-04 to ADE-20 foundations)
