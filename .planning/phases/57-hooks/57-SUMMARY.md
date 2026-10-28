# Phase 57 Summary: Hooks

Status (recorded 2026-10-28): criteria 1 and 3 met; criteria 2 and 4 partial (re-checked against the tree 2026-10-28: the only tests naming `ompos-hooks-omp` are the node-run `HookExtensionEndToEndTests`, which start no omp, the temp-directory rig `HooksServiceRig` and the launch-plan string tests; no test makes a fake omp process load the plugin). The hooks core (21 events, parser, loader, dispatcher, runner, consent identity v2), the loopback bridge, the omp extension `plugins/ompos-hooks-omp`, the ACP tap, `HooksService`, the Hooks page with the run log and the desktop head wiring are built and covered by the recorded passing runs. Blocking events are enforced inside omp by the extension, but no test ran the extension inside a real omp or a fake omp process, and no live model turn ran (OD-07): enforcement against a real omp is UNVERIFIED. Hooks are wired for local desktop sessions; remote ssh sessions run without hook files and without the bridge; cloud workers have the hook code, tests and an addendum, but it is off by default, the worker image does not carry the extension, the client that produces the consent snapshot does not exist, and nothing is deployed. Test results are the recorded per-project runs of 2026-10-28 (Plugins 429, AgentLoop 1221, Acp 201, AgentWorker 153, Presentation 2303, Native 2012 with 2 skipped, Contracts 137, all on the final tree before the last wiring). The full `./bootstrap.sh` gate on a clean worktree of commit e24e6851 exited 0 (recorded 2026-10-28: 12215 tests, 12213 succeeded, 0 failed, 2 skipped; format clean; repository checks and licence check OK); release head 9d4e0075 is tagged v0.15.0, later commits are documentation only. GitHub `ci` runs 40758215402 (main) and 40758275399 (tag) failed at 'Start test services' (rootless podman cannot create a user namespace in the runner container, OQ-168); the local gate is the gate of record.

## Criteria

| Criterion | Status | Evidence |
|-----------|--------|----------|
| 1. `hooks.json` sources parse with 21 events, command and prompt hooks, matchers, timeouts, `failClosed`, `loop_limit`, exit code 2 as deny; invalid files are diagnosed | Met | `HookEventsTests` (the catalogue holds exactly 21 events, wire names, the six permission events, `beforeSubmitPrompt` can block), `HooksConfigParserTests`, `HooksConfigLoaderTests`, `HookSourceLocatorTests`, `HookOutputParserTests`, `HookDispatcherFailurePolicyTests` (exit 0 allow and deny, invalid output, `failClosed`), `HookDispatcherLoopLimitTests`, `HookDispatcherPromptTests`, `HookDispatcherGatingTests`, `SystemHookCommandRunnerTests` (real `/bin/sh`); inside the passing Plugins run (429, recorded 2026-10-28) |
| 2. Blocking events enforced inside omp by the extension; observe-only events from ACP traffic; node tests and an integration test with a fake omp that a denying hook stops a tool call | Partial | Node tests of the extension: `plugins/ompos-hooks-omp/test/bridge.test.mjs` and `e2e.test.mjs` (58 and 2 `test(` declarations; they cover the tool-name mapping, `preToolUse` then `beforeShellExecution`, MCP, `beforeReadFile`, `beforeSubmitPrompt`, `before_subagent_spawn`, deny short-circuit, fail-open and fail-closed, timeouts, no leaks; `e2e.test.mjs` posts to a real loopback HTTP server and gets a deny). The records pass observed `node --test plugins/ompos-hooks-omp/test/*.test.mjs` report 76 tests, 76 pass, 0 fail; `scripts/ci/plugin-node-tests.sh` runs them in the gate. ACP tap: `AcpHookTapTests`, `AcpHookGateTests`, `AcpHookPayloadsTests`. Integration: `HookExtensionEndToEndTests` (AgentLoop; runs the real extension file under node against the real bridge of a `HooksService`; the call is blocked with a consent reason until the hook is approved, then allowed; it starts no omp and skips without node); `CloudHooksWorkerTests.ADenyingProjectHookStopsAToolCallInTheCloud` (AgentWorker; a fake omp script behind the worker's WebSocket, a denying project hook, the bridge answers `permission: deny` with the hook's message and the run arrives as an `_ompos/hook` notification; the test posts to the bridge itself because the fake omp does not load the extension). No test makes a fake omp process load the extension and call a tool that a denying hook stops, and none ran against a real omp |
| 3. First use of a project hook asks for consent on a path-independent hash; changed hooks are quarantined until re-approved | Met | `HookConsentIdentityTests` (the same repository gives the same hash under different checkout directories, editing the named script voids consent, version 2 beside version 1, current version is two), `HooksServiceTests` (a guarding hook does not start without consent and runs after approval, editing a hook invalidates consent and keeps the approved version, project hooks wait for workspace trust, a runner fault quarantines the source), `HookBridgeApprovalTests`; inside the passing Plugins and AgentLoop runs |
| 4. Hooks page and per-run log show every hook, source, last result and timing; the head wiring replaces the test-only `PluginHost` construction (closes OQ-84) | Partial | View models and headless views: `HooksSettingsPageTests`, `HookRunLogModelTests`, `HookConsentPromptTests`, `HookNoticeRowTests`, `HooksViewModelStateTests`, `HooksPageViewTests`, `HooksServiceSourceTests`, `HookLogAndNoticeViewTests`; inside the passing Presentation and Native runs. Wiring in the tree: `HeadServices.Hooks`, `SessionEngineSet.StartHooksAsync`, `SettingsWindowFactory` (Hooks page), `MainView.Shell.cs` (run log and notices). OQ-84 is only partly answered: `PluginHost` and the v1 `HookRunner` are still constructed only in tests (`new PluginHost(` appears in `PluginContractTests` and `HookConsentTests`); the product path is `HooksService`. The `HeadServices` wiring has no test of its own that a head starts the service |

