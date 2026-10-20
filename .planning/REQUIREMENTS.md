# Requirements: OmpOS

**Defined:** 2026-10-20
**Core Value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.

## v1 Requirements

Requirements for the scaffold milestone. Each maps to a roadmap phase. "Real" means implemented and exercised by tests; "stub" means an interface-conforming implementation with contract tests, an owner-tagged TODO and an entry in `docs/open-questions.md`.

### Foundation

- [ ] **FOUND-01**: Developer can build and test the whole Linux-buildable solution from a clean clone with one documented command (`./bootstrap.sh`), which installs/uses the pinned .NET 10 SDK via `global.json` and exits non-zero on any failure.
- [ ] **FOUND-02**: Every project compiles with nullable reference types, `TreatWarningsAsErrors`, .NET analyzers at `AnalysisLevel` latest, deterministic builds and `ContinuousIntegrationBuild` in CI, enforced from `Directory.Build.props`.
- [ ] **FOUND-03**: Package versions are centrally managed (`Directory.Packages.props`) and every project restores with committed `packages.lock.json` files; CI restores with `--locked-mode`.
- [ ] **FOUND-04**: `.editorconfig` is the single formatting/analyzer rule source and `dotnet format --verify-no-changes` passes on the committed tree.
- [ ] **FOUND-05**: A GitHub Actions CI workflow runs restore (locked), build (warnings as errors), tests, format verification, `dotnet list package --vulnerable`, and `podman-compose config` validation on every push/PR; a separate macOS job builds the MAUI head only when the `maui` workload is installable.
- [ ] **FOUND-06**: `OmpOS.Contracts` defines Session, Message, SessionStatus, IntentEnvelope, IntentKind, Diagnostic, UiBlock variants, StreamEvent (with monotonic `Seq`), SandboxDescriptor, SandboxStatus, WorkflowRun, WorkflowRunStatus, OmpOSError/ErrorCode and ContractVersion once, with System.Text.Json source-generated serialization; no other project redefines these shapes.
- [ ] **FOUND-07**: An architecture test (ArchUnitNET) fails the build when any project references a higher layer (L0 Contracts → L1 Parser/Harness.Abstractions/Sandbox.Abstractions → L2 Harness/Sandbox/Sessions/Workflows → L3 Api/Worker → L4 apps) or when anything other than tests references `OmpOS.Api`.
- [ ] **FOUND-08**: A `compose.yaml` validated by `podman-compose config` defines the local dependency stack (Temporal dev server via `temporalio/temporal` CLI image with `server start-dev`); no Docker-only tooling exists in the repo.
- [ ] **FOUND-09**: Repository documentation skeleton exists: `README.md` (bootstrap/run/test), `CONTRIBUTING.md`, `docs/architecture.md`, `docs/decisions/ADR-0001-monorepo-layout.md`, `docs/decisions/ADR-0002-surface-technology.md`, `docs/open-questions.md`, `docs/runbooks/local-dev.md`, `docs/runbooks/sandbox.md`.

### Prompt Parser

- [ ] **PARSE-01**: Every input string, including empty/whitespace-only, oversized (documented `MaxPromptLength` = 64,000 chars with truncation diagnostic), mixed newlines, unicode/emoji, control characters and injection-style text, yields exactly one `IntentEnvelope` and the pipeline never throws to the caller.
- [ ] **PARSE-02**: Ordered recognizers detect slash commands (`/cmd args`), `@mentions`/file references, structured directives, and omp magic keywords (`ultrathink`, `orchestrate`, `workflowz`, `jevify`) using omp's documented matching rules (lowercase standalone prose; fenced code, inline code and HTML ignored), and command-lookalike text inside code fences is treated as plain text.
- [ ] **PARSE-03**: The fallback recognizer is hardwired last and cannot be removed or reordered by registration; the envelope preserves `RawText` byte-for-byte alongside `NormalizedText`, `IntentKind`, `Arguments`, `Resources`, `Confidence` and `Diagnostics`, and recognizer exceptions are captured as diagnostics.
- [ ] **PARSE-04**: New recognizers register through `IPromptRecognizer` (with `Order`) via DI without changes to the pipeline core, proven by a test that registers a custom recognizer.

