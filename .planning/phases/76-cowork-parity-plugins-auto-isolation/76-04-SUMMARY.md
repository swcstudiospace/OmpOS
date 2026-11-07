---
phase: 76-cowork-parity-plugins-auto-isolation
plan: 04
subsystem: isolation
tags: [bubblewrap, seatbelt, probe, acp, avalonia]

requires:
  - phase: 66.1
    provides: Harness.Isolation scaffold and IProcessLaunchWrapper
provides:
  - ProcessLaunch Wrap contract and argument-vector isolators
  - Probe degraded states and IsolationPlanner consent rules
  - Isolation chip model and settings page
affects: [76-06]

actuals:
  tokens: 28000
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "The wrapper runs after EngineEnvironmentPolicy and unsets scrubbed names with a null environment value"
    - "Code policy returns no wrapper; a failed Cowork probe needs consent before level None"
    - "JobObjectContainer is type-only; Windows stays platform_unsupported until a post-start hook"

key-files:
  created:
    - src/Autonogrammer.Harness.Abstractions/ProcessLaunch.cs
    - src/Autonogrammer.Harness.Isolation/BubblewrapArgvBuilder.cs
    - src/Autonogrammer.Harness.Isolation/IsolationProbe.cs
    - src/Autonogrammer.Presentation/Isolation/IsolationChipModel.cs
  modified:
    - src/Autonogrammer.Harness.Abstractions/IProcessLaunchWrapper.cs
    - src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs
    - src/Autonogrammer.Harness.Acp/AcpClientOptions.cs
    - src/Autonogrammer.Harness.Acp/OmpAcpClient.cs

key-decisions:
  - "IProcessLaunchWrapper is Wrap(ProcessLaunch) only; LaunchContribution.ProcessWrapper is unchanged"
  - "Bubblewrap arguments are the golden vector, then --chdir, then the intended file name and arguments"
  - "Denied directories and socket locations are always emitted; bwrap may fail if a tmpfs target does not exist on the host"

patterns-established:
  - "IsolationUnavailable(code) is the degraded sentence; platform_unsupported also says process containment is not applied"
  - "IsolationLiveTests skips unless AUTONOGRAMMER_LIVE_ISOLATION=1"

requirements-completed: [AG-76-10]

# Phase 76 Plan 04: Isolation wrapper, probe and chip Summary

The omp child can be confined by an argument-vector wrapper. A failed probe names its degraded state and does not start unconfined until the user answers.

## What shipped

- `ProcessLaunch` and `IProcessLaunchWrapper.Wrap`. The three existing `AcpStdioTransport` constructors chain to a fourth that applies an optional wrapper after the environment policy. A null wrapper does not rewrite the start info. `AcpClientOptions.ProcessWrapper` defaults to null and `OmpAcpClient` passes it.
- Linux bubblewrap argv (read-only root, one bind per read-write grant, scratch and state directories, tmpfs over secrets and socket locations, unshare flags) and a Seatbelt profile with quoted paths. Both wrappers scrub `SSH_AUTH_SOCK`, `DBUS_SESSION_BUS_ADDRESS`, `DISPLAY` and `WAYLAND_DISPLAY`.
- Probe maps `bwrap_missing`, `userns_disabled`, `probe_timeout` and `platform_unsupported` through `IIsolationProcessRunner`. `IsolationPlanner` leaves Code unwrapped, asks before a failed Cowork probe continues at level None, and returns the wrapper after a successful probe. Windows reports `platform_unsupported` because the job object is not assigned in this phase.
- `IsolationChipModel` and `IsolationPageView` show the level, `network: unrestricted`, the unconfined-launch sentence, the `~/.omp` risk, and Continue / Stop.

## Verification

- Isolation tests except Live: 17 passed.
- `IsolationLiveTests`: skipped without `AUTONOGRAMMER_LIVE_ISOLATION=1`.
- `AcpStdioTransportWrapperTests`: 5 passed. `AcpLaunchPlumbingTests`: 4 passed. `AcpEnvironmentIsolationTests`: 8 passed.
- `IsolationPageViewTests` did not run: `apps/Autonogrammer.Native` does not compile (sibling analyzer errors outside this plan). `Autonogrammer.Native.Views` Release build succeeded, including the new page.

## Gaps

- Live bubblewrap was not executed. A real `bwrap` run can fail if a denied directory or socket path in the golden vector does not exist, because `--tmpfs` needs the destination.
- Windows job-object assignment is deferred (OQ-457). The planner does not return a wrapper on Windows.
- `SettingsPageViews` is not mapped; 76-06 owns that case.

## Commits

- `feat(76-04):` isolation wrapper, probe and chip

## Deviations

- `IsolationPlatform` and `IsolationProcessRun` are extra files so MA0048 holds.
- Golden fixtures are read by walking up from the test output directory. The test csproj was not edited, so the fixtures are not copied to the output.

---
