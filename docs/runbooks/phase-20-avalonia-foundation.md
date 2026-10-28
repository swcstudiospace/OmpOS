---
description: "Record of Phase 20: the Avalonia foundation, its success criteria and the AT-SPI2 evidence."
---

# Phase 20 Avalonia foundation: runbook

Status: Record (historical, 2026-10-22)

Last verified: 2026-10-28 (the status only; the body is a dated record and was not re-verified)

Applies to: v0.15.0

Correction 2026-10-27: `apps/OmpOS.Native/Themes/OmpOSTokens.axaml` is now `src/OmpOS.Native.Views/Themes/OmpOSTokens.axaml`, and the keyboard test `TabMovesFromSessionListThroughTranscriptPromptSendAndStop` is now `TabMovesFromSessionListThroughPipelineStripTranscriptPromptSendAndStop`.

Status as of 2026-10-22: plan waves 1 to 3 are written (`20-01`, `20-02`, `20-03` in `.planning/phases/20-avalonia-foundation/`). Wave 4 (this runbook and the AT-SPI2 evidence) is in progress. Section 3, the AT-SPI2 query for criterion 4, is recorded (X11 under Xvfb, 2026-10-22; evidence in `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/`). The repo records no per-criterion pass. The wave 3 gate run of 2026-10-21 logged to `/tmp/ompos-gate-w3-20261021-224038.log`, which is not committed; `20-03-SUMMARY.md` cites it.

Sources: `.planning/ROADMAP.md`, Phase 20 success criteria 1 to 7; `.planning/phases/20-avalonia-foundation/20-03-SUMMARY.md`; `.planning/phases/20-avalonia-foundation/20-04-PLAN.md`; `docs/open-questions.md` (OQ-28 to OQ-48).

## 1. Success criteria map

Status key: **test present** means the file and test exist in the repo. The run result is not recorded in the repo. **PENDING** means the evidence is not produced.