### ACP Harness

- [ ] **HARN-01**: `OmpOS.Harness.Abstractions` exposes `IAcpTransport` (connect/send/receive/close), `IOmpSession` (create/resume/cancel/dispose, capability negotiation, `SubmitPromptAsync` returning `IAsyncEnumerable<HarnessEvent>`), the `HarnessEvent` union (TextDelta, Thought, ToolCall, ToolCallUpdate, PermissionRequest, Plan, UiBlock, Error, Completed) and a typed `HarnessException` tree (ProcessStart, HandshakeTimeout, MalformedFrame, Disconnected, Backpressure, DuplicateCompletion).
- [ ] **HARN-02**: `HarnessState` (Disconnected, Connecting, Handshaking, Ready, Streaming, Cancelling, Faulted, Disposed) is an explicit state machine with a static allowed-transition table; invalid transitions throw `InvalidHarnessTransitionException` and every transition is covered by tests.
- [ ] **HARN-03**: A deterministic script-driven `InMemoryOmpAdapter` emits a configurable `HarnessEvent` sequence (including a Generative UI block) so the vertical slice and UI run without omp installed; failure-mode tests cover process-start failure, handshake timeout, malformed frame, mid-stream disconnect, consumer backpressure (bounded channel) and duplicate completion.
- [ ] **HARN-04**: A real ACP v1 client (JSON-RPC 2.0 over newline-delimited stdio, source-generated STJ) implements `initialize`, `authenticate`, `session/new`, `session/load`, `session/prompt`, `session/cancel`, `session/update` notifications, `session/request_permission` (agent→client, answered through an approval policy hook that defaults to deny-by-policy and may only deny earlier, never grant more), `fs/read_text_file`/`fs/write_text_file` (desktop capability flags), and `$/cancel_request`; a smoke test spawns the locally installed `omp acp`, completes `initialize` + `session/new` + `session/close` and asserts the advertised capabilities, skipping when `omp` is absent.
- [ ] **HARN-05**: A real omp RPC JSONL client implements the `ready` frame handshake, `negotiate_protocol` v2 with `rpc_chunk` reassembly (1 MiB physical / 64 MiB logical caps), id-correlated command/response matching independent of emission order, `prompt` admission vs `prompt_result` completion, `abort`/`abort_and_restore_queue`, and concurrent stderr draining; a smoke test spawns `omp --mode rpc`, receives `ready`, negotiates v2 and runs `get_state`, skipping when `omp` is absent.
- [ ] **HARN-06**: A remote WebSocket transport is a bounded stub (typed `NotAvailable` result, contract-test fixture passes, owner-tagged open question); abstract `OmpSessionContractTests<T>` and `AcpTransportContractTests<T>` run against the in-memory implementations and are reusable for real ones.

### Session Orchestration & Streaming API

- [ ] **SESS-01**: `Session` carries Id, OwnerId, CreatedAt/UpdatedAt, SessionStatus (Idle, Running, AwaitingToolApproval, Completed, Failed, Cancelled), optional SandboxId, ordered Messages and an append-only `StreamEvent` log with monotonic Seq; `ISessionRepository` (get/save/append/list-from-seq) has an in-memory implementation and an abstract `SessionRepositoryContractTests<T>` fixture.
- [ ] **SESS-02**: `SessionService` enforces one active prompt per session — a concurrent submission is rejected with `OmpOSError` code `PromptInFlight` (never queued silently); steer/follow-up intents are a distinct kind exempt from that rejection and documented as a queued-message policy.
- [ ] **SESS-03**: Harness events are folded into `StreamEvent`s and `UiBlock`s by a single pure projection, so replaying the event log reproduces identical UI state (tested by replay equality).
- [ ] **API-01**: `OmpOS.Api` (ASP.NET Core 10 minimal API) exposes typed, validated endpoints to create/list/get sessions and submit prompts, with request size limits, content-type enforcement, a pluggable per-session rate limiter (in-memory default), CORS locked to configured origins, and an OpenAPI 3.1 document committed via build-time generation.
- [ ] **API-02**: `GET /sessions/{id}/events` streams `StreamEvent`s as Server-Sent Events with `id:` = Seq, honours `Last-Event-ID`/`since` to replay from the log, ends with a terminal event, and an integration test proves disconnect → reconnect → resume without gaps or duplicates (client-side de-duplication by Seq).
- [ ] **API-03**: A correlation ID is accepted/generated at the API boundary and propagated through logs of API → session service → harness → sandbox calls (structured logging with prompt/secret redaction by default and an explicit debug opt-in).

