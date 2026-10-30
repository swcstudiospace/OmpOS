---
phase: 20-avalonia-foundation
plan: "03"
subsystem: ui
tags: [avalonia, native-head, accessibility, focus, reduced-motion, contrast, presentation-binding, headless-tests]

requires:
  - phase: 20-01
    provides: "Autonogrammer.Presentation (L2), MotionPreference, Avalonia 12.1.4 central pins, LayerMap and solution wiring for Presentation"
  - phase: 20-02
    provides: "SessionViewModel, ComposerViewModel, ConversationListState, ConversationSummary and A2uiBlockMapping in Autonogrammer.Presentation; B1 and B2 resolved in the committed tree"
provides:
  - "Autonogrammer.Native (L4) Avalonia desktop shell: App, Program, MainWindow, three views, ViewMapping, one token dictionary"
  - "Autonogrammer.Native.Tests: headless test app builder, shared harness, and five test files for criteria 3 (native half), 4 (names), 5 (keyboard, focus, motion) and 6 (contrast)"
  - "Native wiring: LayerMap entry (L4), Autonogrammer.slnx, Autonogrammer.Linux.slnf (native head and native tests), ArchitectureTests ProjectReference"
affects: [20-04 gate and evidence, 21 session chat (replaces the static state copy and adds change notification), 22 and 24 platform heads]

key-files:
  created:
    - apps/Autonogrammer.Native/Autonogrammer.Native.csproj
    - apps/Autonogrammer.Native/Program.cs
    - apps/Autonogrammer.Native/App.axaml
    - apps/Autonogrammer.Native/App.axaml.cs
    - apps/Autonogrammer.Native/MainWindow.axaml
    - apps/Autonogrammer.Native/MainWindow.axaml.cs
    - apps/Autonogrammer.Native/Themes/AutonogrammerTokens.axaml
    - apps/Autonogrammer.Native/Views/ConversationListView.axaml
    - apps/Autonogrammer.Native/Views/ConversationListView.axaml.cs
    - apps/Autonogrammer.Native/Views/SessionView.axaml
    - apps/Autonogrammer.Native/Views/SessionView.axaml.cs
    - apps/Autonogrammer.Native/Views/ComposerView.axaml
    - apps/Autonogrammer.Native/Views/ComposerView.axaml.cs
    - apps/Autonogrammer.Native/ViewMapping.cs
    - tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj
    - tests/Autonogrammer.Native.Tests/TestAppBuilder.cs
    - tests/Autonogrammer.Native.Tests/AccessibilityNameTests.cs
    - tests/Autonogrammer.Native.Tests/KeyboardFocusTests.cs
    - tests/Autonogrammer.Native.Tests/ReducedMotionTests.cs
    - tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs
    - tests/Autonogrammer.Native.Tests/PresentationBindingTests.cs
  modified:
    - tests/Autonogrammer.ArchitectureTests/LayerMap.cs
    - tests/Autonogrammer.ArchitectureTests/Autonogrammer.ArchitectureTests.csproj
    - Autonogrammer.slnx
    - Autonogrammer.Linux.slnf
    - docs/open-questions.md

key-decisions:
  - "Shared chrome lives in App.axaml: the Fluent theme, the focus style on each shared control, the Button background transition, and the reduced-motion overrides. The token dictionary holds tokens only, so the contrast test reads nothing else."
  - "Focus ring: a :focus-visible style per shared control sets BorderBrush to Autonogrammer.Color.Accent, BorderThickness to Autonogrammer.Size.FocusBorder and FocusAdorner to null (D-20-10)."
  - "Reduced motion: MainWindow sets the reduced-motion class from MotionPreference. Only Reduced sets it; System resolves to Full (D-20-08, OQ-34). The style rules then set Transitions to an empty collection on the shared controls."
  - "View mapping: ViewMapping.Create is an explicit switch over SessionViewModel and ComposerViewModel. An unmapped type throws ArgumentOutOfRangeException. There is no ViewLocator and no reflection (D-20-11)."
  - "Phase 20 has no transport, so the shell starts with a loaded, empty session list and a transcript in the Empty state. Those are the empty states the UI-SPEC copy describes; no session data is invented."
  - "Keys reach the composer through a tunnelling KeyDown handler on the prompt. Enter without Shift is marked handled, which stops the text box inserting a newline. Shift+Enter is left to the text box."
  - "Async work from key presses and clicks is kept in a Task field (ComposerView.PendingWork), not async void. The repository treats the threading analyzers as errors, and VSTHRD100 forbids async void."
  - "The status line text is owned by MainWindow, which supplies the composer's send and stop callbacks: refused send, and stop with nothing running."
  - "Tests use [AvaloniaFact] as the primary route (D-20-06). The contrast test is a plain [Fact] with an XML parse, so no Avalonia object is constructed there."
  - "Shift+Tab and Shift+Enter are sent as (RawInputModifiers)KeyModifiers.Shift, which compiles without naming a RawInputModifiers member. The two enums share their keyboard bits (RawInputHelpers.ToKeyModifiers)."