| # | Criterion (ROADMAP Phase 20) | Files | Tests and checks | Status (2026-10-22) |
| --- | --- | --- | --- | --- |
| 1 | One Avalonia 12.1.4 native project builds with warnings as errors | `apps/OmpOS.Native/OmpOS.Native.csproj`, `apps/OmpOS.Native/packages.lock.json`, `OmpOS.Linux.slnf` (line 25), `bootstrap.sh` (line 39) | Build: `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror`, run by `bootstrap.sh` and the `build-test` job of `.github/workflows/ci.yml` | Build not recorded as passed. Open risks: OQ-35 (analyzers), OQ-36 (AVLN), OQ-42 (trim). |
| 2 | A presentation layer holds the view models and state, with no UI-framework types, and references no Avalonia, MAUI or ASP.NET Core Components assembly | `src/OmpOS.Presentation/` (`OmpOS.Presentation.csproj`, `State/`, `Composer/`, `Approvals/`, `Runs/`, `Observable/`, `Plugins/`, `MotionPreference.cs`); `tests/OmpOS.ArchitectureTests/LayerMap.cs` (`OmpOS.Presentation` = 2) | `tests/OmpOS.Presentation.Tests/PresentationIsolationTests.cs`: `ProjectFile_DeclaresNoUiFrameworkReference`, `BuiltAssembly_ReferencesNoUiFrameworkAssembly`, `NoPublicType_DerivesFromUiFrameworkType` | Test present; run not recorded. |
| 3 | Consumed by the Avalonia head and the Blazor web head; both build against it and its tests pass | `src/OmpOS.UI/OmpOS.UI.csproj` (ProjectReference, line 15); `apps/OmpOS.Native/OmpOS.Native.csproj` (ProjectReference, line 15). `apps/OmpOS.Web` reaches the presentation layer only through `OmpOS.UI`. | Native: `tests/OmpOS.Native.Tests/PresentationBindingTests.cs` (`TranscriptIsBoundToThePresentationSessionViewModel`, `ComposerIsBoundToThePresentationComposerViewModel`, `ViewMappingReturnsTheNativeViewForEachMappedType`, `ViewMappingRejectsAViewModelWithNoMapping`, `NativeAssemblyReferencesPresentationAndDeclaresNoCopy`, `HeadProjectReferencesThePresentationProject`). Web: `tests/OmpOS.UI.Tests/` (eight files reference `OmpOS.Presentation`, for example `AccessibilityTests.cs`). | Test present; run not recorded. `HeadProjectReferencesThePresentationProject` checks `apps/OmpOS.Native` and `src/OmpOS.UI` only. `apps/OmpOS.Web/OmpOS.Web.csproj` has no direct reference. |
| 4 | Every control in the Avalonia head has an explicit `AutomationProperties.Name`; prompt input, send control and transcript are exposed through AT-SPI2 on Linux | `apps/OmpOS.Native/MainWindow.axaml`, `apps/OmpOS.Native/Views/ComposerView.axaml`, `Views/SessionView.axaml`, `Views/ConversationListView.axaml` | Headless: `tests/OmpOS.Native.Tests/AccessibilityNameTests.cs`: `InventoryControlsCarryTheUiSpecNames`, `StatusLineIsAPoliteLiveRegion`, `EveryAuthoredTabStopAndLiveRegionHasAName`, `SkipLinkCarriesTheWebCopy`, `EachSessionRowIsNamedByItsFullTitle`. AT-SPI2: section 3. | Headless half: test present; run not recorded. AT-SPI2 half: recorded on X11 under Xvfb (section 3; `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/atspi-query.txt`): `entry` `Message`, `push button` `Send message`, `scroll pane` `Session transcript`. Wayland not run. |
| 5 | Shared controls are keyboard reachable, have a visible focus style, and disable transitions when reduced motion is on | `apps/OmpOS.Native/App.axaml` (shared styles), `apps/OmpOS.Native/MainWindow.axaml.cs` (`reduced-motion` class), `apps/OmpOS.Native/Themes/OmpOSTokens.axaml` | `tests/OmpOS.Native.Tests/KeyboardFocusTests.cs`: `TabMovesFromSessionListThroughTranscriptPromptSendAndStop`, `ShiftTabMovesFocusBack`, `KeyboardFocusedSharedControlsShowTheAccentRing`. `tests/OmpOS.Native.Tests/ReducedMotionTests.cs`: `ReducedSetsTheClassOnTheWindow`, `SystemAndFullDoNotSetTheClass`, `ReducedMotionRemovesTransitionsFromEverySharedControl`, `FullMotionKeepsTheButtonBackgroundTransition`. | Test present; run not recorded. Open: OQ-31, OQ-32, OQ-34, OQ-40. |
| 6 | One resource dictionary holds colour, type and spacing tokens; a contract test asserts at least 4.5:1 for every foreground and background pair | `apps/OmpOS.Native/Themes/OmpOSTokens.axaml` (merged by `App.axaml`) | `tests/OmpOS.Native.Tests/ThemeContrastTests.cs`: `EveryTextPairMeetsTheContrastMinimum` (12 pairs, `[Theory]`), `ThePairSetIsTheTwelvePairsOfTheContract`, `EveryPairTokenIsDefinedInTheDictionary`, `TheDictionaryDefinesExactlyTheContractColourTokens`, `TheTokensAreOneResourceDictionaryHoldingColourTypeAndSpacing` | Test present; run not recorded. The token types are checked in `docs/open-questions.md` (OQ-46). |
| 7 | Plugin host contracts define commands, panels, tools and agent modes with no UI-framework reference; an in-process sample registers one of each through the host | `src/OmpOS.Contracts/Plugins/` (`CommandContribution.cs`, `PanelContribution.cs`, `ToolContribution.cs`, `AgentModeContribution.cs`, `PluginDescriptor.cs`); `src/OmpOS.Presentation/Plugins/PluginHost.cs`; `tests/OmpOS.Presentation.Tests/Plugins/SamplePlugin.cs` | `tests/OmpOS.Contracts.Tests/Plugins/ContractsUiFreeTests.cs`: `ContractsAssembly_ReferencesNoUiFrameworkAssembly`, `PluginContract_IsSealedRecordWithRequiredMembers`. `tests/OmpOS.Presentation.Tests/PluginContractTests.cs`: `SamplePlugin_RegistersOneCommandPanelToolAndAgentModeThroughHost`, `RegisterCommand_RejectsContributionFromUnregisteredPlugin`, `RegisterCommand_RejectsDuplicateIdentifier`. | Test present; run not recorded. |

The layer edges are checked by `tests/OmpOS.ArchitectureTests/ProjectGraphTests.cs` and `ArchUnitLayerTests.cs`. `LayerMap.cs` has `OmpOS.Presentation` = 2 and `OmpOS.Native` = 4.

## 2. BLOCKED items (from 20-04 plan)

No BLOCKED item is recorded as passed. Each names its prerequisite and its verification route.