### Platform Foundations & Security

- [ ] **OPS-01**: Layered typed configuration (defaults → appsettings → environment → local overrides) with `ValidateOnStart` options classes; startup fails fast with a readable message naming the missing variable; `.env.example` documents every variable.
- [ ] **OPS-02**: `/health/live` and `/health/ready` endpoints exist; readiness reflects harness, repository and (when `Workflows:Enabled`) Temporal connectivity.
- [ ] **OPS-03**: OpenTelemetry tracing/metrics hooks exist with spans across the vertical slice and metrics for session count, stream latency, sandbox provision time and parser fallback rate (console/OTLP exporter configurable).
- [ ] **OPS-04**: Auth boundary is middleware with a no-op single-dev-principal provider (`IUserContext`) and a documented extension point; user text is never concatenated into system/control messages; no secrets are committed (verified by a CI secret scan step).

### Sandbox Provisioning

- [ ] **SBOX-01**: `ISandboxProvider` (provision/attach/exec/stream-logs/snapshot/teardown) takes requests with explicit timeouts and idempotency keys and reports `SandboxStatus` (Requested, Provisioning, Ready, Attached, Degraded, TearingDown, Destroyed, Failed).
- [ ] **SBOX-02**: `SandboxLifecycle` enforces a TTL, exposes an orphan-reaper hook, retries teardown with jittered backoff, and guarantees a failed provisioning never leaves a session in `Running` (tested).
- [ ] **SBOX-03**: `FakeSandboxProvider` passes the abstract `SandboxProviderContractTests<T>` fixture; `LocalPodmanComposeProvider` shells out to `podman-compose`/`podman` via an injectable process runner (tested with a fake runner, real invocation skipped when podman is absent).
- [ ] **SBOX-04**: `OpenShellProvider` (podman compute driver; gateway TOML and policy YAML v1 generated from a typed policy model), `AgentOsProvider`, `RailwaySwarmProvider` and `AttestedEnclaveProvider` (dstack-shaped attestation contract) are interface-conforming stubs returning typed `NotAvailable` results, each with an owner-tagged open question; no vendor API is invented.
- [ ] **SBOX-05**: Sandbox isolation defaults are explicit settings: no host paths or host secrets mounted, per-sandbox credentials, outbound network policy default-deny.

### Durable Workflows

- [ ] **WFLW-01**: `ISessionRunWorkflow` and `ISessionRunActivities` (provision → run prompt → collect result → teardown, teardown in finally) are defined once; `DirectSessionRunner` executes them in-process when `Workflows:Enabled=false` (the default) so the vertical slice needs no Temporal server.
- [ ] **WFLW-02**: `OmpOS.Workflows.Worker` hosts a Temporal .NET worker (`Temporalio.Extensions.Hosting`) with the deterministic `SessionRunWorkflow` and activities carrying retry policies and heartbeats; it starts only when `Workflows:Enabled=true` and a workflow replay test passes; the decision criteria for routing a path through Temporal are documented.
- [ ] **WFLW-03**: Abstract `SessionRunWorkflowContractTests<T>` runs against the direct runner and is reusable for the Temporal implementation.

### Shared Chat Surface (Razor Class Library)