patterns-established:
  - "Native views bind the presentation types through DataContext and expose Refresh() for state changes, until presentation view models raise change notifications (OQ-44)."
  - "Test headless helpers live in TestAppBuilder.cs as the internal ShellHarness, next to the assembly-level test application attributes."

requirements-completed: [XP-01, XP-08]  # native half only; see the criteria table: the gate and the AT-SPI2 runbook evidence are not complete in this wave

duration: not measured
completed: 2026-10-21
status: complete-pending-gate
---

# Phase 20: Avalonia Foundation, Plan 03 Summary

**The Autonogrammer.Native desktop shell, one token dictionary, explicit view mapping, named/focusable/reduced-motion controls, five headless test files and the native wiring are written. Nothing has been built, restored, run or gated in this wave. Every test result is pending the orchestrator's gate.**

## Performance

- **Duration:** not measured
- **Tasks:** 3 of 3 implemented
- **Files created:** 21
- **Files modified:** 5

## Verification status

No build, restore, test, lint, format or gate command was run, as the orchestrator contract requires. Nothing below is recorded as passed.

What was checked without a build: API names and namespaces were read from the Avalonia 12.1.4 package XML in the local NuGet cache (`avalonia` 12.1.4, `avalonia.desktop`, `avalonia.themes.fluent`) and from the 12.1.4 headless sources on GitHub (`AvaloniaFactAttribute`, `AvaloniaTestApplicationAttribute`, `AvaloniaTestIsolationLevel`, `HeadlessWindowExtensions.KeyPress`, `Key.cs`, `RawKeyEventArgs`, `RawInputHelpers`). Those checks are reading only. They confirm names and namespaces, not that the code compiles or runs.

## Accomplishments

- `Autonogrammer.Native` (L4, `Exe`) references `Autonogrammer.Presentation` and the central Avalonia pins with no versions. It has one composition root (`Program.Main` via `UsePlatformDetect` and `StartWithClassicDesktopLifetime`), an `App` that merges `Themes/AutonogrammerTokens.axaml`, and one main window (`MinWidth` 640, `MinHeight` 480, title `Autonogrammer`).
- `AutonogrammerTokens.axaml` is one `ResourceDictionary`: seven colour tokens (`Autonogrammer.Color.*`), the type family, sizes, weights and line heights (`Autonogrammer.Font.*`), seven spacing tokens (`Autonogrammer.Space.*`), and the session width, target size and focus border (`Autonogrammer.Size.*`).
- The inventory names are set in XAML: `Conversations` (list), `Session transcript` (transcript scroll region, `Focusable` and `IsTabStop` set explicitly, A-08), `Message` (prompt), `Send message`, `Stop response`, and `Session status` (status line, `LiveSetting` Polite). Each session row is named by its full title in `ContainerPrepared` and `ContainerIndexChanged`.
- Focus: `:focus-visible` on `ListBox`, `ListBoxItem`, `ScrollViewer`, `TextBox` and `Button`, with the Accent border, the 2 px focus border and `FocusAdorner` null.
- Motion: the only transition is the 120 ms Button background `BrushTransition`. `Window.reduced-motion` styles remove `Transitions` from the list, its rows, the transcript scroll region, the prompt and both buttons.
- Behaviour per the UI-SPEC table: Enter sends and Shift+Enter does not; a blank send is a no-op; a refused send keeps the draft and shows `Message not sent. Sending is unavailable.`; Escape does nothing while nothing runs; Stop with nothing running shows `No response is running.` Send and Stop are always enabled.
- `ViewMapping.Create` is an explicit type switch with no `ViewLocator` and no reflection.
- Tests: 5 test files plus `TestAppBuilder.cs`, which holds the assembly-level `[AvaloniaTestApplication]`, `[AvaloniaTestIsolation(PerTest)]` and `[CollectionBehavior(DisableTestParallelization = true)]` (A-05) and the internal `ShellHarness`.

