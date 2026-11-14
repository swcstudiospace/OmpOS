# 75-11 Summary: scheduled Cowork through the real host

Status: code and docs edited. No build, test, lint or format was run (worker contract). Nothing here is a gate pass.

## Files changed
- `apps/Autonogrammer.Native/Cowork/CoworkSessionHost.cs`: new `CreateScheduledTaskAsync(projectId, trustedFolders, ct)`. Links the task to its project, stores the run's trusted folders after the project's own, sets `MemoryOff = false` (the rail-task policy), and refuses a folder that fails `CoworkPathBoundary.TrustedFolderRefusal` or is not on this device before anything is written. `CreateLinkedTask` now delegates storage to `StoreNewTask`; behaviour is unchanged.
- `apps/Autonogrammer.Native/Cowork/ScheduledCoworkSessionStarter.cs`: rewritten. Create via `CreateScheduledTaskAsync`, open, send. Project and folders are no longer refused. Skip and a model choice are refused. Sentences raised before any task exists are returned as the reason.
- `tests/Autonogrammer.Native.Tests/Cowork/ScheduledCoworkSessionStarterTests.cs`: rewritten, 7 tests (see below).
- `docs/reference/cowork-projects-memory.md`: "Starter" and "Tests" bullets of the scheduled-tasks section.
- `CHANGELOG.md`: one new bullet, line 14, in Unreleased > Added. No other line touched.
- This file.

## Unchanged, with reason
- `AutomationsHost.cs`: the Cowork starter is already registered in the runner constructor (`DeferredCoworkStarter`) before `Start()`. `AgentHead.cs:636` attaches the Cowork host before `Start()` at `AgentHead.cs:333`. Nothing needed changing.
- `AgentHead.cs`: no change needed (see above).
- `CoworkSessionHostTests.cs`: no change. Host coverage sits in the starter tests.
- `HeadAutomationSessions` (Code path): untouched.

## Task status
Task 1 (execute scheduled Cowork through the real host):
- Starter registered before scheduler start: DONE, already in place, no change.
- Project-aware create, project persisted in session metadata: DONE in code. Not run.
- Trusted folders persisted and checked against the boundary and device: DONE in code. Not run.
- Fixed memory mode persisted (`MemoryOff = false`): DONE in code. Not run.
- Approval mode forwarded: PARTIAL, by decision. Manual and Auto both run as Manual; Skip is refused. Reason: Auto's read-only shell and allow-rule approvals would act unattended, while the SessionApprovalMode contract says Auto never approves itself unattended. Manual is the only mode that cannot. Cowork never forwarded a mode before either (the composer's default is Manual), so no existing behaviour changed.
- Model forwarded: NOT DONE. Cowork has no model-selection path: `CoworkEngineRequest`/`CoworkEngineComposer` never pass a model to omp, and the model switch lives in Code's `SessionSlot.Models`, which Cowork does not have. Building one is a new feature with no live omp to verify it against. The run is refused with a stated reason instead.
- Skip: refused in the starter. This is stricter than the runner's isolation check, because the host cannot prove isolation.
- Partial-session cleanup: retained (`CloseSafelyAsync`). A failure while storing the task's metadata marks the row Failed (pre-existing `MarkFailed`) and does not close it.
- Scheduler-to-host test: NOT RUN and NOT A TRUE SCHEDULER TICK. `ARunnerStartThroughTheRealHostCreatesTheProjectTaskAndRecordsARefusalAsFailed` drives `AutomationRunner.StartAsync` into the real host. It does not exercise `AutomationScheduler`.
- Cancellation and failed-send negative tests: NOT WRITTEN. Only the disposed-host failure and the rejections (project missing, folder refused or missing, Skip, model) are covered.
- Verify commands from the plan (`ag-dotnet test` on the three filters): NOT RUN.

Task 2 (documentation and handoff):
- Phase-75 reference updated for the starter behaviour above: DONE (text only).
- CHANGELOG bullet: DONE (text only). States that live omp behaviour is not verified.
- OQ-440, OQ-441, C7 live-omp consumption: left unverified, as the plan requires.

## Exact checks exercised
None. The worker contract forbids builds and tests. Every check listed by the plan is unrun.

## Checks not run (for the integrating agent)
1. `ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*ScheduledCoworkSessionStarterTests*"`
2. `ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkSessionHostTests*"` (existing tests; the host refactor touches `CreateLinkedTask`)
3. `ag-dotnet test --project tests/Autonogrammer.Harness.Automations.Tests -c Release -- --filter-class "*SessionTargetTests*"`
4. Phase-75 gates from 75-RESEARCH.md, as listed in the plan's verification section.
5. Analyzer pass (`CA2007`, `MA0051`, method length, ordinal rules) on the edited files.

## Open items and risks
- The folder-missing notice from `CreateScheduledTaskAsync` (a project folder absent on this device) is dropped by the starter and is not in run history. The task is still created.
- `CoworkSessionHostTests.cs` contains a pre-existing `#pragma warning disable CA2007`. It was not added here, and the contract says no CA2007 pragma. Left as found.
- Whether the new test `AScheduledRunInAProjectLinksItsTaskAndTrustsItsFolders` passes depends on the temp folders sitting outside the boundary's protected and account roots. Expected to hold on Linux; not observed.
