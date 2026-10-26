# ADR-0006: Terminal control and PTY for ADE-14

- Status: Proposed. The PTY runner and the panel pass their Linux tests. The three-platform spike and the accessibility decision are open (see Blockers).
- Date: 2026-10-22
- Deciders: ompos-maintainers
- Scope: the embedded terminal control and the pseudo-terminal (PTY) library for ADE-14, gated by risk R-1 (`docs/plans/ade-harness-plan.md`) and open question OQ-49 (`docs/open-questions.md`). Does not cover MCP, rules or search (ADE-16 to ADE-19).

## Context

ADE-14 requires an integrated terminal that starts in the workspace directory, asks for approval before each command, keeps auto-run off unless explicitly configured, and never spawns a declined command. R-1 records that no terminal control has been verified on Avalonia. ADE-14 needs a PTY, ANSI rendering, resize, keyboard focus and accessibility exposure on the three desktop heads (Linux X11, Windows 11 24H2, macOS 26). R-1 requires the decision to be recorded in an ADR before Phase 28 starts.

This ADR records the candidates and the facts gathered for them. It does not choose a control or a PTY.

## Decision

Two decisions are open and remain open until a spike passes:

1. Which terminal control and which PTY library ADE-14 adopts (OQ-49).
2. Whether the control exposes the terminal to the accessibility trees (AT-SPI2 on Linux, UI Automation on Windows, NSAccessibility on macOS).

The pairing Iciclecreek.Avalonia.Terminal 4.1.0 with Porta.Pty 2.2.3 is implemented for the Linux spike. `OmpOS.Harness.Terminal` holds `PtyProcessRunner`, which uses Porta.Pty. `apps/OmpOS.Native/Views/Terminal` holds the panel, which uses the control. Neither decision is closed: the pairing is not adopted until the spike passes on all three platforms.

What is decided now is the approval boundary, which does not depend on the control. `src/OmpOS.Harness.Terminal` holds a `TerminalGate` that:

- records each proposal as Pending and spawns nothing until it is approved;
- rejects a working directory that resolves outside the workspace root, following symbolic links, before anything is recorded;
- treats a declined proposal as terminal and never calls the runner;
- runs an approved command once through `IProcessRunner`, using `ProcessStartInfo.ArgumentList`, an explicit working directory and no shell;
- runs without a prompt only when `TerminalSettings.AutoRunEnabled` is set explicitly, and that setting defaults to false;
- bounds the captured standard output and standard error.
- when run by the pseudo-terminal runner, starts the executable with its argument list through Porta.Pty, with no shell, in the explicit working directory, and streams the bounded output to an `IProcessOutputSink`. The pseudo-terminal merges standard error into the output.

Process spawning lives only in `OmpOS.Harness.*` projects, as `BannedSymbols.NoProcess.txt` requires.

## Candidates

Licence and dependency facts were read from the NuGet package pages on 2026-10-22. The licence values are the `License` field of each package page. The package licence texts were not inspected in this session.

| Package | Version | Licence | Role | Declared dependencies (net10.0) | Notes |
| --- | --- | --- | --- | --- | --- |
| Iciclecreek.Avalonia.Terminal | 4.1.0 (2026-10-14) | MIT | Avalonia control (`TerminalControl`, `TerminalWindow`) with an XTerm console; advertises Sixel and Kitty graphics | Avalonia >= 12.0.4, Avalonia.Skia >= 12.0.4, Porta.Pty >= 2.2.3, SkiaSharp.HarfBuzz >= 3.119.4, Unicode.net >= 2.0.0, Wcwidth >= 4.0.1, XTerm.NET >= 3.0.0 | Bundles a PTY through Porta.Pty. The repository pins Avalonia 12.1.4, which the declared range admits. Resolution to 12.1.4 must be confirmed in the spike. |
| SvcSystems.UI.Terminal | 2.3.0 (2026-10-16) | MIT | Avalonia terminal control | Avalonia >= 12.1.4, XTerm.NET >= 3.0.0 | The NuGet page does not list Porta.Pty as a dependency. OQ-49 pairs this control with Porta.Pty 2.2.3, so the pairing is an assembly that the spike must make and verify. |
| Porta.Pty | 2.2.3 (2026-10-01) | MIT | Cross-platform PTY library (Windows, Linux, macOS) with a native shim | Microsoft.Windows.Console.ConPTY >= 1.24.260710001 | Used by Iciclecreek.Avalonia.Terminal. Candidate PTY for the SvcSystems pairing. |

