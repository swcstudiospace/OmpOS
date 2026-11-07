---
phase: 74-dispatch-relay
plan: 74-06
subsystem: presentation
tags: [dispatch, presentation, avalonia, bunit, accessibility]
requires:
  - 74-01 Dispatch contract enums (DispatchFailureReason, DispatchCommandState)
provides:
  - Presentation Dispatch models: DispatchPageModel, DevicesViewModel, DeviceRowModel, DispatchCommandRowModel, DispatchApprovalModel, RemoteTaskStripModel, PairThisDeviceModel, DispatchPhaseMap
  - DispatchCopy with distinct text per phase, degradation and failure
  - IDispatchHost, IThisDeviceHost, FakeDispatchHost, NotConfiguredDispatchHost, DispatchHostException
  - Web components DispatchPageView, DispatchApprovalPanel, RemoteTaskStrip; Avalonia views DispatchPageView, DispatchApprovalView, RemoteTaskStripView; three ViewMapping cases
  - wwwroot/styles/dispatch.css (token colours, reduced-motion override)
affects: [74-08]
actuals:
  tokens: not measured
  tasks: 2
  commits:
    - 5311e685 feat(74-06) host, copy and phase (Task 1)
    - c126ab91 feat(74-06) view models and page model (Task 1)
    - 159f8db8 feat(74-06) dispatch.css (Task 2)
    - e137a231 feat(74-06) web components with bUnit tests (Task 2)
    - e0d9e0bd feat(74-06) Avalonia views and native view tests (Task 2)
    - b42dda7a fix(74-06) assertive failures and page tests from the fake host (Task 2)
tech-stack:
  added: []
  patterns:
    - Views bind Presentation models only; no view reads a host or a store
    - Failures announce through an assertive region; status changes through a polite one
    - One public type per file (MA0048)
key-files:
  created:
    - src/Autonogrammer.Presentation/Dispatch/ (host, copy, phase, degradation, view models, one type per file)
    - src/Autonogrammer.UI/Components/Dispatch/DispatchPageView.razor, DispatchApprovalPanel.razor, RemoteTaskStrip.razor
    - src/Autonogrammer.Native.Views/Views/Dispatch/ (three axaml and code-behind pairs)
    - src/Autonogrammer.UI/wwwroot/styles/dispatch.css
    - tests/Autonogrammer.Presentation.Tests/Dispatch/ (DispatchViewModelTests, DispatchPageModelTests)
    - tests/Autonogrammer.UI.Tests/Dispatch/DispatchComponentTests.cs
    - tests/Autonogrammer.Native.Tests/Dispatch/DispatchViewTests.cs
  modified:
    - src/Autonogrammer.Native.Views/ViewMapping.cs (three Dispatch cases; the unmapped default still throws)
    - src/Autonogrammer.UI/wwwroot/styles/autonogrammer.css (one @import line)
key-decisions:
  - "DispatchViewModel, listed in the plan's artifacts, is not a separate type. DispatchPageModel is the page container; no consumer needed a second type."
  - "Failures (an unreachable service, a failed command) announce through an assertive region in both heads. The web page keeps one polite region, for its transient announcement; the native status line is polite."
  - "Device and command lists render only when they have rows (web)."
requirements-completed: [AG-74-3, AG-74-17]
---

# Phase 74 plan 06 summary

Dispatch presentation for the web head and the Avalonia heads: the Devices and Dispatch page, the approval prompt and the Remote task strip. Every view binds a Presentation model; each state has its own fixed copy.

## Tests
- Gate `b42dda7a` (clean worktree, `bootstrap.sh`): `bootstrap: OK`; 14550 tests, 0 failed, 3 skipped; api, demo and artifact web smoke each exit 0.
- `--filter-class "*Dispatch*"` in `tests/Autonogrammer.UI.Tests`: 7 passed, 0 failed (`DispatchComponentTests`).
- `--filter-class "*DispatchView*"` in `tests/Autonogrammer.Native.Tests`: 7 passed, 0 failed (`DispatchViewTests`).
- `--filter-class "*Dispatch*"` in `tests/Autonogrammer.Presentation.Tests`: 30 passed. That filter also matches 11 pre-existing Dispatcher tests, so this plan adds 19.
- CssContractTests 32/32 and HeadMotionTests 6/6 passed before the follow-up commit. The follow-up does not touch CSS, and the gate run covers both.
- Format verified with `format --verify-no-changes` on every changed C# file.

## Deviations
- The plan's artifact list names `DispatchViewModel`. It is not created; `DispatchPageModel` holds the page state.
- The plan's acceptance says Presentation/Dispatch holds "the 8 files". The layout is one public type per file (MA0048), so the folder holds more files. The types are the listed ones, minus `DispatchViewModel`.
- No RED step is claimed. The first Presentation test build failed on analyzer errors in production code (CA1032, VSTHRD200, CA1062), not on the new tests. The Native and UI suites passed on their first run once analyzer errors were fixed.
- The first gate attempt on `e0d9e0bd` was cancelled at start. Its log is incomplete and no gate result is claimed for that commit. The cancel was to add the assertive failure region and the fake-host view tests (commit `b42dda7a`) before one gate on the final head.
- Review correction: "focus returns afterwards" is not built. Focus moves to the page heading after a cancel (OQ-503). AG-74-15 is withdrawn from this plan until that is decided.
- Review correction: Cancel and the approval controls use `Autonogrammer.Size.TargetMin`: 32 px on the desktop and the web, 44 px where the Android head overrides the token (OQ-505). No desktop 44 px target is claimed.
- Rename, revoke and send have ports and routes but no page controls (OQ-506).
- The native page has no transient-announcement source, because the Presentation model carries none. The web page takes an `Announcement` parameter. This is a parity gap; the native status line covers status changes.

## Not verified here
- Screen-reader behaviour (NVDA, VoiceOver, TalkBack): manual, blocked-external.
- Android layout and touch targets on a device: blocked-external (no Android SDK on this host).
- No live or service-gated test exists in this plan.

## Notes for 74-08
- `IAccountFeature` is not a blocker. It exists at `services/Autonogrammer.Api/Accounts/IAccountFeature.cs`, and `DispatchFeature` is registered at `services/Autonogrammer.Api/Dispatch/DispatchModule.cs:33`. Clients read feature keys from `GET /account` (ADR-0030:139). An earlier note calling it missing was wrong.
