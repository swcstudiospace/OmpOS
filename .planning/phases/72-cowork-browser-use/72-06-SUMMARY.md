---
phase: 72-cowork-browser-use
plan: 06
subsystem: settings
tags: [browser, settings, acquisition]
requires:
  - phase: 72-cowork-browser-use
    provides: 72-01 locator states, manifest row and user-confirmed ChromiumAcquirer
provides:
  - Browser settings page over IBrowserUseSettingsSource with a confirm-only acquisition dialog
  - Desktop BrowserUse page between Automations and Privacy when the source is supplied
  - HeadSettings copy of that source; Web head unchanged
affects: [72-08]
actuals:
  tokens: 40000
  tasks: 2
  commits: 1
tech-stack:
  added: []
  patterns: [presentation seam, user-confirmed acquire]
key-files:
  created:
    - src/Autonogrammer.Presentation/Settings/BrowserUse/BrowserUseSettingsPage.cs
    - src/Autonogrammer.Presentation/Settings/BrowserUse/BrowserUseAcquisitionDialog.cs
    - src/Autonogrammer.Native.Views/Views/Settings/BrowserUsePageView.axaml
    - apps/Autonogrammer.Native/Settings/BrowserUseSettingsSource.cs
  modified:
    - src/Autonogrammer.Presentation/Settings/SettingsSection.cs
    - apps/Autonogrammer.Native/Settings/SettingsWindowFactory.cs
    - apps/Autonogrammer.Native/Settings/HeadSettings.cs
key-decisions:
  - "SettingsSection.BrowserUse is 23 because ComputerUse already occupies 22."
  - "AcquireAsync is invoked only from BrowserUseAcquisitionDialog.ConfirmAsync."
  - "A missing manifest row hides the install action and reports ManagedBrowserNotPinned when no binary is found."
patterns-established:
  - "Presentation binds IBrowserUseSettingsSource. The Native adapter maps 72-01 types."
requirements-completed: [AG-72-3]
coverage:
  - id: D1
    description: Each locator state renders its sentence and fix action. Acquisition is hidden without a row. The dialog names version, host, size and licence. Open and LocalOriginsTrusted show a risk line. Invalid saves name the key.
    requirement: AG-72-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Presentation.Tests/Settings/BrowserUseSettingsPageTests.cs
        status: pass
    human_judgment: false
  - id: D2
    description: Desktop view renders the same states and dialog. The page sits after Automations and before Privacy when the source is supplied, and is absent when it is not.
    requirement: AG-72-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Native.Tests/Settings/BrowserUsePageViewTests.cs
        status: pass
    human_judgment: false
duration: 60min
completed: 2026-10-30
status: complete
---

# Phase 72 Plan 06: Settings and acquisition Summary

**Browser settings page, confirm-only acquisition dialog, and a desktop registration that the Web head omits**

## Performance

- **Duration:** 60 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 2
- **Files modified:** 20

## Accomplishments

- `BrowserUseSettingsPage` shows Chromium state, policy lists, PolicyMode and the acquisition action only when a manifest row exists.
- `BrowserUseAcquisitionDialog.ConfirmAsync` is the only caller of the source acquire operation. Dismiss does not acquire.
- The desktop view, factory, page-view map and `HeadSettings` copy the optional source. The Web head is untouched.

## Task Commits

1. **Task 1-2: settings page, view and registry** - one commit (feat)

## Files Created/Modified

- `src/Autonogrammer.Presentation/Settings/BrowserUse/` - page, dialog, source seam and copy
- `src/Autonogrammer.Native.Views/Views/Settings/BrowserUsePageView.axaml` - desktop render
- `apps/Autonogrammer.Native/Settings/BrowserUseSettingsSource.cs` - adapter over 72-01 types
- Settings section, factory, head options and the four lock tests

## Decisions Made

- BrowserUse is ordinal 23. ComputerUse already held 22. The page is inserted after Automations and before Privacy.
- Presentation does not reference Harness.BrowserUse. The Native adapter passes `userConfirmed: true` because the dialog already confirmed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Next free section value**
- **Found during:** Task 1
- **Issue:** The plan's "22 today" was already ComputerUse.
- **Fix:** BrowserUse = 23. The lock test pins that value and count 24.
- **Files modified:** SettingsSection.cs, NetworkAndKeyboardCopyTests.cs
- **Verification:** section lock test
- **Committed in:** the plan commit

**2. [Rule 3 - Blocking] One type per file**
- **Found during:** Task 1
- **Issue:** MA0048 rejects extra types in the listed files.
- **Fix:** Split enums, records and copy into the owned BrowserUse folder.
- **Files modified:** Settings/BrowserUse
- **Verification:** presentation build
- **Committed in:** the plan commit

---

**Total deviations:** 2 auto-fixed
**Impact on plan:** Registration order matches D-40. No Web file was edited.

## Issues Encountered

- Sibling connector analyzer errors can fail a full Presentation rebuild. Tests were run with those codes excluded from warnings-as-errors when required.
- Chromium was not launched.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 72-08 constructs `BrowserUseSettingsSource` and assigns `HeadSettingsOptions.BrowserUse`.

---
*Phase: 72-cowork-browser-use*
*Completed: 2026-10-30*
