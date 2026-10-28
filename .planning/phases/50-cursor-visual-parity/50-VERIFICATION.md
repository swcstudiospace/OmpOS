# Phase 50 Verification: Cursor Visual Parity

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-27 are in `50-SUMMARY.md`. A row marked "no recorded run" has a command but no result. Tests are hermetic: headless Avalonia, no omp, no network. Only the screenshots used the real omp 18.12.6, and none sent a prompt.

Recorded totals: whole-solution test runs at `d75fd88` passed (F10, as reported by the main agent: Native 1450, Presentation 1133, Acp 132, EngineHost 46, Architecture 46, Api.IntegrationTests 210, Coordination 32; every other project 0 failures). No log of that run is committed. The commands below were not re-run by the author of this record.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*FontResourceTests*" --filter-class "*FontFaceResolutionTests*" --filter-class "*TypographyMarkupTests*"` | Each font file hashes to its row of `FontManifest.txt`, carries its name table and an OFL notice; the folder holds only manifest fonts; every use of `Autonogrammer.Font.Display` is 20 px or larger and SemiBold or heavier; no view or head names a font family by literal; the scanner reports what it should (a planted violation) | Inside the passing Native run (F10) |
| 2 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ThemePreferenceTests*" --filter-class "*ThemeContrastTests*" --filter-class "*FileSettingsStoreTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*GeneralPageTests*"` | The application starts Dark; each preference maps to its variant; a saved theme changes the running window; markdown and terminal colours follow the variant; every text pair meets 4.5:1 and every non-text pair 3:1 in both variants; `theme` persists in `config/settings.json` | Inside the passing runs (F10) |
| 3 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ControlStyleTests*" --filter-class "*ButtonLabelColourTests*" --filter-class "*ColourLiteralTests*"` | Controls take their chrome from the tokens through the Fluent aliases in both variants, popups included; a transition is declared only where it is also cleared under reduced motion; a primary button label keeps its own colour; no view holds a colour literal outside the allowlist (baseline 0) | Inside the passing Native run (F10). Screenshots: `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/`, `after-light/` |
| 4 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*DensityTests*" --filter-class "*SettingsAccessibilityTests*" --filter-class "*AgentPanelTabsTests*" --filter-class "*SkipLinkVisibilityTests*"` | Desktop targets use `TargetMin` 32 and none is below 24 by 24; the skip link is transparent at rest and opaque on focus, is the first tab stop, and Enter moves focus to the transcript | Inside the passing Native run (F10) |
| 5 | Screenshots, recipe in `docs/runbooks/evidence/ui-parity-2026-10-26/README.md` (Debug build of `apps/Autonogrammer.Native` under Xvfb, real omp as engine, state directory per run) | The 14-image set (01 to 05 at 1280x800 and `01-empty-panel-960x640`, `02-chat-session-960x640`, in Dark and Light), every Settings page, three slash palette states, and the Changes bar render | 27 images in each folder; viewed in this record's pass: `slash-palette-dark.png`, `changes-bar-light.png`, `01-empty-panel-960x640.png` of `after-light/`; the first pass viewed the chat flyout, Kanban in Light and Settings General. The layouts are judged by eye |
| 6 | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*KeyboardFocusTests*" --filter-class "*AccessibilityNameTests*" --filter-class "*ReducedMotionTests*" --filter-class "*PanelMarkupTests*"` | Tab order, the 2 px focus ring, accessible names, reduced motion and the token rows of the panel markup | Inside the passing Native run (F10) |
| 7 | `./bootstrap.sh` (or, for the gate-equivalent run, `dotnet build Autonogrammer.Linux.slnf -c Release` and then `dotnet test Autonogrammer.Linux.slnf -c Release --no-build -- --filter-not-trait "Category=Live"`) | Build with warnings as errors, the analyzers, and every non-live test | Whole-solution test runs passed (F10). `./bootstrap.sh` itself, `dotnet format --verify-no-changes` and the web smoke: no recorded run for `d75fd88` |

## The 15 steps of the specification

Each step has its commit and tests in `50-01-PLAN.md`. The step 15 acceptance ("the 14-image set exists; `ThemeContrastTests`, `KeyboardFocusTests`, `AccessibilityNameTests` and `ReducedMotionTests` are green") is met by the images above and the recorded runs.

## Additions beyond the criteria

| Item | Command | What it proves |
|------|---------|----------------|
| Slash palette | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ComposerSlashPaletteViewTests*" --filter-class "*SlashPaletteEndToEndTests*" --filter-class "*RuntimeSlashPaletteTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*SlashPaletteEngineCommandsTests*" --filter-class "*ComposerSlashPaletteTests*"` and `dotnet test tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpAvailableCommandsTests*"` | A slash opens the palette with every command; the typed name filters; Enter and Tab complete the name and do not send; Escape closes; at most eight rows show; an unknown name is sent as written; a terminal runtime has no palette; the commands come from the ACP update of a scripted omp |
| Changes bar | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ChangesBarViewTests*"` and `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*ChangeSetModelTests*" --filter-class "*SessionChangeSetTests*" --filter-class "*ChangeDiffTests*" --filter-class "*LocalWorkspaceFileSystemChangeTests*"` | Keep, Undo, Keep all, Undo all, Review into the Diff review tool and Apply review act on the real files of a temporary workspace; a file changed since is Stale and nothing is written; CRLF files are restored with CRLF; paths outside the workspace are refused |
| Editor deadlock | `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*EditorTabSwitchStressTests*"` | Switching or closing a tab while the tokenizer is busy never hangs |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/runbooks/evidence/ui-parity-2026-10-26/before/` | The five captures that caused the specification (01 empty panel, 02 chat session, 03 omp terminal, 04 Kanban cards, 05 Kanban empty). |
| `docs/runbooks/evidence/ui-parity-2026-10-26/foundation/` | Steps 1 to 4 and 6 before any view was restyled, in Dark, with its list of what still looked off. |
| `docs/runbooks/evidence/ui-parity-2026-10-26/after-dark/`, `after-light/` | The captures after the restyle, the Network and Keyboard pages, the slash palette and the Changes bar render; `README.md` of the parent folder lists the method and every defect. |

## Known limits of this verification

- The look has no owner sign-off (OQ-87 to OQ-91 are defaults).
- Linux under Xvfb only, with no window manager: no title bar, popup corner wedges, no compositor. Windows, macOS, Android and the web head were not run.
- No model turn (OD-07): no streaming or error state with a real model, no real agent edit in the Changes bar, no palette command run.
- The typography scan is textual; the display sizes and weights were not measured on a rendered frame.
- No log of the F10 run is committed, and `./bootstrap.sh` was not run for this record.
