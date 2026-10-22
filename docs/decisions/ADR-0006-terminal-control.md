# ADR-0006: Terminal control and PTY for ADE-14

- Status: Proposed
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

No candidate is adopted. No candidate is referenced by product code.

What is decided now is the approval boundary, which does not depend on the control. `src/OmpOS.Harness.Terminal` holds a `TerminalGate` that:

- records each proposal as Pending and spawns nothing until it is approved;
- rejects a working directory that resolves outside the workspace root, following symbolic links, before anything is recorded;
- treats a declined proposal as terminal and never calls the runner;
- runs an approved command once through `IProcessRunner`, using `ProcessStartInfo.ArgumentList`, an explicit working directory and no shell;
- runs without a prompt only when `TerminalSettings.AutoRunEnabled` is set explicitly, and that setting defaults to false;
- bounds the captured standard output and standard error.

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
- A control or PTY that spawns processes must either route them through `OmpOS.Harness.*` and the approval gate, or be reviewed as a bypass. A control that starts a shell on its own would bypass the gate; the spike must check this.
- OQ-49 stays open until the spike and this ADR's decision are recorded. R-1 is closed by the same record.
- The gate's containment check resolves symbolic links. It does not guard against a link that is created or retargeted between the check and the spawn. That window is a known residual risk and is noted for review.

## Evidence

- `docs/plans/ade-harness-plan.md`: ADE-14 row and R-1.
- `docs/open-questions.md`: OQ-49.
- NuGet package pages, read 2026-10-22: `https://www.nuget.org/packages/Iciclecreek.Avalonia.Terminal/4.1.0`, `https://www.nuget.org/packages/SvcSystems.UI.Terminal/2.3.0`, `https://www.nuget.org/packages/Porta.Pty/2.2.3`.
- Implementation and tests: `src/OmpOS.Harness.Terminal/`, `tests/OmpOS.Harness.Terminal.Tests/` (23 tests passing, Release, 2026-10-22).