## Spike criteria

The spike is required by R-1 and runs on Linux X11, Windows 11 24H2 and macOS 26. It uses the Avalonia version the repository pins (12.1.4). For each candidate, it must check:

- ANSI colour rendering;
- resize of the PTY and the rendered grid;
- keyboard-only use, including focus entry and exit;
- accessibility exposure: AT-SPI2 on Linux X11, UI Automation on Windows and NSAccessibility on macOS.

A candidate passes only if every check passes on all three platforms. The PTY decision (1) and the accessibility decision (2) are decided separately, so a control may pass on accessibility while its PTY is replaced, or the reverse.

The spike evidence is recorded in a runbook under `docs/runbooks/` when it runs. No spike has run as of this ADR.

## Consequences

- ADE-14 cannot ship its UI until one of the two open decisions closes. The approval gate does not depend on the control: it is the enforcement point for command approval whatever control is chosen.
- Measured on Linux: `TerminalControl` starts its default shell (`bash`) when it loads, if `Process` is not empty. The panel clears `Process`, and `ShowingThePanelStartsNoProcess` checks that the test host gains no child process. The control's own launch and input members (`LaunchProcess`, `AttachConnection`, `SendInputAsync`) can start or write to a process. The panel never calls them, and a review must keep it that way. This is a residual risk until the spike passes.
- OQ-49 stays open until the spike and this ADR's decision are recorded. R-1 is closed by the same record.
- The gate's containment check resolves symbolic links. It does not guard against a link that is created or retargeted between the check and the spawn. That window is a known residual risk and is noted for review.

## Measured evidence (Linux X11, 2026-10-22)

Environment: Linux 6.8.0-147-generic x64, .NET SDK 10.0.402, Avalonia 12.1.4 on the headless platform, Iciclecreek.Avalonia.Terminal 4.1.0, Porta.Pty 2.2.3, XTerm.NET 3.0.0. Headless tests do not drive a real X11 display, so the results below are headless results.

| Check | Result | Test |
| --- | --- | --- |
| PTY runs a real command from an argument list and returns output and exit code | yes | `PtyProcessRunnerTests.RunsARealCommandFromAnArgumentListAndReturnsOutputAndExitCode` |
| Arguments reach the process unchanged, with no shell | yes | `PtyProcessRunnerTests.PassesEachArgumentUnchangedAndNeverInterpretsItsContents` |
| Explicit working directory | yes | `PtyProcessRunnerTests.RunsInTheExplicitWorkingDirectory`; `PtyTerminalGateTests.AnApprovedCommandWithNoWorkingDirectoryRunsInTheWorkspaceRoot` |
| Output streamed to a sink; output bounded | yes | `PtyProcessRunnerTests.StreamsOutputToTheSinkAndReturnsTheSameText`; `PtyProcessRunnerTests.KeepsAndStreamsOnlyTheConfiguredNumberOfCharacters` |
| Pending proposal spawns nothing; declined proposal never reaches the terminal | yes | `PtyTerminalGateTests.PendingProposalSpawnsNothingUntilItIsApproved`; `PtyTerminalGateTests.ADeclinedProposalNeverReachesTheTerminal`; panel `ADeclinedProposalSpawnsNothing` (counting runner) |
| Default settings do not auto-run | yes | panel `DefaultSettingsWaitForADecisionAndSpawnNothingOnTheirOwn` |
| ANSI colour rendered | yes at the emulator level: the red cells carry a non-default foreground, and the text after the reset keeps the default. Pixels not measured: headless frame capture returned no frame | panel `AnsiColourReachesTheTerminalCells` |
| Resize handled | yes: a run in progress is resized (`ResizingTheWindowDuringARunResizesThePseudoTerminal`; `PtyProcessRunnerTests.ResizesARunInProgressWhenTheSizeChanges`); a run reports the grid size once the layout has settled (`TheRunReportsTheGridSizeOnceTheLayoutSettles`); the harness starts a run at the size set before it (`PtyProcessRunnerTests.StartsTheCommandAtTheSizeSetBeforeTheRun`) | panel and harness |
| Keyboard reachable | yes: Tab from the command entry reaches the output control | panel `TabReachesTheTerminalOutput` |
| Typed keys reach no process | yes: `IsLive` stays false and the screen does not change | panel `TypingInTheOutputControlReachesNoProcess` |
| Accessible names exposed | yes at the Avalonia level. Platform exposure (AT-SPI2, UI Automation, NSAccessibility): not measured | panel `EveryControlHasAnAccessibleName` |
| Cancel stops the process | yes | `PtyProcessRunnerTests.CancellingKillsTheProcessAndThrows`; panel `StopKillsARunningCommand` |

