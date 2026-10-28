# Phase 50 Context: Cursor Visual Parity

Milestone v0.14.0. Requirements AL-21, AL-22, AL-23. Depends on Phases 39, 40, 46 and 48 (the views it restyles). Design specification: `docs/plans/ui-parity/cursor-agents-window-parity.md` (written 2026-10-26 from screenshots of the running application, 15 landing steps). Evidence: `docs/runbooks/evidence/ui-parity-2026-10-26/` (`before/`, `foundation/`, `after-dark/`, `after-light/` and their README files). Owner questions: `docs/open-questions.md` OQ-87 to OQ-91. Audit: `docs/quality/cursor-parity-audit-2026-10-26.md`.

This context was written after the work (2026-10-27) from the commits `0fd3a27` (specification and records), `7323f4b` (steps 1 to 4), `977010c` (steps 5 and 7 to 14), `ee4b6f4` (step 15, first captures), `a12871a` (final captures with the Network and Keyboard pages) and `d75fd88` (the last tests and the additions listed below), the code, the tests and the evidence files. It records what was decided in the code, not a discussion.

## Goal

The application looks and feels like Cursor's Agents window, set in the Greptile typefaces, and keeps its accessibility contract: Dark by default, a styled control library, compact density, and a layout for every view that follows section 4 of the specification.

Success criteria are in `.planning/ROADMAP.md` (Phase 50, criteria 1 to 7). The criteria table with status is in `50-SUMMARY.md`.

## Decisions

| ID | Decision | Where it is in the code and tests |
|----|----------|-----------------------------------|
| D50-1 | The Greptile faces are static instances: DM Sans Regular, Medium and SemiBold, Anybody SemiBold (derived under the OFL), Space Mono Regular and Bold. Avalonia cannot select a variable-font axis, so the earlier variable Anybody rendered its Thin default instance. The two variable files were removed. | `src/Autonogrammer.Native.Views/Assets/Fonts/**`, `tests/Autonogrammer.Native.Tests/Theme/FontManifest.txt`, `FontResourceTests`, `FontFaceResolutionTests` |
| D50-2 | Anybody is the display face (`Autonogrammer.Font.Display`) and is used only at 20 px and above in SemiBold. Display text is 24 px (`display`) or 20 px (`display-sm`). No view names a font family by literal. | `AutonogrammerControls.axaml`, `TypographyMarkupTests` |
| D50-3 | One token set carries the Light and Dark roles, radii, spacing, sizes, two shadows and the weights. Colour roles stay inside the `Light` and `Dark` dictionaries; sizes stay outside. Fluent resource keys are aliases to the tokens, so no control shows unthemed chrome. | `src/Autonogrammer.Native.Views/Themes/AutonogrammerTokens.axaml`, `AutonogrammerControls.axaml`, `ControlStyleTests`, `ColourLiteralTests`, `ThemeContrastTests` |
| D50-4 | The application starts Dark. Settings > General offers System, Light and Dark; the choice persists in `config/settings.json` as `theme` and views that build colours in code read the tokens again when it changes. | `apps/Autonogrammer.Native/App.axaml` (`RequestedThemeVariant="Dark"`), `GeneralPage`, `FileSettingsStore`, `ThemePreferenceTests`, `GeneralPageTests` |
| D50-5 | The desktop target is 32 px (`Autonogrammer.Size.TargetMin`); the Android head keeps 44. The test floor is the WCAG 2.2 AA 24 px minimum in one constant. | `AutonogrammerTokens.axaml`, `AutonogrammerAndroidApp.axaml`, `tests/Autonogrammer.Native.Tests/TargetSize.cs`, `DensityTests`, `SettingsAccessibilityTests` |
| D50-6 | The skip link is hidden until it has focus (transparent, still present for assistive technology and still the first tab stop). | `Button.skip-link` in `AutonogrammerControls.axaml`, `SkipLinkVisibilityTests` |
| D50-7 | The accessibility contract is unchanged: `x:Name` values, accessible names, tab order, the 2 px focus ring, reduced motion and the keyboard map. | `KeyboardFocusTests`, `AccessibilityNameTests`, `ReducedMotionTests`, `PanelMarkupTests` |
| D50-8 | The native window frame stays on every platform; the top bar is an in-window bar. | OQ-88 |

## Owner questions answered by recorded defaults

The owner did not answer the specification's questions O1 to O8. The defaults below were taken on 2026-10-26 and stay open as non-blocking rows. None is an owner sign-off.