## Task Status

| Task | Status | Notes |
|---|---|---|
| 1. Native shell, tokens, views, explicit mapping | implemented, gate pending | All files in the plan's list, plus the skip link (see Deviations). |
| 2. Headless tests: names, keyboard, motion, contrast, binding | implemented, gate pending | Tests were written from the behaviour list. Nothing has been run. |
| 3. Native wiring: LayerMap, solution, filter, ArchitectureTests reference | implemented, gate pending | No `Directory.Packages.props` change (owned by 20-01). |

## Criteria table

Status key: **implemented-pending-gate** means the code and tests exist and nothing has run. **Blocked** names the exact reason.

| Criterion or acceptance item | Status | Evidence or reason |
|---|---|---|
| C1. One Avalonia 12.1.4 native project builds with warnings as errors (zero AVLN and trim warnings) | implemented-pending-gate | Project and XAML written. Open risks: OQ-35 (analyzers), OQ-36 (AVLN), OQ-42 (trim under `IsTrimmable`). Any fire is fixed at source, not suppressed. |
| C1a. `packages.lock.json` for `apps/Autonogrammer.Native` and `tests/Autonogrammer.Native.Tests` (D-20-12) | blocked for this wave | Restore is not permitted to this agent. The orchestrator must run `dotnet restore --force-evaluate` for both projects and commit the lock files. Without them `RestoreLockedMode` on CI fails. |
| C2. Every control in the Control Inventory rows has the UI-SPEC `AutomationProperties.Name` | implemented-pending-gate | `AccessibilityNameTests.InventoryControlsCarryTheUiSpecNames`, `SkipLinkCarriesTheWebCopy`, `EachSessionRowIsNamedByItsFullTitle`. |
| C2a. No authored tab-stop control or live region without a name (test form of "fails on any unnamed control") | implemented-pending-gate | `EveryAuthoredTabStopAndLiveRegionHasAName`. The walker skips template parts (`TemplatedParent`), which are not authored controls. |
| C3 (native half). The native head binds the same `Autonogrammer.Presentation` view models | implemented-pending-gate | `PresentationBindingTests`: DataContext identity for transcript and composer; `ViewMapping` results; the native assembly declares no `Autonogrammer.Presentation` type and references the presentation assembly; the native and web head csproj files reference `Autonogrammer.Presentation`. |
| C3 (build half). Both heads build against the presentation layer | implemented-pending-gate | `Autonogrammer.UI` references Presentation (wave 2); its B1 and B2 fixes are in the committed tree. The native build is part of C1. |
| C4 (runbook half). Prompt, send and transcript exposed through AT-SPI2 on Linux | blocked | The AT-SPI2 query needs `Autonogrammer.Native` run under `xvfb-run` and `dbus-run-session`. This wave may not run it. Owner: 20-04 evidence. Open: OQ-37, OQ-38, OQ-39. |
| C5a. Shared controls reachable by keyboard in order (list, transcript, prompt, send, stop) | implemented-pending-gate | `KeyboardFocusTests.TabMovesFromSessionListThroughTranscriptPromptSendAndStop`, `ShiftTabMovesFocusBack`. Depends on OQ-40 (template-internal `ScrollViewer` tab stops) and OQ-31 (reverse focus). |
| C5b. Visible `:focus-visible` style: Accent border, 2 px, no default adorner, on every shared control | implemented-pending-gate | `KeyboardFocusTests.KeyboardFocusedSharedControlsShowTheAccentRing`. Depends on OQ-32 (`:focus-visible` under Fluent headless). |
| C5c. Reduced motion removes transitions from every shared control | implemented-pending-gate | `ReducedMotionTests` (4 facts). `System` resolves to `Full` (OQ-34). |
| C6. One token dictionary; every foreground and background pair at least 4.5:1 | implemented-pending-gate | `ThemeContrastTests`: 12 theory rows over the exhaustive 4 by 3 pairs, parsed as XML. Contrast uses the WCAG 2.x linearisation (threshold 0.03928). |
| Task 1 acceptance: single ResourceDictionary with colour, type and spacing tokens | implemented-pending-gate | `Themes/AutonogrammerTokens.axaml`; `TheTokensAreOneResourceDictionaryHoldingColourTypeAndSpacing`. |
| Task 1 acceptance: `ViewMapping.cs` has an explicit switch and no `ViewLocator` | implemented | Checked by reading the file; no `ViewLocator` appears in the native tree. |
| Task 2 acceptance: `DisableTestParallelization` (A-05) | implemented-pending-gate | Set as the assembly attribute in `TestAppBuilder.cs`. The test csproj has no such property: xunit.v3 takes it only as `CollectionBehavior`. |
| Task 2 acceptance: no bUnit reference | implemented | The test csproj has no bUnit reference. |
| Task 2 acceptance: contrast parses AXAML with `XDocument` | implemented | `ThemeContrastTests` uses `System.Xml.Linq`. |
| Task 2 acceptance: keyboard tests use `KeyPress` with `Key` and `PhysicalKey`, never `KeyTextInput` | implemented | `ShellHarness.Press` calls `KeyPress(Key, RawInputModifiers, PhysicalKey, null)`. |
| Task 3 acceptance: `LayerMap` has `["Autonogrammer.Native"] = 4` | implemented | `LayerMap.cs`. |
| Task 3 acceptance: `Autonogrammer.Linux.slnf` lists the native head and both native test projects; `apps/Autonogrammer.App` absent | implemented | Native head after `apps/Autonogrammer.Web`; `Autonogrammer.Native.Tests` after `Autonogrammer.UI.Tests`. |
| Task 3 acceptance: ProjectGraph and ArchUnit tests pass with the native entry | implemented-pending-gate | ArchitectureTests references `Autonogrammer.Native`, so `ArchUnitLayerTests` loads its built DLL. |
| `[AvaloniaFact]` runs under Microsoft.Testing.Platform (OQ-28, OQ-29) | implemented-pending-gate | Route used as the primary, as the plan requires. Not run. No evidence of a blocker exists yet. If the gate shows that it cannot run, record it in OQ-29 and switch to the D-20-06 fallback. The fallback is not applied silently. |