Findings from the control's source (decompiled with ilspycmd 11.1.0.9782 from Iciclecreek.Avalonia.Terminal 4.1.0):

- The control starts its default shell when `TerminalView` loads, unless `Process` is empty. The panel sets `Process` to empty before the control is shown.
- The control adds its default theme to `Application.Current.Styles` once per process, guarded by a static flag. A second application instance gets no theme, so no template and no emulator. The panel includes the theme in its own `UserControl.Styles`.
- `TerminalControl.Terminal` throws `NullReferenceException` until the template is applied. The panel reads it only when a run starts.
- The control has no public method to write output. The panel writes output through `TerminalControl.Terminal.Write`, which is XTerm.NET's emulator API.
- A missing executable is not reported as a spawn exception. The child exits with code 2 and prints nothing (`PtyProcessRunnerTests.AMissingExecutableIsReportedAsAnExitWithNoOutput`).
- Approving a command changes the panel's text, which can change the grid height while the command runs. The pseudo-terminal follows the grid, so a run can report a size that differs from the size at approval. The panel's resize test therefore reads the size after the layout settles.

Blockers, exact:

1. Windows 11 24H2 and macOS 26: the spike criteria require every check on all three platforms. No evidence exists for either platform.
2. Platform accessibility exposure: AT-SPI2 on Linux X11 is not measured, and UI Automation and NSAccessibility are not measured.
3. Pixel evidence of ANSI colour: `CaptureRenderedFrame` returned no frame under the repository's headless test application (`TestAppBuilder`), so pixels were not compared.
4. Resize and keyboard on a real X11 display: measured headless only.

This ADR stays Proposed. The Linux tests above pass, but the decision rule in Spike criteria is not met on all three platforms, and blocker 2 leaves decision (2) open.

## Evidence

- `docs/plans/ade-harness-plan.md`: ADE-14 row and R-1.
- `docs/open-questions.md`: OQ-49.
- NuGet package pages, read 2026-10-22: `https://www.nuget.org/packages/Iciclecreek.Avalonia.Terminal/4.1.0`, `https://www.nuget.org/packages/SvcSystems.UI.Terminal/2.3.0`, `https://www.nuget.org/packages/Porta.Pty/2.2.3`.
- Implementation and tests on Linux, Debug: `src/OmpOS.Harness.Terminal/` with `PtyProcessRunner` and `IProcessOutputSink`; `tests/OmpOS.Harness.Terminal.Tests/` (40 tests passing, 0 skipped, 2026-10-22); `apps/OmpOS.Native/Views/Terminal/`; `tests/OmpOS.Native.Tests/Terminal/` (13 panel tests passing on two consecutive runs, 2026-10-22).
- Full Native test project (`dotnet test`, Debug, 2026-10-22): 147 tests, 147 passing, 0 failing, 0 skipped. The in-run resize test (`ResizingTheWindowDuringARunResizesThePseudoTerminal`) waits, bounded at about 20 seconds, for the pseudo-terminal to report the grid size the test writes after the layout settles; it passed three times run alone.

## Update 2026-10-26

The Status line above is unchanged: the control and PTY choice stays open, and closing it needs the owner's decision after the three-platform spike (OQ-49). This note records links only.

- [ADR-0013](ADR-0013-plugin-isolation.md) records the isolation boundary for plugin hooks. It shares one residual risk with this record: a path or executable can change between the check and the spawn.
- The omp terminals that the application starts load the plugins of [ADR-0012](ADR-0012-agent-loop-plugins.md) (DR-2). This record does not cover them: its gate governs commands the user proposes, not the omp process.
- The decision-record structure check, `tests/OmpOS.ArchitectureTests/DecisionRecordTests.cs`, applies from ADR-0007 up. This record is checked only for being listed in [README.md](README.md) and for naming the terminal topic.
