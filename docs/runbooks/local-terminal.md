---
description: "Status record of the local terminal: a shell that spawns, echoes typed input and exits with its code on each desktop OS."
---

# Local terminal runbook

Status: Partial (Linux passes; Windows and macOS were not run); status record of 2026-10-26

Last verified: 2026-10-28

Applies to: v0.15.0

Scope: Phase 37 criterion 4 (VX-08). A local shell spawns, echoes typed input and exits with its code on each desktop OS. Status as of 2026-10-26.

This record states status only. Nothing in it is built, run or deployed by this document. The Windows and macOS entries were moved here from `.planning/phases/37-session-runtime-local-terminal/37-SUMMARY.md`, which held them because Phase 37 did not edit `docs/`.

## Status key

- **PASS**: a gate test asserts the behaviour on that platform.
- **NOT RUN**: the platform could not be run. It is never recorded as passed.
- **BLOCKED**: a named prerequisite is missing. The owner and route are in the entry.

## Linux: PASS in the gate

- Backend: `src/OmpOS.Harness.Terminal/LinuxLocalPtyBackend.cs`.
- Test: `LinuxLocalPtyBackendTests.EchoesTypedInputAndDeliversTheMarkerAndTheExitCode` spawns `/bin/sh`, writes `typed`, reads `MARK<typed>` and checks exit code 7. Related tests in the same class: `ASizeSetBeforeTheInputIsTheSizeTheCommandSees`, `StartsTheCommandInTheExplicitWorkingDirectory`, `DoesNotPassHostVariablesOutsideTheAllowlistToTheCommand`.
- Run:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet test tests/OmpOS.Harness.Terminal.Tests --filter-not-trait "Category=Live"
```

- Recorded result for the current HEAD: to be recorded by the orchestrator gate run.

## Windows: NOT RUN

- No local terminal backend exists for Windows, and no Windows runner exists. The target is Windows 11 24H2.
- The Windows runner is an external host (owner item). Status: BLOCKED on the runner.
- Before a run is possible, the Windows backend and its tests must be written. `LinuxLocalPtyBackendTests` do not apply to Windows.
- The terminal control and PTY pairing is also unsettled for Windows: ADR-0006 (`docs/decisions/ADR-0006-terminal-control.md`) is Proposed, and its three-platform spike has no evidence for Windows. Tracked in OQ-49 and OQ-69.
- Evidence to record when the run happens: the command, the host and OS build, the exit code and the test output, in a file under `docs/runbooks/evidence/`.

## macOS: NOT RUN

- No macOS local PTY backend exists. The target is macOS 26.
- The macOS run needs the Mac channel (XP-11, `.planning/REQUIREMENTS.md`). Status: BLOCKED on the Mac channel.
- ADR-0006's three-platform spike has no evidence for macOS. Tracked in OQ-49 and OQ-69.
- Evidence to record when the run happens: the same items as for Windows.

## What is and is not wired

- The terminal panel the native head ships (`src/OmpOS.Native.Views/Views/Terminal/TerminalPanelView`) runs commands through `PtyProcessRunner` and `TerminalGate` proposals. It does not use `LinuxLocalPtyBackend`, which is unit-tested only (status of the 2026-10-26 roadmap scout).
- No class other than the test fake `tests/OmpOS.Harness.Herdr.Tests/FakeHerdrSessionClient.cs` implements `IHerdrSessionClient`. Session restart semantics for a real herdr endpoint wait for an owner decision (OQ-76).

## Open items

1. Windows 11 24H2 runner and backend: owner, runner host. Route: OQ-69.
2. macOS 26 backend and run: owner, Mac channel (XP-11). Route: OQ-69.
3. Gate result for the current HEAD: to be recorded by the orchestrator gate run.