## Files Created/Modified

Created (21): see `key-files.created`. Native tree: csproj, `Program.cs`, `App.axaml(.cs)`, `MainWindow.axaml(.cs)`, `Themes/AutonogrammerTokens.axaml`, three views (`.axaml` and `.axaml.cs` each), `ViewMapping.cs`. Native tests: csproj, `TestAppBuilder.cs`, `AccessibilityNameTests.cs`, `KeyboardFocusTests.cs`, `ReducedMotionTests.cs`, `ThemeContrastTests.cs`, `PresentationBindingTests.cs`.

Modified (5): `tests/Autonogrammer.ArchitectureTests/LayerMap.cs` (one entry), `tests/Autonogrammer.ArchitectureTests/Autonogrammer.ArchitectureTests.csproj` (one `ProjectReference`), `Autonogrammer.slnx` (two entries), `Autonogrammer.Linux.slnf` (two entries, valid JSON by reading), `docs/open-questions.md` (OQ-31 and OQ-33 updated; OQ-44 to OQ-48 added).

Not touched: `Directory.Packages.props` (owned by 20-01), any `packages.lock.json`, any file in `src/Autonogrammer.Presentation` (owned by 20-02), `Autonogrammer.UI`, `Autonogrammer.Web`, `Autonogrammer.App`. No `git add`, commit or push.

## Deviations from Plan