| Row | Question | Default in force |
|-----|----------|------------------|
| OQ-87 | O1: 32 px desktop targets instead of the 44 px contract | `TargetMin` 32 on the desktop, Android keeps its larger value, the 44 px assertions became the 24 px WCAG minimum in `TargetSize.MinimumTarget`. If the owner refuses, the specification's fallback keeps 44 px hit areas around 28 px visuals. |
| OQ-88 | O2: custom title bar on Windows and macOS | Native window frame on every platform; a custom title bar is deferred until a Windows and a macOS run exist (OQ-69, OQ-74). |
| OQ-89 | O4: Settings as a separate window although `46-UI-SPEC.md` says there is no second window | A separate, non-modal Settings window, opened from the rail footer and with Ctrl+,. |
| OQ-90 | O6: replace the two variable font files by static instances, one derived under the OFL | Yes: static DM Sans, Space Mono and a derived Anybody SemiBold with their licence texts. |
| OQ-91 | O3, O5, O7, O8: workspace label, primary colour in Dark, mode chip rule, Kanban Steer rule | The top bar shows the workspace folder name and falls back to `Autonogrammer`; the primary action keeps the purple fill; the composer mode chip shows only when omp supplies modes; Steer is always visible on a Kanban card and disabled with a reason when the session cannot be steered. |

## Contract amendments

The Phase 20 and Phase 46 specifications set values that the specification of this phase changes. Each is recorded here so that the old text is not read as current.

| Item | Before | Now | Test and source |
|------|--------|-----|-----------------|
| Pointer target on the desktop | 44 px (`TargetMin` 44; 26 assertions in 13 test files) | 32 px target and a 24 px minimum for every interactive control (WCAG 2.2 AA, 2.5.8) | `Autonogrammer.Size.TargetMin` 32, `TargetSize.MinimumTarget` 24, `DensityTests` (OQ-87) |
| Hover and press transition | 120 ms `BrushTransition` on `Button`, `ToggleButton` and `TabItem` (`46-UI-SPEC.md`) | One 100 ms `BrushTransition` on `Background` for `Button`, `ToggleButton`, `ListBoxItem` and `TabStripItem`; each is cleared under `Window.reduced-motion` | `AutonogrammerControls.axaml`, `ControlStyleTests` (a transition is declared only where it is also cleared), `ReducedMotionTests` |
| Chip height | 24 px | 20 px (`Autonogrammer.Size.ChipHeight`) | `PanelMarkupTests` (`Autonogrammer.Size.ChipHeight`, 20) |
| Session row height | 64 px | 48 px (`Autonogrammer.Size.SessionRowMinHeight`) | `PanelMarkupTests` |
| Transcript and composer column | 880 px | 760 px (`Autonogrammer.Size.ThreadContentMaxWidth`) | `PanelMarkupTests` |
| Body and heading sizes | 15 and 18 | 14 and 16 | `AutonogrammerTokens.axaml` |

## Fifteen steps of the specification

The units of the work are the 15 landing steps of section 5 of the specification. `50-01-PLAN.md` lists each with its commit and tests.

## Additions beyond the criteria, recorded here

Two capabilities landed in `d75fd88` under Phase 49 numbering. The criteria of Phase 49 (live steering) do not cover them, so they are recorded here and in the audit, not as Phase 49 results.

- The omp slash command palette in the composer: typing `/` lists the commands omp advertises through ACP `available_commands_update` (471 in the sandbox of the screenshots). Arrow keys move, Enter and Tab complete the name, Escape closes, at most eight rows show and the list scrolls. omp runs a command when the prompt that starts with it is sent; the palette runs nothing. The one Autonogrammer catalogue entry (`side`) is not listed because no handler is wired. See audit rows VX-02, 38.3 and 38.4.
- The Changes bar: under the transcript, the files the agent changed in the session with Keep, Undo and Review per file and Keep all and Undo all. Undo writes the text from before the agent back only while the file still holds what the agent left (a CRLF file is restored with CRLF; a file changed since is Stale and nothing is written). Review opens the Diff review tool with that file's diff and Apply review writes the decisions through `DiffReviewWriter`. See audit rows ADE-11 and 27.3.

Also in `d75fd88`: the editor's TextMate handling no longer deadlocks when a tab is switched or closed while the tokenizer is busy (`EditorTabSwitchStressTests`).

## Out of scope

- A custom title bar (OQ-88).
- Any change of behaviour of the views it restyles.
- The web head and Android layouts beyond the shared token and control library.
