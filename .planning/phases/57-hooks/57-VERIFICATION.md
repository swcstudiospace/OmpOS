# Phase 57 Verification: Hooks

How to re-run each proof and what it proves. Run from the repository root. Set the .NET environment first:

```sh
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
```

Results recorded on 2026-10-28 are in `57-SUMMARY.md`. Tests are hermetic: no real omp, no network, no model. The process-runner tests use the real `/bin/sh` and skip on Windows; `HookExtensionEndToEndTests` runs the real extension file under node and skips without node.

Recorded totals (orchestrator facts, recorded 2026-10-28, final tree before the last wiring): Plugins 429, AgentLoop 1221, Acp 201, AgentWorker 153, Presentation 2303, Native 2012 (2 skipped), Contracts 137, Architecture 46, all passing. The full `./bootstrap.sh` gate on a clean worktree of commit e24e6851 exited 0 (recorded 2026-10-28: 12215 tests, 12213 succeeded, 0 failed, 2 skipped; format clean; repository checks and licence check OK); release head 9d4e0075 is tagged v0.15.0, later commits are documentation only. GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at 'Start test services' (rootless podman cannot create a user namespace in the runner container, OQ-168); the local gate is the gate of record. The commands below were not re-run by the author of this record, except the node command, which printed 76 tests, 76 pass, 0 fail when the records pass ran it on 2026-10-28. "Inside the passing <Project> run" means the project passed as a whole, not that the class was run alone.

## Criteria

