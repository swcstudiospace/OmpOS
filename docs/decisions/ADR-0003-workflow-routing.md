# ADR-0003: Workflow Routing — Direct Runner vs Temporal

## Status

Accepted (2026-10-20)

## Context

OmpOS needs to execute session runs (provision sandbox → run prompt → collect artifacts → teardown) with two execution modes:

1. **Direct (in-process)** — default, no external dependencies, fast, suitable for short-lived runs.
2. **Durable (Temporal)** — survives process restarts, provides durable retries/timers, observability, replay.

The Phase 3 platform delivers both: `DirectSessionRunner` in `OmpOS.Workflows` (L2, no Temporal dependency) and a Temporal worker in `services/OmpOS.Workflows.Worker` (L3, Temporalio 1.20.0 + Hosting 1.20.0) behind the `Workflows:Enabled` flag.

## Decision

**Route through Temporal ONLY when a run must survive process restarts or needs durable retries/timers. Everything else stays on `DirectSessionRunner`.**

### Direct Runner (Default)

- **When**: `Workflows:Enabled=false` (default), or explicitly chosen by the caller.
- **Characteristics**:
  - In-process, sequential execution.
  - Teardown in `finally` block (guaranteed cleanup).
  - Deterministic primitives injected via `IWorkflowContext` (`DirectWorkflowContext` uses real time/GUID/random).
  - No external dependencies, zero startup latency.
  - Suitable for: interactive sessions, short prompts, development, CI.

### Temporal Worker (Opt-in)

- **When**: `Workflows:Enabled=true` with valid `Workflows:Address`, `Namespace`, `TaskQueue`.
- **Characteristics**:
  - Runs in separate process (`services/OmpOS.Workflows.Worker`).
  - Same state machine as `DirectSessionRunner` (provision → prompt → collect, teardown in
    `finally` once provisioned) behind its own `[WorkflowRun]` entry point
    (`TemporalSessionRunWorkflow`), over the shared `ISessionRunActivities` contract.
  - Activity methods take the workflow input only; cancellation and heartbeats flow through
    the ambient `ActivityExecutionContext` (never serialized arguments).
  - Activities executed with retry policies (provision: 3×, prompt: 1×, collect: 3×, teardown: 5×).
  - Heartbeats on long activities.
  - Durable timers, survives host crashes, visible in Temporal UI.
  - Determinism enforced by `WorkflowReplayer` over a dev-server-exported history fixture,
    with payloads serialized through a source-generated `DataConverter` (reflection
    serialization is disabled repo-wide for trim/AOT safety).
  - Suitable for: long-running agents, background jobs, runs that must survive deployments, audit requirements.

## Configuration

```json
{
  "Workflows": {
    "Enabled": false,
    "Address": "localhost:7233",
    "Namespace": "default",
    "TaskQueue": "ompos-session-runs"
  }
}
```

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `Enabled` | No | `false` | Master switch. When `false`, worker host exits 0 immediately. |
| `Address` | When Enabled | `localhost:7233` | Temporal server gRPC endpoint. |
| `Namespace` | No | `default` | Temporal namespace. |
| `TaskQueue` | No | `ompos-session-runs` | Task queue for workflow/activity tasks. |

### Validation

- `WorkerOptions` read through the section indexer with manual parsing (no reflection binding,
  which would trip trim warnings that are errors repo-wide) and validated at startup.
- When `Enabled=true`, `Address` must be non-empty (throws `InvalidOperationException` naming
  `Workflows:Address` otherwise).
- Worker host exits with code 0 when `Enabled=false` — no Temporal client, no worker registration.

## Dev Server (Local Development)

The `deploy/compose.yaml` defines a `temporal` service under the `workflows` profile:

```bash
# Start Temporal dev server (includes UI at localhost:8233)
podman-compose --profile workflows up -d temporal

# Equivalent to `temporal server start-dev` (requires temporal CLI)
```

The worker connects to `localhost:7233` by default. The dev server uses an in-memory database (data lost on restart).

## Architecture Constraints

