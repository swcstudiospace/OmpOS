# Phase 50 Summary: Cursor Visual Parity

Status (recorded 2026-10-27): criteria 1 to 7 met. Criteria 1 to 6 are met by named tests and by the recorded screenshots; criterion 7 is met as recorded whole-solution test runs, not as a `./bootstrap.sh` run. The look has no owner sign-off: OQ-87 to OQ-91 are defaults, not answers. Nothing was run on Windows, macOS, Android or the web head, and no model turn was made.

The record was written after the work. Statuses use: Met, Met (recorded), Not verified.

Recorded test results (2026-10-27): whole-solution test runs at `d75fd88` passed (F10, as reported by the main agent: Native 1450, Presentation 1133, Acp 132, EngineHost 46, Architecture 46, Api.IntegrationTests 210, Coordination 32; every other project 0 failures). No log of that run is committed. The screenshots were captured from the Debug build of `apps/OmpOS.Native` under Xvfb with the real omp 18.12.6 as engine (no prompt sent); the Changes bar images are headless renders.

## Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Static Greptile faces with licences; Anybody only at 20 px and above and never thin; `FontResourceTests` and a typography markup test | Met | Six static TTFs (DM Sans Regular, Medium, SemiBold; Anybody SemiBold; Space Mono Regular, Bold) with OFL notices; `FontResourceTests`, `FontFaceResolutionTests`, `TypographyMarkupTests` (`EveryUseOfTheDisplayFaceIsAtLeast20PxAndSemiBold`, `CodeThatNamesTheDisplayFaceSetsALargeSemiBoldSize`, `NoViewOrHeadNamesAFontFamilyByLiteral`). The scan is textual. |
| 2 | Dark default, System, Light and Dark persisted, colours re-read, `ThemeContrastTests` in both variants | Met | `ThemePreferenceTests` (`TheApplicationStartsDark`, `EachPreferenceMapsToItsVariant`, `ASavedThemeChangesTheRunningWindow`, `AMarkdownBlockRecoloursWhenTheVariantChanges`, `TheTerminalPaletteHasSixteenColoursPerVariant`), `ThemeContrastTests` (Light and Dark), `FileSettingsStoreTests`; `after-dark/settings-2-general.png` |
| 3 | Controls styled from OmpOS tokens through the Fluent aliases, tested in both variants | Met | `ControlStyleTests`, `ButtonLabelColourTests`, `ColourLiteralTests` (allowlist baseline 0); `OmpOSControls.axaml`; the screenshot set |
| 4 | 32 px desktop targets, 24 px floor, skip link hidden until focus and first in the tab order | Met | `DensityTests`, `SettingsAccessibilityTests`, `AgentPanelTabsTests.TheTabsAreNamedAndComeAfterTheSkipLink`, `SkipLinkVisibilityTests` (`AtRestTheSkipLinkIsTransparentButStillThereForAssistiveTechnology`, `FocusMakesTheSkipLinkOpaqueAndLeavingMakesItTransparentAgain`, `TheSkipLinkIsTheFirstStopOfTheTabOrder`, `EnterOnTheFocusedSkipLinkMovesFocusToTheTranscript`); Android `TargetMin` 44 |
| 5 | Layouts of section 4; screenshots 01 to 05 at 1280x800 in Dark and Light and at 960x640 | Met | `after-dark/` and `after-light/` each hold 27 images: 01 to 05, `01-empty-panel-960x640` and `02-chat-session-960x640` (the 14-image set), every Settings page, three slash palette states and the Changes bar render. Judged by eye against the specification. The defects below stay |
| 6 | Accessibility contract unchanged | Met | `KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, `PanelMarkupTests` present and green in the recorded runs |
| 7 | The gate is green | Met (recorded) | Whole-solution test runs above; every build runs with warnings as errors (`Directory.Build.props`). `./bootstrap.sh`, `dotnet format --verify-no-changes` and the web smoke are not recorded for `d75fd88` |

## What the phase delivered beyond the criteria

| Item | Status | Evidence |
|------|--------|----------|
| omp slash command palette in the composer | Met (live, no model turn) for listing; running a command not verified | `ComposerSlashPaletteViewTests`, `SlashPaletteEndToEndTests` (scripted omp), `AcpAvailableCommandsTests`, `HostSessionCommandsTests`; real omp 18.12.6 listed 471 commands in `after-dark/slash-palette-dark.png` (viewed). omp runs a command when the prompt that starts with it is sent; no model turn was made |
| Changes bar with Keep, Undo, Review, Keep all, Undo all, Stale refusal and CRLF handling, feeding the Diff review tool | Met by tests; not run on a real agent edit | `ChangesBarViewTests`, `ChangeSetModelTests`, `SessionChangeSetTests`, `ChangeDiffTests`, `LocalWorkspaceFileSystemChangeTests`; `changes-bar-dark.png`, `changes-bar-light.png` are headless renders over `ChangesFixture` (the sandbox has no model credentials, so the running application cannot show the bar) |
| Mode and thinking chips bound in the composer | Met (live, no model turn) | `OptionChipView` in `ComposerView`; `RuntimeOptionChipTests`, `LiveOmpOptionChipsTests`; the model, `Default` and `high` chips are in `slash-palette-dark.png` |
| Keyboard Shortcuts and Network settings pages | Met | Recorded in Phase 40 (`ShortcutCatalogueTests`, `NetworkDiagnosticsSourceTests`); `settings-10-network*.png`, `settings-11-keyboard*.png` |
| Editor TextMate deadlock fix | Met | `EditorTabSwitchStressTests` |
| GSD-Core plugin `bin` scripts tracked and its node tests gated | Met | `fee72a5`; `scripts/ci/plugin-node-tests.sh` (Phase 44 criterion 3) |

## Defects the captures showed

Fixed during the phase (evidence README, "Defects found in the first captures" and "F9 pass"): primary button labels drawn in the body colour; `SelectableTextBlock` without the text-role classes; "No messages yet." drawn twice; the empty and loading panel hugging the top; a live-looking Stop button while idle; Settings pages with different left edges; stacked "Add API key" and "Test connection"; invisible "0 entries." lines kept as live regions; check box 4 px above its label centre (`SettingsCheckBoxAlignmentTests`); the rail label "New omp terminal" cut at 960 px (`AgentPanelLayoutTests.TheTerminalButtonKeepsItsLabelWholeInTheNarrowRail`); the Network page explanation.

Still open (evidence README, "What still looks off"):

- Popup flyouts show small light corner wedges on Xvfb (no compositor, so no popup alpha). Not verified on a real desktop.
- The Kanban board shows 4.3 columns at 1280 px; the fifth column header is cut at the scroll edge (horizontal scroll, as specified).
- Kanban cards read "unavailable" for Task, Graph, Lessons and Lease without a Substrate.
- In Light, omp's own truecolour output in the terminal uses pale greys that are low contrast on the light terminal body; the emulator palette cannot change them.
- A "2 pipeline stages are degraded" notice shows on an idle new session when Substrate is off and GSD is unavailable: accurate, but loud.
- The Substrate brief wording still carries the internal reason code, "(HttpStatus, HTTP 403)".

## Not verified (stated, not claimed)

- The owner's sign-off of the rendered look (OQ-87 to OQ-91 are defaults).
- A real desktop run: Wayland native, Windows, macOS, Android, and any window manager (the captures have no title bar).
- A model turn: streaming, error with a real model, a real agent edit through the Changes bar, running a palette command.
- `./bootstrap.sh`, `dotnet format` and the web smoke at `d75fd88`.
- The look of the web head (it uses `OmpOS.UI`, not these styles).

## Open items

1. Owner confirmation of OQ-87 to OQ-91, or a different choice (OQ-87: 44 px hit areas around 28 px visuals).
2. A custom title bar, when a Windows and a macOS run exist (OQ-88).
3. A model turn approval (OD-07) to record the streaming and error states and a real agent edit in the Changes bar.
4. Screenshots of the states of Phase 46 criterion 6 that need a model turn, and of Kanban loading, error and disconnected.