| Criterion | Command | What it proves | Recorded result |
|-----------|---------|----------------|-----------------|
| 1 | `dotnet test tests/Autonogrammer.Harness.Plugins.Tests -c Release -- --filter-class "*HookEventsTests*" --filter-class "*HooksConfigParserTests*" --filter-class "*HooksConfigLoaderTests*" --filter-class "*HookSourceLocatorTests*" --filter-class "*LocalHookFileSourceTests*" --filter-class "*HookOutputParserTests*" --filter-class "*HookMatcherTests*"` | 21 events with wire names and matcher values; managed, project, user, Claude and plugin sources; command and prompt hooks, matchers, timeouts, `failClosed`, `loop_limit`; invalid files give diagnostics and never throw | Inside the passing Plugins run (429) |
| 1 | `dotnet test tests/Autonogrammer.Harness.Plugins.Tests -c Release -- --filter-class "*HookDispatcherFailurePolicyTests*" --filter-class "*HookDispatcherGatingTests*" --filter-class "*HookDispatcherMergeTests*" --filter-class "*HookDispatcherLoopLimitTests*" --filter-class "*HookDispatcherPromptTests*" --filter-class "*HookDispatcherOmpTests*" --filter-class "*SystemHookCommandRunnerTests*" --filter-class "*HookEnvironmentTests*" --filter-class "*HookInputBuilderTests*" --filter-class "*HookPayloadsTests*" --filter-class "*HookRunLogTests*"` | Exit 0 and exit 2, invalid output, `failClosed`, timeouts, loop budget per hook, merge, prompt hooks, a real `/bin/sh` runner with a scrubbed environment and bounded output, the run log | Inside the passing Plugins run |
| 2 (extension) | `node --test plugins/autonogrammer-hooks-omp/test/*.test.mjs` (also run by `scripts/ci/plugin-node-tests.sh`) | The extension maps omp tools to Cursor names, posts the right payloads per event, denies and short-circuits, fails open or closed per event, honours timeouts, leaks nothing; a real loopback HTTP round trip returns a deny | 76 tests, 76 pass, 0 fail, observed by the records pass 2026-10-28 (the gate also runs it) |
| 2 (bridge and tap) | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*HookBridge*" --filter-class "*AcpHookTapTests*" --filter-class "*AcpHookPayloadsTests*" --filter-class "*HookExtensionEndToEndTests*"` and `dotnet test tests/Autonogrammer.Harness.Acp.Tests -c Release -- --filter-class "*AcpHookGateTests*" --filter-class "*AcpHookNotificationTests*"` | The bridge authenticates, decodes and times out; the ACP tap builds observe-only payloads; the real extension is blocked until the hook is approved and then allowed, with the hook run once | Inside the passing AgentLoop (1221) and Acp (201) runs |
| 2 (fake omp, cloud) | `dotnet test tests/Autonogrammer.AgentWorker.Tests -c Release -- --filter-class "*CloudHooksWorkerTests*" --filter-class "*CloudHookFilterTests*" --filter-class "*HooksConsentSnapshotReaderTests*"` | With a fake omp behind the worker's WebSocket, an approved project hook denies a tool call through the bridge (`permission: deny`, the hook's message) and the run reaches the client as an `_autonogrammer/hook` notification; without a snapshot no project hook runs; a hook outside the snapshot denies without running | Inside the passing AgentWorker run (153). The test posts to the bridge itself; the fake omp does not load the extension |
| 2 (real omp) | none | Blocking events stopped by the extension inside a real omp | UNVERIFIED (OD-07): `omp acp --no-extensions -e plugins/autonogrammer-hooks-omp` with `HOME` and `PI_CODING_AGENT_DIR` set to a throwaway folder, fake keys, `AUTONOGRAMMER_HOOKS_URL` and `AUTONOGRAMMER_HOOKS_TOKEN` pointing at a stub, then a model turn that calls a shell tool |
| 3 | `dotnet test tests/Autonogrammer.Harness.Plugins.Tests -c Release -- --filter-class "*HookConsentIdentityTests*"` and `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*HooksServiceTests*" --filter-class "*HookBridgeApprovalTests*"` | The hash is the same under different checkout directories and changes with any identity field or script content; version 1 stays readable; a guarding hook does not start without consent; an edit invalidates consent and keeps the approved version; project hooks wait for trust; a runner fault quarantines the source | Inside the passing Plugins and AgentLoop runs |
| 4 | `dotnet test tests/Autonogrammer.Presentation.Tests -c Release -- --filter-class "*HooksSettingsPageTests*" --filter-class "*HookRunLogModelTests*" --filter-class "*HookConsentPromptTests*" --filter-class "*HookNoticeRowTests*" --filter-class "*HooksViewModelStateTests*"` and `dotnet test tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*HooksPageViewTests*" --filter-class "*HooksServiceSourceTests*" --filter-class "*HookLogAndNoticeViewTests*"` | The page lists every hook with source, last result and timing; review and approve record consent; the run log and notice rows render; the service adapter maps records | Inside the passing Presentation (2303) and Native runs |
| 4 (wiring) | none beyond the code: `HeadServices.Hooks`, `SessionEngineSet.StartHooksAsync`, `SettingsWindowFactory` | The desktop head constructs `HooksService` and a hooks session per local omp session | Not tested as a unit; UNVERIFIED: a headless test that `HeadServices` opens the service and that a started session's launch plan carries `-e plugins/autonogrammer-hooks-omp` |
| Launch | `dotnet test tests/Autonogrammer.Harness.AgentLoop.Tests -c Release -- --filter-class "*HookBridgeLaunchTests*" --filter-class "*HookBridgePluginTests*"` | The planner adds the extension and the three environment variables only when the plugin is healthy | Inside the passing AgentLoop run |

## Evidence files

| File | What it shows |
|------|---------------|
| `docs/decisions/ADR-0016-hooks-execution-model.md` | The execution model, amending ADR-0013 decision 4 (accepted 2026-10-27) |
| `docs/decisions/addendum-ADR-0020-hooks.md` | Hooks in cloud workers: opt-in, consent snapshot, filter, `_autonogrammer/hook` frame |
| `docs/reference/hooks.md` | The reference (its status banner is stale) |
| `plugins/autonogrammer-hooks-omp/README.md` | The extension and its UNVERIFIED list |
| `scripts/ci/plugin-node-tests.sh` | The gate line that runs the extension tests |

## Known limits of this verification

- No omp process loaded the extension in any test; no live model turn (OD-07); nothing ran on Windows, macOS or Android.
- The cloud test is hermetic: no Railway deploy, no image, no consent snapshot produced by a client.
- Remote ssh sessions run no hooks from the remote host; no test covers that path beyond the notice.
- The records pass ran the node tests only; the .NET commands above carry the recorded project totals, not per-class results.
