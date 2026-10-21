# HO-04: Temporal replay fixtures re-validated on this host (2026-10-21)

Requirement: HO-04 (REQUIREMENTS.md, Human-Only Items on This Host). Re-validate the replay fixtures against a Temporal dev server on this host.

## Environment

- Dev server: container `ompos-temporal` (image `docker.io/temporalio/temporal:1.9.1`, podman), gRPC `127.0.0.1:7233`. `GetSystemInfo` reports server version `1.32.0`.
- SDK: .NET 10.0.402 at `/root/.dotnet`. The Temporalio DLL loaded by the throwaway project is byte-identical to the worker's copy.
- Throwaway project `/tmp/replay-revalidate`, outside the repository and not committed. Repository binaries were built at 13:12 UTC. This wave changed no workflow source.

## Procedure and result

1. Fixture replay. `session-run.json` (workflow id `session-run-001`, 29 events) was replayed with the repository's `TemporalSessionRunWorkflow`. Result: PASS.
2. Live-server history. The workflow was started on the dev server with the repository's input (RunId `run-1`, SessionId `session-1`, SandboxId `sandbox-1`, CorrelationId `corr-1`, Prompt `test prompt`, ResumeFromSeq 0, IdempotencyKey `idem-1`). Workflow id `session-run-ho4-1792589140`, run id `01a15ea3-941a-71e9-859a-9262e1d03494`, task queue `ompos-session-runs`. Result: Completed, FinalSeq 1, 0 artifacts. The 29-event history replayed with the current workflow code: PASS.
   The 29 event types and their order match the committed fixture exactly. Checked with `jq` on `.events[].eventType` for `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` and the live-server history.
3. Activities were stand-ins that return the values recorded in the fixture, wrapped by the repository's `TemporalSessionRunActivities`. `ProvisionSandbox` returned descriptor `sandbox-1`. `RunPrompt` returned 1. `CollectArtifacts` returned `[]`. `TeardownSandbox` completed.
4. Negative control (replay only, no server). Renaming one recorded `RunPrompt` event to `RunPromptMutated` makes replay fail with `WorkflowNondeterminismException` (`TMPRL1100`). The two PASS results are therefore not vacuous.

## Scope

This proves workflow determinism and replay compatibility against live-server histories. It does not exercise the real activities (sandbox provider, harness, omp), because the stand-ins replace them. Real activity behaviour is covered by the composed worker under OD-02 and by `tests/OmpOS.Workflows.Tests`.

## Raw output (run.log, result lines)

```
server_version=1.32.0
workflow_id=session-run-ho4-1792589140 run_id= task_queue=ompos-session-runs
result_status=Completed final_seq=1 artifacts=0 failure_detail=null
result_run_id=01a15ea3-941a-71e9-859a-9262e1d03494
fresh_history_events=29
replay[fixture] PASS events=29 workflow_id=session-run-001
replay[fresh] PASS events=29 workflow_id=session-run-ho4-1792589140
```

Negative control:

```
negative_control renamed_events=1
replay[negative-control] FAIL events=29 workflow_id=session-run-001 Temporalio.Exceptions.WorkflowNondeterminismException: Nondeterminism: ... Activity type of scheduled event 'RunPromptMutated' does not match activity type of activity command 'RunPrompt'
```

The fresh history JSON (`/tmp/replay/fresh-history.json`) and the full logs stay on the host and are not committed.
