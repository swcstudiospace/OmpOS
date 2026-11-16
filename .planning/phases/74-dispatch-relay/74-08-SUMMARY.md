---
phase: 74-dispatch-relay
plan: 74-08
subsystem: presentation
tags: [dispatch, settings, native, web, accessibility]
requires:
  - 74-01 Dispatch contracts
  - 74-06 DispatchPageModel, the Dispatch views, DispatchCopy, IDispatchHost, IThisDeviceHost, NotConfiguredDispatchHost
provides:
  - SettingsSection.Dispatch (value 30)
  - DispatchRelayModel: reads the devices and this desktop's pairing from the host ports on the owning context and raises a change when ready
  - DispatchSettingsPage (holds the relay; loads it with the window)
  - Native window page after Cloud, with its host ports and once-created model on SettingsWindowOptions
  - Web settings page after Cloud at /settings/dispatch (no route added), drawn by the shared Dispatch page view in the web dialog
affects: [74-09]
actuals:
  tokens: not measured
  tasks: 2
  commits:
    - 0cfae974 feat(74-08) the Dispatch settings section in the native and web heads
tech-stack:
  added: []
  patterns:
    - Settings pages hold a model; the Native view re-points to the relay page when the relay raises its change
    - Lock tests count and iterate the sections; each is updated from its current value
key-files:
  created:
    - src/Autonogrammer.Presentation/Dispatch/DispatchRelayModel.cs
    - src/Autonogrammer.Presentation/Settings/DispatchSettingsPage.cs
    - tests/Autonogrammer.Presentation.Tests/Dispatch/DispatchSettingsPageTests.cs
    - tests/Autonogrammer.UI.Tests/Dispatch/DispatchSettingsDialogTests.cs
  modified:
    - src/Autonogrammer.Presentation/Settings/SettingsSection.cs (Dispatch = 30)
    - src/Autonogrammer.Presentation/Dispatch/DispatchCopy.cs (ProblemsLabel)
    - src/Autonogrammer.Native.Views/Views/Settings/SettingsPageViews.cs (view map, context, re-point on change)
    - src/Autonogrammer.Native.Views/Views/Dispatch/DispatchPageView.axaml (named live regions)
    - apps/Autonogrammer.Native/Settings/SettingsWindowOptions.cs, SettingsWindowFactory.cs
    - apps/Autonogrammer.Web/Services/Settings/WebSettingsOrder.cs, WebSettingsPages.cs
    - src/Autonogrammer.UI/Components/Settings/SettingsDialog.razor (Dispatch case)
    - src/Autonogrammer.UI/Components/Dispatch/DispatchPageView.razor (heading h3)
    - tests: Presentation NetworkAndKeyboardCopyTests (count 31); Native SettingsWindowTests, SettingsWindowHostTests, SettingsFixture; Web WebSettingsPagesTests, SettingsPagesRenderTests (demo count 25)
key-decisions:
  - "DispatchRelayModel is public in Presentation.Dispatch; the plan named it only as an internal member of the options. The options hold it, the page wraps it."
  - "The Dispatch page heading is h3, the level the settings pages use for their titles, so the dialog's page-title check finds it."
  - "The Native live regions carry names from the Dispatch copy (DevicesLabel, ProblemsLabel); the accessibility suite requires a name on every live region."
requirements-completed: [AG-74-15]
---

# Phase 74 plan 08 summary

The Dispatch section in the Native settings window (and the Android head that shares it) and in the web settings at `/settings/dispatch`, following the Cloud page seam, with every lock test that counts or iterates the sections updated from its current value.

## Gate
- `0cfae974` (clean worktree, `bootstrap.sh`): `bootstrap: OK`; 14578 tests, 0 failed, 3 skipped; api, demo and artifact web smoke each exit 0.

## Tests
- Presentation `*Dispatch*`: 33 passed (3 new: the section and its title, the device read through the host port, the change raised when ready).
- Presentation `*NetworkAndKeyboardCopy*`: 7 passed (the section count is 31).
- Native `*Settings*`: 436 passed, 1 skipped. The accessibility, font and window lock tests pass with the Dispatch page in the window.
- Web `*Settings*`: 138 passed. `SettingsPagesRenderTests` count is 25 (was 24).
- UI `*Dispatch*`: 8 passed (1 new: the dialog draws the Dispatch page with its heading and no host). UI `*SettingsWebPages*`: 6 passed.
- Web `*IndexRoutes*`: 4 passed, unchanged. No route was added.
- Format verified on every changed C# file.

## Deviations
- The plan's file list omits `SettingsFixture.cs` and `DispatchCopy.cs`. The fixture hand-builds the Native window's page list, so it needed the Dispatch page to keep the accessibility, font and order checks honest. `DispatchCopy.ProblemsLabel` names the failure region the accessibility suite requires to be named.
- The plan's files list has no `DispatchRelayModel.cs`. It is the page's model and is needed by both heads.
- No RED step is claimed. Tests and implementation were written together; the first Native build failed on CA1859 and was fixed.

## Must-haves met in the correction
- "The page shows the per-capability toggles and Pause Dispatch on a desktop": done on the Native page (`DispatchPageView` switches; `DispatchRelayModel.SetCapabilityAsync` and `SetPausedAsync`; `IThisDeviceHost.SetPausedAsync`). The web head has no desktop section, as the plan says. The earlier deferral to 74-09 was wrong: the state was already on the page model.

## Not verified here
- Live update on the web head: the dialog draws the relay's page after the window's load, because the window raises its change once every page has loaded. The tests check the render without a host, not a load that changes the page.
- Screen-reader behaviour (NVDA, VoiceOver, TalkBack): not run.
- The Android head: not built here (no Android SDK on this host).

## Notes for 74-09
- The host properties (`DispatchRelayHost`, `DispatchRelayThisDevice`) are set on `SettingsWindowOptions` and read by the factory; 74-09 supplies them through `HeadSettings` and `HeadServices`.
- The desktop toggles and Pause Dispatch are built (see the correction above). The heartbeat that reports `paused` belongs to the hub client in 74-05 (OQ-504).

## SOTA Update (executed 2026-11-10, refs 74)

- SOTA states verified in settings surfaces (per-phase status, toggles for capabilities/pause from host models); a11y (lock tests, targets); first-attempt gate.
- Full details in created 74-SOTA-DISPATCH-RELAY.md.