1. **Skip link added.** Plan Task 1 places a skip link first in the window. UI-SPEC's Control Inventory has six rows and does not name it. It is a button named `Skip to conversation` (the web copy), and it moves focus to the transcript. Logged as OQ-47 for product review.
2. **Accessibility walker scope.** Plan Task 2 says the walker fails on "any control with an empty name". The test walks the authored controls (`TemplatedParent` is null) that are tab stops or live regions. Panels, borders, template parts and text blocks are outside its scope. Without this scope, every template part and panel would fail.
3. **`DisableTestParallelization` location.** The acceptance text says "csproj sets DisableTestParallelization". xunit.v3 exposes it as the `CollectionBehavior` attribute, not as an MSBuild property. The attribute is in `TestAppBuilder.cs`, as A-05 specifies.
4. **Shift+Tab member.** `RawInputModifiers.Shift` could not be confirmed in 12.1.4 source. The test uses `(RawInputModifiers)KeyModifiers.Shift` instead (OQ-31).
5. **Helper placement.** Test helpers are the internal `ShellHarness` class in `TestAppBuilder.cs`, not a separate file.
6. **Clear and async handling.** `ComposerView` tracks its async work in a field (`PendingWork`), because `async void` breaks the threading analyzers under warnings-as-errors. Faults in that work are not reported to the user in this wave. The send callback in `MainWindow` never throws, so this is not reached.
7. **Contrast threshold.** The test uses the WCAG 2.x normative threshold 0.03928 rather than 0.04045. Both give the same pass result for these tokens (minimum ratio is UI-SPEC's 5.15).

## Fallback

The gate run (2026-10-21, log `/tmp/autonogrammer-gate-w3-20261021-224038.log`) showed that `Avalonia.Headless.XUnit` 12.1.4 cannot run here: all 22 `[AvaloniaFact]` tests failed during discovery with `MissingMethodException` (`Xunit.v3.TestIntrospectionHelper.GetTestCaseDetails`) under xunit.v3 4.0.2. The D-20-06 fallback is applied. xunit.v3 stays at 4.0.2 (OQ-28 and OQ-29 are answered in `docs/open-questions.md`).

Route now: plain `[Fact]` on `async Task`. Each body runs inside a `HeadlessUnitTestSession` from `Avalonia.Headless` (core) that `TestAppBuilder.StartSession()` opens with `StartNew(typeof(TestAppBuilder), AvaloniaTestIsolationLevel.PerTest)`. Sync bodies use `Dispatch`. Async bodies use `RunAsync`, a small adapter in `TestAppBuilder.cs`, because the core API has no `Func<Task>` overload. `DisableTestParallelization` (`ParallelizationAttribute`, A-05) is unchanged. Assertions, test names and behaviour are unchanged.

Changed files:
- `tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj`: removed `Avalonia.Headless.XUnit`. `Avalonia.Headless` and `Avalonia.Themes.Fluent` stay.
- `Directory.Packages.props`: removed the `Avalonia.Headless.XUnit` PackageVersion. No other project referenced it.
- `tests/Autonogrammer.Native.Tests/packages.lock.json`: removed the `Avalonia.Headless.XUnit` entry by hand. Restore must regenerate it.
- `tests/Autonogrammer.Native.Tests/TestAppBuilder.cs`: removed `AvaloniaTestApplication` and `AvaloniaTestIsolation`. Added `StartSession()` and `RunAsync`.
- `AccessibilityNameTests.cs` (5 tests), `KeyboardFocusTests.cs` (9 tests), `ReducedMotionTests.cs` (4 tests), `PresentationBindingTests.cs` (4 tests): `[AvaloniaFact]` became `[Fact]` with the session. `ViewMappingRejectsAViewModelWithNoMapping` touches no control, but it also runs in the session, as it did under `[AvaloniaFact]`. In `PresentationBindingTests.cs` the session variable is named `headless` because the body declares `session`.
- `docs/open-questions.md`: OQ-28 is resolved by fallback and OQ-29 is resolved.
- Unchanged: `ThemeContrastTests.cs` (plain `[Fact]`, XML only), the two plain `[Fact]` tests in `PresentationBindingTests.cs`, and `ShellHarness.cs`.

The orchestrator must still verify:
1. `dotnet restore --force-evaluate` for `tests/Autonogrammer.Native.Tests` (and `apps/Autonogrammer.Native`). The hand-edited lock must match restore output. Commit both lock files (C1a).
2. Build with warnings as errors. Nothing here was built. Watch for analyzer findings in the new code: the `RunAsync` adapter, the `ConfigureAwait(true)` placement, and the `TestContext.Current.CancellationToken` use.
3. Run all 22 tests under MTP. Each must execute, not fail at discovery. Any failure goes to OQ-29 or OQ-30, not to a silent change.
4. OQ-31, OQ-32 and OQ-40 still apply to the keyboard and accessibility tests. This fallback does not change them.
5. Sessions are disposed per test. Confirm that no test hangs and that the parallelism setting still holds.
6. Older text in this file that mentions `[AvaloniaFact]`, `AvaloniaTestApplication` and the isolation attributes describes the superseded route. This section is the current state.

## Blockers

- **C1a (lock files).** Restore is not permitted to this agent. The orchestrator must run `dotnet restore --force-evaluate` for `apps/Autonogrammer.Native/Autonogrammer.Native.csproj` and `tests/Autonogrammer.Native.Tests/Autonogrammer.Native.Tests.csproj`, then commit both `packages.lock.json` files (D-20-12).
- **C4 runbook evidence.** The AT-SPI2 query for prompt, send and transcript is not run in this wave. It belongs to 20-04 with `xvfb-run` and `dbus-run-session` (OQ-37, OQ-38, OQ-39).

## Risks and Unverified Items

Nothing has compiled or run. Most likely failures, in order:

1. **`ContainerPreparedEventArgs.Container` type (OQ-45).** Assumed `Control`. If it is another type, `ConversationListView.NameRow` fails to compile.
2. **Double-to-`Thickness` resource conversion (OQ-46).** Every spacing and focus-border token is applied through `DynamicResource` to a `Thickness` property. It relies on Avalonia's implicit conversion at run time.
3. **Template-internal `ScrollViewer` tab stops (OQ-40).** If the `ScrollViewer` inside `ListBox` or `TextBox` is a tab stop by default, the Tab order test sees an extra stop and fails.
4. **Button Enter activation.** Two tests send Enter to a `Button` and expect it to activate. The test follows UI-SPEC; Avalonia's `Button` Enter handling was not verified.
5. **`:focus-visible` in headless (OQ-32).** The ring test depends on keyboard focus producing the pseudo-class.
6. **Analyzers and AVLN (OQ-35, OQ-36) and trim (OQ-42).** Unmeasured under warnings-as-errors. Fixes belong in source.
7. **Presentation view models are not observable (OQ-44).** The native views refresh only when `Refresh()` is called. Phase 20 has no state changes, so this does not affect the wave's behaviour.
8. **Placeholder colour (OQ-48).** The watermark part name is not verified; the placeholder keeps the theme colour if it does not match.
9. **Font stack (OQ-41).** The comma-separated family string is declared as UI-SPEC states; Avalonia's parsing of it is not verified.

## Verification the Orchestrator Must Run

1. `dotnet restore --force-evaluate` for `apps/Autonogrammer.Native` and `tests/Autonogrammer.Native.Tests`. Review and commit both lock files (D-20-12).
2. `./bootstrap.sh`: build `Autonogrammer.Linux.slnf` with warnings as errors. Expect the first real signal on OQ-35, OQ-36, OQ-42 and the `ContainerPreparedEventArgs` type (OQ-45). Fix any finding at source; do not suppress.
3. Run the five native test files. Record any failure against the matching OQ row. A `[AvaloniaFact]` failure under MTP is an OQ-29 blocker to record, not a silent switch.
4. `ArchitectureTests`: ProjectGraph and ArchUnit with the new `Autonogrammer.Native` entry.
5. `dotnet format --verify-no-changes` on the changed projects, then the formatter if it reports changes.
6. Wave 20-04 owns the AT-SPI2 runbook evidence and `docs/architecture.md` rows for `Autonogrammer.Native` (L4) and `Autonogrammer.Presentation` (L2).

## Next Phase Readiness

- Phase 21 replaces the static state copy with session data, adds change notification to the presentation view models (OQ-44) and renders transcript blocks in `SessionView`.
- Plans 20-04 and the orchestrator's gate depend on this wave's build and test results. Until those run, every criterion above is implemented and pending.

---
*Phase: 20-avalonia-foundation*
*Completed: 2026-10-21*