- **No Temporal types in L2 (`OmpOS.Workflows`)** — only `IWorkflowContext` abstraction.
- **Temporal types only in L3 (`services/OmpOS.Workflows.Worker`)** — `[Workflow]`, `[Activity]`, `WorkflowReplayer`, `AddHostedTemporalWorker`.
- **Architecture test** enforces: API/UI projects must not reference Temporal; `OmpOS.Workflows` must not reference Temporal.
- **Directory.Packages.props** pins `Temporalio` 1.20.0 + `Temporalio.Extensions.Hosting` 1.20.0 (same version).

## Replay Testing

Determinism is verified without a Temporal server:

```csharp
var proto = History.Parser.ParseJson(await File.ReadAllTextAsync("TestData/histories/session-run.json"));
var history = new WorkflowHistory("session-run-001", proto.Events);
var replayer = new WorkflowReplayer(
    new WorkflowReplayerOptions { DataConverter = SessionRunDataConverter.Instance }
        .AddWorkflow<TemporalSessionRunWorkflow>());
await replayer.ReplayWorkflowAsync(history, cancellationToken: TestContext.Current.CancellationToken);
```

The fixture `session-run.json` (29 events) is a real dev-server export produced 2026-10-20
with image `docker.io/temporalio/temporal:1.9.1`. The server was started with
`podman run -d --name ompos-temporal -p 127.0.0.1:7233:7233 docker.io/temporalio/temporal:1.9.1 server start-dev --ip 0.0.0.0 --db-filename /home/temporal/temporal.db`
and the worker with `Workflows__Enabled=true Workflows__Address=127.0.0.1:7233 Workflows__Namespace=default Workflows__TaskQueue=ompos-session-runs /root/.dotnet/dotnet run --project services/OmpOS.Workflows.Worker`
(task queue `ompos-session-runs`, namespace `default`). One run was started with the
container's own CLI `podman exec ompos-temporal temporal workflow start --type TemporalSessionRunWorkflow --task-queue ompos-session-runs --workflow-id session-run-001 --input '{"RunId":"run-1","SessionId":"session-1","SandboxId":"sandbox-1","CorrelationId":"corr-1","Prompt":"test prompt","ResumeFromSeq":0,"IdempotencyKey":"idem-1"}'`,
waited to `COMPLETED`, then exported with
`podman exec ompos-temporal temporal workflow show --workflow-id session-run-001 --output json`
saved directly to `tests/OmpOS.Workflows.Tests/TestData/histories/session-run.json` with no
hand-editing of history events. Notes:

- The history JSON is parsed with the protobuf parser, not STJ, so fixture loading is
  unaffected by the repo-wide reflection-serialization switch.
- Replay deserializes payloads with `SessionRunDataConverter` (source-generated), the same
  converter the worker uses — replay therefore also covers the payload serialization path.
- CI runs `dotnet test` on `OmpOS.Workflows.Tests` which includes `TemporalReplayTests`.
  Q-WF-HISTORY (verify the hand-built fixture against a real export) is resolved: no
  hand-built fixture remains.

## Open Questions

None outstanding for workflow routing.

## Consequences

### Positive

- Clear routing criteria prevents over-engineering simple runs.
- Single workflow definition (`ISessionRunWorkflow`) reused by both runners.
- Worker is a separate deployable unit — can scale independently.
- Replay tests catch non-determinism in CI without a Temporal server.

### Negative

- Two code paths to maintain (direct + Temporal adapters).
- Configuration complexity for operators (must know when to enable Temporal).
- Replay fixture may drift from real server exports until Q-WF-HISTORY resolved.

## References

- [Temporal .NET SDK 1.20.0](https://github.com/temporalio/sdk-dotnet/releases/tag/v1.20.0)
- [Temporalio.Extensions.Hosting](https://www.nuget.org/packages/Temporalio.Extensions.Hosting/1.20.0)
- [Workflow Replay Testing](https://dotnet.temporal.io/testing#workflow-replayer)
- Phase 3 Research: `.planning/phases/03-platform/03-RESEARCH.md` (VF-4)
- Phase 3 Context: `.planning/phases/03-platform/03-CONTEXT.md` (D-04)