- [ ] **UI-01**: `OmpOS.UI` provides a conversation list, a session view with streamed messages, a composer, and a `UiBlock` renderer covering markdown text, code block (copy), tool-call card with status, permission card rendering agent-provided options verbatim, plan panel, file-diff placeholder, error block, A2UI v0.9.1 surface (fixed component catalog subset) and an unknown-block fallback that shows the raw payload collapsed.
- [ ] **UI-02**: Streaming UX: deltas render incrementally, a stop/cancel control is shown while streaming, partial output is preserved on failure with a retry affordance, auto-scroll pauses when the user scrolls up with a jump-to-latest control, and UI states (loading history, empty, streaming, awaiting approval, recoverable/fatal error, reconnecting, offline, success) are explicit.
- [ ] **UI-03**: Accessibility baseline: composer and all controls keyboard-operable (Enter send / Shift+Enter newline / Esc stop), visible focus rings, labelled icon buttons, WCAG AA contrast tokens, throttled `aria-live` announcements, `prefers-reduced-motion` honoured, ≥44px touch targets; verified by an automated check (bUnit assertions and an axe-core run against the published web app where tooling exists).
- [ ] **UI-04**: The UI is a pure fold over `StreamEvent`s: the same event log renders identical block trees in bUnit tests, and unknown events never blank or crash the view.

### Web, Desktop and Mobile Shells

- [ ] **WEB-01**: `apps/OmpOS.Web` is a Blazor WebAssembly standalone app hosting `OmpOS.UI`, reads the API base URL from environment-driven configuration (no hardcoded hosts), consumes the SSE stream with response streaming enabled, and completes the vertical slice end-to-end against the in-memory adapter in a browser smoke run.
- [ ] **WEB-02**: `vercel.json` (dotnet-install `installCommand`, `dotnet publish` build, static `outputDirectory`, SPA rewrite, CSP/security headers) deploys the static publish output to Vercel without any server component.
- [ ] **DESK-01**: `apps/OmpOS.App` (.NET MAUI Blazor Hybrid) shares `OmpOS.UI`; on desktop targets (Windows, macCatalyst) it hosts the harness in-process via an `IEngineHost` registered for local stdio spawning of `omp acp`, can alternatively point at the remote backend, and stubs window-state persistence, single-instance guard and crash-safe session recovery behind interfaces.
- [ ] **MOB-01**: On mobile targets (iOS, Android) the same MAUI head registers a remote-only `IEngineHost`, never spawns processes, and reconnects the SSE stream with sequence resume on `Window.Resumed`.
- [ ] **SHELL-01**: The MAUI head is excluded from the default Linux solution filter, built in the macOS CI job, and the root README states which heads were verified where.

### Verification & Handoff

- [ ] **VERIFY-01**: A clean-clone bootstrap run (fresh `git clone` into a temp dir → `./bootstrap.sh`) succeeds and is recorded in `docs/runbooks/local-dev.md`.
- [ ] **VERIFY-02**: `docs/open-questions.md` lists every unverified external fact (omp session/update variants emitted in real turns, A2UI payload channel from omp, agentOS hosting of omp, OpenShell gRPC contract, Railway Cloud Swarm endpoints/auth, dTEE/dstack attestation verifier, Vercel build-image details, Podman 5.x availability) with an owner tag, and every TODO in code links to an entry.
- [ ] **VERIFY-03**: The final report (`docs/handoff.md`) distinguishes implemented, stubbed and deferred items against the acceptance criteria AC_1–AC_11 of the ultrathink specification.

## v2 Requirements

Deferred to a later milestone. Tracked but not in the current roadmap.

### Engine

- **ENG-01**: Steering/follow-up queue chips over omp RPC (`steer`, `follow_up`, `queue_update`, `promote_queued_message`).
- **ENG-02**: Background subagents panel (`set_subagent_subscription`, `subagent_*` frames).
- **ENG-03**: Desktop terminal capability (`terminal/*`) and terminal embeds.
- **ENG-04**: Real diff renderer (hunks, side-by-side/stacked), session fork tree, `/btw` side questions.

### Platform

- **PLAT-01**: Live OpenShell provisioning on a Podman 5.x host; agentOS hosting evaluation; dstack attestation verification.
- **PLAT-02**: Cross-surface continuity (desktop-hosted harness publishing to the backend; approve from phone) with idempotent permission answers.
- **PLAT-03**: Persistent session store (PostgreSQL) and multi-replica SSE fan-out with session affinity or an external event log.