## What the phase delivered

- The Cursor hook model: 21 events, command and prompt hooks, matchers, timeouts, `failClosed`, `loop_limit`, exit 2 as deny, sources managed, project, user, Claude settings and plugins, diagnostics instead of failures.
- Execution as written for Cursor (ADR-0016, amending ADR-0013 decision 4): shell strings with stdin JSON, a scrubbed environment, bounded output.
- Consent on a hash that has no absolute path, quarantine of changed hooks, workspace trust for project hooks, a run log (directory 0700, file 0600) and a Hooks page.
- The OmpOS-owned omp extension and the loopback bridge for blocking events; the ACP tap for events omp reports.
- Desktop wiring for local sessions; hook code, notification frame and tests for cloud workers (`addendum-ADR-0020-hooks.md`).

## Deviations

- Exit code 0 with no output on a permission event is no decision (allow), not an invalid response; only `failClosed` turns it into a deny. Exit code 2 also blocks `beforeSubmitPrompt`. (HooksCore report; recorded in ADR-0016.)
- Stdout cap 1 MiB, stderr 64 KiB, run-log excerpts 4 KiB; all matching hooks run even after a deny; scalars merge last wins.
- Consent v2 also hashes the content of project scripts a command names (a heuristic scanner).
- A managed hooks file is pre-consented only when the file and its directory are not group or other writable; root ownership is not checked and Windows never pre-consents.
- The cloud addendum is named `addendum-ADR-0020-hooks.md`, not `ADR-0020-addendum-hooks.md`, because `DecisionRecordTests` require a unique number and an index row for every `ADR-*.md`.
- The cloud worker composes the shared libraries directly and not `HooksService` (it needs event and hook-type filtering and a consent snapshot, not a user store).
- `docs/reference/hooks.md` still carries a status banner that says the heads do not yet construct the hooks service (OQ-84); the tree constructs it for the desktop head. The banner is stale.

## Partial

- Criterion 2: no integration test runs the extension inside a fake omp process; the closest are `HookExtensionEndToEndTests` and the cloud test, which call the bridge or the extension handler directly. Real omp enforcement is UNVERIFIED.
- Criterion 4: OQ-84 is not closed as worded: the heads use `HooksService`, not `PluginHost` and `HookRunner`.
- Remote ssh sessions: hook files of the remote host are not read, no bridge or extension runs there (`NoHookFiles`, a Degraded notice says so); only plugin hooks and the events omp reports apply.
- Cloud workers: built and tested, off by default (`Worker__Hooks__Enabled`), image does not carry `plugins/ompos-hooks-omp`, no client produces the consent snapshot, not deployed (the staged private worker on Railway, 2026-10-28, ran no hook).
- The hosted API does not push hook run events into the web run stream; the web Hooks page lists files only (`HooksReader`).
- A scripted test that a hook deny stops a call before the approval policy was not written (NativeSessionWiring report).

## Blocked (OQ/OD)

- OD-07: no live omp turn; UNVERIFIED: `omp acp --no-extensions -e plugins/ompos-hooks-omp` with a throwaway `HOME`, fake keys and `OMPOS_HOOKS_URL` and `OMPOS_HOOKS_TOKEN` pointing at a stub: extension load, subagent skip by `ctx.agent.kind`, MCP `sourceInfo`, the `input` event only in interactive mode, the field names in the extension README's UNVERIFIED list.
- OQ-84: the head construction question is answered for hooks (`HooksService`); ADR-0013's three-tier isolation rule and the owner sign-off remain open, and no head loads hook plugins through `PluginHost`.
- OQ-96: default taken (ADR-0016 accepted 2026-10-27); product owner has not confirmed.
- Proposed OQ: should a managed hooks file be pre-consented only when root-owned? .NET has no portable owner check; today only group and other write bits are checked and Windows never pre-consents.
- Proposed OQ: which provider and model run `type: prompt` hooks, and are they allowed in hosted runs? The worker keeps command hooks only.
- Proposed OQ: remote hook files and mention files over the ssh file system (NativeSessionWiring report).
- Windows PowerShell and cmd invocation (`HookShell`) was written to Cursor's description and never run.

## Known limits

- The extension's field names for read, edit and write inputs, the MCP server name parse of `mcp__<server>_<tool>` (wrong for a server name with `_`), the `preCompact.trigger` inference and the thinking field name are listed UNVERIFIED in `plugins/ompos-hooks-omp/README.md`.
- A cloud consent token carries up to 256 hashes (about 23 KiB of header against Kestrel's 32 KiB limit; CloudHosted report, UNVERIFIED).
- The Tab events cannot be produced by OmpOS (`HookEventsTests`).
