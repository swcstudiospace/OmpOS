---
phase: 72-cowork-browser-use
plan: 01
subsystem: browser
tags: [chromium, puppeteer, pipe, cdp]
requires:
  - phase: 66-rebrand-to-autonogrammer
    provides: BrowserUse project scaffold and PuppeteerSharp 25.15.0 pin
provides:
  - Host-owned Chromium launch plan over a debugging pipe, with no port and no sandbox escape
  - Allow-listed child environment, locator, empty embedded manifest and checksum acquirer
  - Session host bounds, profile wipe and AddBrowserUse
affects: [72-cowork-browser-use]
actuals:
  tokens: 28000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns: [argument-list launch, allow-list environment, user-confirmed checksum acquire]
key-files:
  created:
    - src/Autonogrammer.Harness.BrowserUse/Chromium/ChromiumLaunchPlan.cs
    - src/Autonogrammer.Harness.BrowserUse/Chromium/ChromiumProcessLauncher.cs
    - src/Autonogrammer.Harness.BrowserUse/BrowserSessionHost.cs
    - src/Autonogrammer.Harness.BrowserUse/chromium-manifest.json
  modified:
    - src/Autonogrammer.Harness.BrowserUse/Autonogrammer.Harness.BrowserUse.csproj
key-decisions:
  - "No --no-sandbox flag is assembled, even when SandboxMode is Disabled; that mode is only a validator gate."
  - "The acquirer host check defaults to storage.googleapis.com and tests inject the loopback listener host."
  - "The live pipe spike is implemented and skipped unless AUTONOGRAMMER_LIVE_BROWSER=1. It was not launched."
patterns-established:
  - "ChromiumLaunchPlan is the only flag list."
  - "BrowserEnvironment keeps the D-07 names and drops engine-policy names."
requirements-completed: [AG-72-1, AG-72-2, AG-72-3, AG-72-15]
coverage:
  - id: D1
    description: Flag list has every isolation flag once and never opens a debugging port or disables the sandbox.
    requirement: AG-72-2
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/ChromiumLaunchPlanTests.cs#Build_IncludesEveryIsolationFlagExactlyOnce
        status: pass
    human_judgment: false
  - id: D2
    description: Child environment drops ten secret-named variables and the engine-policy names while keeping PATH, HOME, LANG, LC_ALL and DISPLAY.
    requirement: AG-72-2
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/BrowserEnvironmentTests.cs#Build_DropsSecretsAndEngineNames_AndKeepsDisplayNames
        status: pass
    human_judgment: false
  - id: D3
    description: Options validator names keys, refuses sandbox disable without the allow switch, and accepts only Deny or Quarantine.
    requirement: AG-72-15
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/BrowserUseOptionsValidatorTests.cs
        status: pass
    human_judgment: false
  - id: D4
    description: Locator order, too-old and missing binaries, and the skipped snap path.
    requirement: AG-72-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/ChromiumLocatorTests.cs
        status: pass
    human_judgment: false
  - id: D5
    description: Empty embedded manifest hides acquisition. Local-server acquire installs a good archive and refuses hash, size, cross-host redirect and traversal.
    requirement: AG-72-3
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/ChromiumAcquirerTests.cs
        status: pass
    human_judgment: false
  - id: D6
    description: Session host enforces concurrent, tab, memory, idle and wall bounds and wipes the 0700 profile after CDP close then process-tree kill.
    requirement: AG-72-2
    verification:
      - kind: unit
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Live/PipeConnectLiveTests.cs#SessionHost_EnforcesBounds_AndWipesTheProfile
        status: pass
    human_judgment: false
  - id: D7
    description: Live pipe connect, getVersion, one screencast frame and scrubbed /proc environ.
    requirement: AG-72-1
    verification:
      - kind: e2e
        ref: tests/Autonogrammer.Harness.BrowserUse.Tests/Live/PipeConnectLiveTests.cs#Pipe_ConnectsOverTheHostTransport_AndScrubsTheEnvironment
        status: unknown
    human_judgment: true
    rationale: The spike skips unless AUTONOGRAMMER_LIVE_BROWSER=1. This run did not launch a browser.
duration: 90min
completed: 2026-10-30
status: complete
---

# Phase 72: Cowork browser use Summary

**Host-owned Chromium pipe launch, allow-listed environment, empty checksum manifest and session bounds**

## Performance

- **Duration:** 90 min
- **Started:** 2026-10-30
- **Completed:** 2026-10-30
- **Tasks:** 3
- **Files modified:** 30

## Accomplishments
- Chromium starts from an argument list with the D-09 flags, a pipe and no debugging port or `--no-sandbox`.
- The child environment keeps only the D-07 names. Locator order and the user-confirmed acquirer are tested. The embedded manifest has zero rows.
- `BrowserSessionHost` enforces the documented bounds and wipes the profile. `AddBrowserUse` registers options with `ValidateOnStart`.

## Task Commits

1. **Task 1-3: launch, locate, session** - one commit (feat)

## Files Created/Modified
- `src/Autonogrammer.Harness.BrowserUse/Chromium/**` - launch plan, environment, locator, acquirer, pipe launcher
- `src/Autonogrammer.Harness.BrowserUse/Options/**` - options and validator
- `src/Autonogrammer.Harness.BrowserUse/BrowserSessionHost.cs` - lifetime bounds and profile wipe
- `src/Autonogrammer.Harness.BrowserUse/chromium-manifest.json` - embedded document with no rows
- `scripts/browser/pin-chromium.sh` - maintainer row writer, does not download
- `tests/Autonogrammer.Harness.BrowserUse.Tests/Chromium/**` - unit tests
- `tests/Autonogrammer.Harness.BrowserUse.Tests/Live/PipeConnectLiveTests.cs` - skipped live spike plus offline bounds

## Decisions Made
- Sandbox disable never adds `--no-sandbox`. The validator refuses `SandboxMode=Disabled` unless `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1`.
- Acquisition talks only to an injected allowed host, which production sets to `storage.googleapis.com`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Extra types under owned folders**
- **Found during:** Task 1
- **Issue:** MA0048 requires one type per file, so enums and the session wrapper could not live in the listed files.
- **Fix:** Added the extra types under the folders this plan owns.
- **Files modified:** Options and Chromium folders, plus test Support fakes
- **Verification:** Library build is warning-free
- **Committed in:** the plan commit

---

**Total deviations:** 1 auto-fixed
**Impact on plan:** Required by the one-type-per-file rule. No new package.

## Issues Encountered
- The official `ag-dotnet test` command could not rebuild because sibling Cowork.Abstractions edits were not warning-clean. This plan's tests passed with `BuildProjectReferences=false` against the already-built references: 15 passed, 1 live test skipped.
- The live pipe spike was not executed. The assignment forbids launching a browser.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- 72-02 can read `PolicyMode`, `AllowedHosts` and `DeniedHosts` from `BrowserUseOptions`.
- The live spike still needs an owner-approved `AUTONOGRAMMER_LIVE_BROWSER=1` run.

---
*Phase: 72-cowork-browser-use*
*Completed: 2026-10-30*