### Product

- **PROD-01**: A2UI v1.0 client→server actions, usage meter and compaction dividers, notices and desktop attention notifications, rename/export/search.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Authentication providers (OAuth/SSO, multi-tenant) | `omp acp` uses a single `agent` auth method; identity forks the data model before the core loop is validated — single dev principal with reserved `OwnerId` |
| Billing / usage metering | omp owns provider cost; show agent-reported usage read-only only |
| Model routing / curated model picker | omp returns 739 model options; a generic config-option selector rendered verbatim is the honest surface |
| Plugin / extension marketplace | omp manages extensions, skills and MCP; client only surfaces `available_commands_update` |
| Client-side tool execution engine | Duplicates omp and breaks the approval model; only ACP client capabilities are implemented |
| Full code editor / IDE panes | Chat-first product; read-only diff/file viewers only |
| Mobile push notifications | Needs APNs/FCM and a device registry; in-app resume with pending-approval badge instead |
| Fully offline execution on web/mobile | Agent runs elsewhere; offline means read-only history at most |
| Real-time multi-user collaboration | Presence/conflict/permission attribution unsolved in the single-owner model |
| Voice / live audio | `omp acp` does not advertise audio |
| Yolo-by-default approvals | Deny-by-policy posture; `omp acp` launched with an explicit approval mode |
| Docker-based tooling | podman-compose/podman only (user requirement) |
| TypeScript web client | User chose Blazor WASM; OpenAPI kept for future generation |
| Live Railway/Vercel/Temporal Cloud resources, app-store packaging/signing | Config-as-code and runbooks only in this milestone |
| Modifying the existing Railway Cloud Swarm | External system; integration boundary + open question only |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| FOUND-01 | TBD | Pending |
| FOUND-02 | TBD | Pending |
| FOUND-03 | TBD | Pending |
| FOUND-04 | TBD | Pending |
| FOUND-05 | TBD | Pending |
| FOUND-06 | TBD | Pending |
| FOUND-07 | TBD | Pending |
| FOUND-08 | TBD | Pending |
| FOUND-09 | TBD | Pending |
| PARSE-01 | TBD | Pending |
| PARSE-02 | TBD | Pending |
| PARSE-03 | TBD | Pending |
| PARSE-04 | TBD | Pending |
| HARN-01 | TBD | Pending |
| HARN-02 | TBD | Pending |
| HARN-03 | TBD | Pending |
| HARN-04 | TBD | Pending |
| HARN-05 | TBD | Pending |
| HARN-06 | TBD | Pending |
| SESS-01 | TBD | Pending |
| SESS-02 | TBD | Pending |
| SESS-03 | TBD | Pending |
| API-01 | TBD | Pending |
| API-02 | TBD | Pending |
| API-03 | TBD | Pending |
| OPS-01 | TBD | Pending |
| OPS-02 | TBD | Pending |
| OPS-03 | TBD | Pending |
| OPS-04 | TBD | Pending |
| SBOX-01 | TBD | Pending |
| SBOX-02 | TBD | Pending |
| SBOX-03 | TBD | Pending |
| SBOX-04 | TBD | Pending |
| SBOX-05 | TBD | Pending |
| WFLW-01 | TBD | Pending |
| WFLW-02 | TBD | Pending |
| WFLW-03 | TBD | Pending |
| UI-01 | TBD | Pending |
| UI-02 | TBD | Pending |
| UI-03 | TBD | Pending |
| UI-04 | TBD | Pending |
| WEB-01 | TBD | Pending |
| WEB-02 | TBD | Pending |
| DESK-01 | TBD | Pending |
| MOB-01 | TBD | Pending |
| SHELL-01 | TBD | Pending |
| VERIFY-01 | TBD | Pending |
| VERIFY-02 | TBD | Pending |
| VERIFY-03 | TBD | Pending |

**Coverage:**
- v1 requirements: 49 total
- Mapped to phases: 0
- Unmapped: 49 ⚠️

---
*Requirements defined: 2026-10-20*
*Last updated: 2026-10-20 after initial definition*
