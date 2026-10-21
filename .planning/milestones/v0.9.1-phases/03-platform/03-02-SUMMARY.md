---
requirements-completed: [WFLW-01, WFLW-02, WFLW-03]
---

# Phase 3 · Plan 02 Summary — Session-run workflow: direct runner and Temporal worker behind the flag (WFLW-01..03)

**Completed:** 2026-10-20 · **Implementation:** gsd-executor `P3Workflows`, then `P3Workflows-2` (revived after the first pass died), then `P3FixtureReexport` (provenance) and `P3Fix` (review criticals) · **Verified by:** orchestrator

## What landed

- `src/OmpOS.Workflows` (no Temporal reference; verified by grep and csproj): `ISessionRunWorkflow`, `ISessionRunActivities`, `SessionRunInput`, `SessionRunResult`, `IWorkflowContext`, `DirectSessionRunner` (runs provision, prompt, collect, then teardown in `finally`), `DirectWorkflowContext` (injected clock and random).
- `services/OmpOS.Workflows.Worker`: `Program.Main` returns 0 before any Temporal registration when `Workflows:Enabled` is false (verified by test calling `Main` directly). When enabled it registers the Temporal client and worker. The Temporal workflow guards its teardown in `finally` and records failure as status, matching the direct runner (review CR-04). Activities run through `GuardedSessionRunActivities`; fake activities require `Workflows:EnableFakeActivities=true`.
- Replay fixture: `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` is a real export, 29 events, produced by running the workflow end to end against `temporalio/temporal:1.9.1` (`server start-dev`) under podman, started with the container CLI and exported with `temporal workflow show --output json`, with no hand-editing. ADR-0003 was corrected to state exactly that; the earlier claim of an `FetchHistoryAsync` export was not verified and has been removed.

## Verification

- `timeout 600 /root/.dotnet/dotnet test tests/OmpOS.Workflows.Tests` → **20/20 passed**, about 2.3 s.
- `/root/.dotnet/dotnet build services/OmpOS.Workflows.Worker -warnaserror` → 0 errors.
- Phase 3 verifier (static, pre-fix): WFLW-01..03 verified; re-verification pending.

## Deviations and deferrals (explicit; open questions)

- Q-WF-RETRY: the direct runner has no activity retry and fails fast (teardown in `finally`); the Temporal path carries per-activity retry policies. Owner platform.
- The heartbeat on the direct path is a no-op by design; durable heartbeats exist only on the Temporal path.
- Q-SB-WORKER-COMPOSE: real provider composition into the worker is pending; the worker runs guarded activities only.