| # | Criterion or item | Missing prerequisite | Verification route | Owner | Status (2026-10-22) |
| --- | --- | --- | --- | --- | --- |
| BLOCKED-01 | D-20-04 / A-02: `SessionHost.razor` usings compile (20-02 T3) | No MAUI workload or SDK on this Linux host | Build `apps/OmpOS.App` on a macOS or Windows host with the .NET MAUI workload installed. The `shells` job in `.github/workflows/ci.yml` runs on `macos-15` and installs `maui` only when `apps/OmpOS.App/OmpOS.App.csproj` exists. | ompos-maintainers | BLOCKED |
| BLOCKED-02 | XP-03: Windows head UIA names and roles | No Windows 11 24H2 host | Phase 22: UIA client records Name and ControlType on Windows 11 Tier 1. See `docs/runbooks/phase-22-desktop-heads.md`. | ompos-maintainers | BLOCKED |
| BLOCKED-03 | XP-04, XP-05, XP-11: macOS and iOS builds, NSAccessibility and UIAccessibility, signing | No macOS 26 host, no Xcode 27.1, Mac channel | Phases 22 and 24 on a Mac-channel host; signing is human-gated. See `docs/runbooks/phase-22-desktop-heads.md`. | owner (Mac channel) | BLOCKED |
| BLOCKED-04 | XP-06: Android head and `AccessibilityNodeInfo` | No Android SDK or emulator on this host, no Android 16 device | Phase 24: `uiautomator dump` on Android 16 | ompos-maintainers | BLOCKED |
| BLOCKED-05 | XP-02: Wayland-native backend opt-in | Only an X11 session was verified here (the spike used Xvfb); no Wayland session on this host | Phase 22 Linux head: run under a Wayland session | ompos-maintainers | BLOCKED (NOT RUN) |
| BLOCKED-06 | XP-07 / G-WEB: Avalonia.Browser re-measurement | Out of scope (D-20-14); needs wasm-tools and Vercel | Phase 23; Blazor publish and Vercel behaviour unchanged in this phase | ompos-maintainers | BLOCKED (out of scope for Phase 20) |
| BLOCKED-07 | Tier 1 Linux CI (Ubuntu 25.x, Fedora 43, Debian 13) | This host is Ubuntu 24.04 (Tier 2); the `build-test` runner image is not pinned | CI matrix in a later phase | platform | BLOCKED |

## 3. AT-SPI2 evidence (criterion 4, runbook half): RECORDED on Linux X11 (2026-10-22)

- **Status: recorded, Linux X11 only.** Evidence: `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/` (`README.md`, `atspi-query.txt`, `run-atspi-native.sh`, `atspi_dump.py`). Run date 2026-10-22 (UTC). Backend X11 under Xvfb: `DISPLAY=:100`, `WAYLAND_DISPLAY` unset, `Avalonia.X11.dll` and `libX11` mapped in the app process. No Wayland run was made (phase 22 Wayland row stays NOT RUN).
- **Route run:** `run-atspi-native.sh`, which launches `apps/OmpOS.Native/bin/Release/net10.0/OmpOS.Native.dll` (the gate build, not rebuilt) under `xvfb-run -a` and `dbus-run-session`, starts `at-spi-bus-launcher`, and runs `atspi_dump.py`. The spike's `run-atspi.sh` was not reused; the spike is unchanged.
- **Observed controls (exact accessible name, one node each):**
  - Prompt input: role `entry`, name `Message` (`TextBox`, `Views/ComposerView.axaml`).
  - Send control: role `push button`, name `Send message`.
  - Transcript: role `scroll pane`, name `Session transcript` (`ScrollViewer`, `Views/SessionView.axaml`).
  - Also observed: `list` `Conversations`; `push button` `Stop response`.
  - `Session status` (`LiveSetting` Polite) is NOT in the tree at idle. Its `TextBlock` is declared `IsVisible="False"` in `MainWindow.axaml`, so the node does not exist until the status is shown. It was not observed while visible.
- **Application name:** AT-SPI2 lists the process as `Avalonia Application`. Its top-level frame is named `OmpOS`; the query matches on that frame.
- **Items confirmed or not confirmed:**
  - Explicit `AutomationProperties.Name` reaches AT-SPI2 (OQ-37): confirmed for the three required controls and for `Conversations` and `Stop response`.
  - Transcript `ScrollViewer` keyboard reach (A-08, OQ-40): NOT tested. The query reads the tree only.
  - `AutomationProperties.LiveSetting` reaching AT-SPI2 (OQ-39): NOT confirmed. The `Session status` node was absent at idle.
  - Resolved font face on Ubuntu 24.04 (OQ-41): NOT measured by this run.
