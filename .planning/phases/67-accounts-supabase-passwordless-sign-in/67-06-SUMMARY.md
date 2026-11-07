---
phase: 67-accounts-supabase-passwordless-sign-in
plan: "06"
subsystem: auth
tags: [avalonia, accounts, passwordless, accessibility]

requires:
  - phase: 67-01
    provides: IAccountSession, AuthPhase, AuthErrorKind, SignInCoordinator, AccountSessionManager
  - phase: 67-04
    provides: PkceSignInFlow, AccountSecretStoreSelector, AccountSettingsKeys, CrossProcessFileLock
provides:
  - Presentation AccountViewModel, AccountCopy and IAccountHost with no Accounts reference
  - Native AccountView and SignInDialog, unplaced pending phase 69 HeaderEndSlot
  - DesktopAccountHost and HeadServices token supplier (account token, env fallback when signed out)
affects: [67-07, 69, 70]

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Account UI binds only to Presentation.Accounts; the Native head adapts IAccountSession behind IAccountHost"
    - "AUTONOGRAMMER_API_TOKEN is the signed-out fallback only"

key-files:
  created:
    - src/Autonogrammer.Presentation/Accounts/AccountViewModel.cs
    - src/Autonogrammer.Presentation/Accounts/AccountCopy.cs
    - src/Autonogrammer.Presentation/Accounts/IAccountHost.cs
    - apps/Autonogrammer.Native/Composition/DesktopAccountHost.cs
    - src/Autonogrammer.Native.Views/Views/Account/AccountView.axaml
  modified:
    - apps/Autonogrammer.Native/Composition/HeadServices.cs
    - apps/Autonogrammer.Native/Settings/FileSettingsStore.cs

key-decisions:
  - "ICoworkAccountGate is absent; the gate implementation is a hand-off to phase 69 and was not invented."
  - "Account views are built and tested unplaced. Nothing was added to window chrome."
  - "Not-configured (missing API address, owner-token, development, or no client block) stays SignedOut with AccountCopy.NotConfigured; the GoTrue provider returns null."

patterns-established:
  - "One fixed AccountCopy sentence per AuthPhase and AuthErrorKind; notices are separate sentences, never server text."
  - "Devices opens only when GET /account features contains dispatch."

requirements-completed: [AG-67-7, AG-67-23]

coverage:
  - id: D1
    description: Five exclusive account states, offline as a SignedIn notice, consent only with SessionNotRemembered, devices only when the host reports dispatch.
    requirement: AG-67-23
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Accounts/AccountViewModelTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Native account panel names, focus, throttled announcements, reduced-motion spinner, and composition token and secret-store rows.
    requirement: AG-67-7
    verification:
      - kind: automated_ui
        ref: tests/Autonogrammer.Native.Tests/Account/AccountAccessibilityTests.cs
        status: unknown
    human_judgment: true
    rationale: Native.Tests could not be executed in this turn because another owner's uncommitted files (66-05 legacy discovery and workspace views) failed the views/abstractions build. The verifier must rerun the Native filter once that tree compiles.
  - id: D3
    description: No new colour role. CSS and contrast gates unchanged.
    requirement: AG-67-23
    verification:
      - kind: unit
        ref: tests/Autonogrammer.UI.Tests CssContractTests and AccessibilityTests
        status: unknown
    human_judgment: true
    rationale: UI suite result was not confirmed before this summary was written; rerun if the suite did not finish green.

duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 67 plan 06 Summary

**Desktop account panel over IAccountHost: five exclusive states, fixed copy, and a token supplier that prefers the signed-in account token.**

## Performance

- **Duration:** about 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** presentation accounts, native account views, head composition, settings keys, tests

## Accomplishments

- AccountViewModel draws exactly one of SignedOut, AwaitingLink, SigningIn, SignedIn and Error. Offline, SessionNotRemembered, SignOutNotConfirmed and ServiceChanged are notices. The consent action is offered only with SessionNotRemembered.
- AccountCopy has one fixed sentence per AuthPhase and AuthErrorKind. Presentation does not reference Autonogrammer.Accounts.
- Native AccountView and SignInDialog name every control, focus the code field and the error, update countdowns every second, and announce only at 30s, 10s and resend availability. The spinner is hidden under reduced motion. The views are not placed in chrome.
- HeadServices sends the signed-in account token and uses AUTONOGRAMMER_API_TOKEN only when signed out. DesktopAccountHost reads GET /account for the Devices row and writes account.hasStoredSession and account.allowFileSecretStore.

## Task Commits

1. **Tasks 1-3: view model, native views, composition** - one commit `feat(67-06):`

## Files Created/Modified

- `src/Autonogrammer.Presentation/Accounts/` - host seam, copy catalogue, five-state model
- `src/Autonogrammer.Native.Views/Views/Account/` - AccountView and SignInDialog
- `apps/Autonogrammer.Native/Composition/DesktopAccountHost.cs` - IAccountHost adapter
- `apps/Autonogrammer.Native/Composition/HeadServices.cs` - token supplier and account members
- `apps/Autonogrammer.Native/Settings/FileSettingsStore.cs` - the two account settings keys

## Decisions Made

- Skipped ICoworkAccountGate. Grep found no port, no HeaderEndSlot and no SurfaceChromeModel. Phase 69 owns the port and the slot.
- A stored session is published from the secret store before any network call. GoTrue options are loaded lazily from GET /auth/config on the same API client.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] CreateAsync was already at the method-length limit**
- **Found during:** Task 2
- **Issue:** Adding the account wiring inline tripped MA0051 (60 lines).
- **Fix:** OpenAccountAsync owns client creation, host start and the token delegate.
- **Files modified:** HeadServices.cs
- **Verification:** Native build reached the app project after the split.
- **Committed in:** the plan commit

## Issues Encountered

- Native.Tests and the UI suite could not be finished while plan 66-05 files were mid-edit and did not compile (LegacyNotice MA0048, workspace view types). Those files were not edited after the ownership note. Presentation.Tests passed: 2313 succeeded, 0 failed.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Phase 69 must place AccountView in HeaderEndSlot and implement ICoworkAccountGate over IAccountSession. This plan did not invent that port.
- Phase 67-07 can share AccountCopy, AccountViewModel and IAccountHost.

## Hand-off

ICoworkAccountGate, HeaderEndSlot and SurfaceChromeModel are absent. The gate implementation is deferred to phase 69. Dispatch.Desktop stays on IAccountSession plus ISecretStore.

---
*Phase: 67-accounts-supabase-passwordless-sign-in*
*Completed: 2026-10-30*