- **Host facts:** Ubuntu 24.04.4 LTS, kernel 6.8.0-147-generic, x64, `at-spi2-core` 2.52.0-1build1, `xvfb` 2:21.1.12-1ubuntu1.8. Full record in the evidence `README.md`.
- **Result:** the query exited 0 and the three required controls each resolved to one node. No name was missing or wrong, so no finding against Task 20-03 arises from this run. The `Session status` live-region item stays open under OQ-39.

## 4. Control inventory and behaviour as built

Names are the `AutomationProperties.Name` values in the XAML.

| Control | Name | File | Notes |
| --- | --- | --- | --- |
| Conversation list | `Conversations` | `Views/ConversationListView.axaml` | `Focusable` True. Each row is named by its full title (`EachSessionRowIsNamedByItsFullTitle`). |
| Transcript | `Session transcript` | `Views/SessionView.axaml` | `Focusable` and `IsTabStop` True (A-08, OQ-40). |
| Prompt input | `Message` | `Views/ComposerView.axaml` | Enter sends; Shift+Enter does not send. |
| Send | `Send message` | `Views/ComposerView.axaml` | `Button`. Always enabled. |
| Stop | `Stop response` | `Views/ComposerView.axaml` | `Button`. Always enabled. With nothing running it shows `No response is running.` |
| Status line | `Session status` | `MainWindow.axaml` | `LiveSetting` Polite. |
| Skip link | `Skip to conversation` | `MainWindow.axaml` | Not in the UI-SPEC Control Inventory (OQ-47). Moves focus to the transcript. |

- **Keyboard order:** conversation list, transcript, prompt, send, stop (`TabMovesFromSessionListThroughTranscriptPromptSendAndStop`).
- **Focus style:** `:focus-visible` on each shared control. Border AccentPurple (`OmpOS.Color.AccentPurple`), width `OmpOS.Size.FocusBorder` (2 px), `FocusAdorner` null (D-20-10).
- **Reduced motion:** `MainWindow` sets the window class `reduced-motion` only for `MotionPreference.Reduced`. `System` resolves to `Full` (D-20-08, OQ-34). Shared controls then have their transitions removed by the styles in `App.axaml`.

## 5. Open questions that bear on this evidence

Status text lives in `docs/open-questions.md`. This list names the IDs only.

- Resolved: OQ-28 and OQ-29. `[AvaloniaFact]` did not run under MTP, so the route is a plain `[Fact]` with the `Avalonia.Headless` core session (D-20-06). `Avalonia.Headless.XUnit` was removed from `tests/OmpOS.Native.Tests`.
- Build gate: OQ-30 (issue #22021, parallelism), OQ-33 (`StartWithClassicDesktopLifetime`), OQ-35 and OQ-36 (analyzers and AVLN), OQ-42 (trim), OQ-43 (transitive pins).
- Behaviour and accessibility: OQ-31 (`RawInputModifiers.Shift`, reverse focus), OQ-32 (`:focus-visible`), OQ-34 (reduced-motion reader), OQ-37 to OQ-41 (AT-SPI2 names, `LiveSetting`, `ScrollViewer` focus, font face), OQ-48 (`PART_Watermark`).
- Model and types: OQ-44 (native views refresh on change; the models now raise `PropertyChanged`, the views do not yet subscribe), OQ-45 (`ContainerPreparedEventArgs.Container` type), OQ-46 (token types; the earlier assumption is withdrawn, see the OQ-46 status).
- Product: OQ-47 (skip link).

## 6. Remaining steps before Phase 20 can close

1. Run `dotnet restore OmpOS.Linux.slnf --force-evaluate` as the orchestrator (`20-04-PLAN.md`, orchestrator-only step 1). Confirm the `packages.lock.json` files for `Presentation`, `Presentation.Tests`, `Native` and `Native.Tests`.
2. Run `./bootstrap.sh` once: build with warnings as errors, test, and `dotnet format --verify-no-changes`. Record the result per criterion in section 1.
3. Run the AT-SPI2 query (section 3) and create the evidence directory `docs/runbooks/evidence/phase-20-avalonia-foundation-2026-10-21/` with `README.md` and `atspi-query.txt`.
4. Record each finding against the matching OQ row and the task that owns it.
5. Update the status column of section 1 and section 2 with the recorded results. Do not mark a criterion passed without its artefact.
