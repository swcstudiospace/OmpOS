<!-- Tracking: ultrathink node n2 "Decompose into monorepo layout and dependency direction" — SPE-17752 (https://linear.app/swcstudio/issue/SPE-17752); node n8 "Synthesize ordered execution plan in parallel waves" — SPE-17756 (https://linear.app/swcstudio/issue/SPE-17756) -->

# Roadmap: OmpOS

## Overview

OmpOS goes from an empty repository to a scaffolded, verifiable full-stack agent workspace in five fixed phases. Foundation lays the monorepo, the single-source-of-truth contracts and the mechanical guardrails (analyzers, lock files, architecture test, CI, compose). Core builds the product's correctness centre: the catch-all prompt parser, the ACP/RPC harness with its explicit state machine and scripted in-memory adapter, the append-only sequence-numbered session log, and the resumable SSE API with its security/observability foundations. Platform adds the resource-owning layer — sandbox providers (fake + local podman-compose + interface-conforming vendor stubs) and durable workflows (direct runner by default, Temporal worker behind a flag). Surfaces projects the event log into Generative UI through one shared Razor Class Library hosted by a Blazor WASM web head on Vercel and a MAUI Blazor Hybrid desktop/mobile head, proving the vertical slice end-to-end against the in-memory adapter. Verify & Handoff proves a clean clone bootstraps, runs every test and architecture check, finalises the docs and open questions, and reports implemented vs stubbed vs deferred against the ultrathink acceptance criteria.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Foundation** - Monorepo layout, solution, shared contracts, analyzers/formatter, locked CPM, CI, podman compose, docs skeleton + ADR-0001/0002 — complete (human-deferred: remote CI run, binary compose run)
- [x] **Phase 2: Core** - Prompt parser, ACP harness (abstractions, state machine, in-memory adapter, real ACP v1 + omp RPC clients, WebSocket stub), session orchestration, SSE streaming API, platform foundations/security — complete (deferred: interactive approval, Q-HARN-PERMISSION)
- [x] **Phase 3: Platform** - Sandbox provider abstraction + fake/local podman-compose providers + vendor stubs; durable workflows (direct runner + Temporal worker behind flag) — complete (deferred: worker composition Q-SB-WORKER-COMPOSE, direct-path retry Q-WF-RETRY, credential issuance Q-SB-CREDENTIALS)
- [x] **Phase 4: Surfaces** - Shared Razor Class Library chat surface with Generative UI renderer, Blazor WASM web head on Vercel, MAUI Blazor Hybrid desktop/mobile head, vertical slice end-to-end against the in-memory adapter — complete for RCL + web head (human-deferred: MAUI binaries on macOS; outstanding: live Vercel deploy WEB-02)
- [x] **Phase 5: Verify & Handoff** - Clean-clone bootstrap test, full test/architecture run, docs finalised, open questions populated, implemented-vs-stubbed-vs-deferred report — complete (VERIFY-01 recorded from a clean clone; handoff in docs/handoff.md)

## Phase Details

### Phase 1: Foundation

**Goal**: A developer can clone the repo, run one command, and get a green, warning-free, locked, formatted build of a monorepo whose shared contracts and layer direction are enforced mechanically
**Depends on**: Nothing (first phase)
**Requirements**: FOUND-01, FOUND-02, FOUND-03, FOUND-04, FOUND-05, FOUND-06, FOUND-07, FOUND-08, FOUND-09
**Success Criteria** (what must be TRUE):
  1. `./bootstrap.sh` on a Linux box with no .NET installed installs the SDK pinned in `global.json` (10.0.x), restores with `--locked-mode`, builds with warnings-as-errors and runs the test suite, exiting 0; any failing step exits non-zero.
  2. `dotnet format --verify-no-changes` and `dotnet list package --vulnerable` both pass on the committed tree, and every project inherits nullable, `TreatWarningsAsErrors`, latest `AnalysisLevel`, deterministic build and CPM from `Directory.Build.props`/`Directory.Packages.props` with committed `packages.lock.json` files.
  3. `OmpOS.Contracts` compiles Session, Message, SessionStatus, IntentEnvelope, IntentKind, Diagnostic, UiBlock variants, StreamEvent (`Seq`), SandboxDescriptor, SandboxStatus, WorkflowRun, WorkflowRunStatus, OmpOSError/ErrorCode and ContractVersion with an STJ source-generated context and `JsonSerializerIsReflectionEnabledByDefault=false`, and the ArchUnitNET test fails when a project references a higher layer or a non-test project references `OmpOS.Api`.
  4. `.github/workflows/ci.yml` runs locked restore, build, tests, format verification, vulnerability audit and `podman-compose config` on push/PR, with a separate gated macOS MAUI job; `compose.yaml` validates with `podman-compose config` and no Docker-only tooling exists in the repo.
  5. `README.md`, `CONTRIBUTING.md`, `docs/architecture.md`, `docs/decisions/ADR-0001-monorepo-layout.md`, `docs/decisions/ADR-0002-surface-technology.md`, `docs/open-questions.md`, `docs/runbooks/local-dev.md` and `docs/runbooks/sandbox.md` exist with bootstrap/run/test instructions and the two decisions recorded.

**Plans**: TBD
- [x] 01-01-PLAN.md
- [x] 01-02-PLAN.md
- [x] 01-03-PLAN.md

**UI hint**: no

### Phase 2: Core

**Goal**: Any prompt string becomes exactly one IntentEnvelope, runs through a typed ACP/RPC harness with an explicit state machine and a scripted in-memory adapter, lands in an append-only sequence-numbered session log, and streams out of a secured, observable API as resumable Server-Sent Events
**Depends on**: Phase 1
**Requirements**: PARSE-01, PARSE-02, PARSE-03, PARSE-04, HARN-01, HARN-02, HARN-03, HARN-04, HARN-05, HARN-06, SESS-01, SESS-02, SESS-03, API-01, API-02, API-03, OPS-01, OPS-02, OPS-03, OPS-04
**Success Criteria** (what must be TRUE):
  1. Parser tests prove that empty, whitespace-only, >64,000-char, mixed-newline, unicode/emoji, control-character and injection-style inputs each yield exactly one `IntentEnvelope` with `RawText` preserved byte-for-byte and no exception; slash commands, `@mentions`, structured directives and omp magic keywords are recognised per omp's rules (ignored inside fences/inline code/HTML); a custom `IPromptRecognizer` registered via DI is invoked in `Order` and the fallback cannot be removed or reordered.
  2. Every `HarnessState` transition in the allowed-transition table is covered by tests, invalid transitions throw `InvalidHarnessTransitionException`, and the `InMemoryOmpAdapter` plus the WebSocket stub pass the abstract `OmpSessionContractTests<T>`/`AcpTransportContractTests<T>` fixtures, including failure-mode tests for process-start failure, handshake timeout, malformed frame, mid-stream disconnect, bounded-channel backpressure and duplicate completion.
  3. With `omp` installed, the ACP v1 smoke test spawns `omp acp`, completes `initialize` + `session/new` + `session/close` and asserts advertised capabilities, and the RPC smoke test spawns `omp --mode rpc`, receives `ready`, negotiates protocol v2 and runs `get_state`; both skip cleanly when `omp` is absent; `session/request_permission` is answered by a deny-by-policy hook that can only deny earlier, never grant more.
  4. `SessionService` rejects a concurrent prompt on a running session with `OmpOSError` code `PromptInFlight` (steer/follow-up intents exempt), `InMemorySessionRepository` passes `SessionRepositoryContractTests<T>`, and replaying a session's `StreamEvent` log through the single pure projection reproduces an identical block tree (replay-equality test).
  5. `OmpOS.Api` serves create/list/get session and submit-prompt endpoints (validated, size-limited, content-type enforced, rate-limited, CORS-locked, OpenAPI 3.1 document committed), `GET /sessions/{id}/events` streams SSE with `id:` = `Seq`, honours `Last-Event-ID`/`since` and ends with a terminal event — an integration test proves disconnect → reconnect → resume with zero gaps and zero duplicates; `/health/live` and `/health/ready` respond; startup fails fast naming any missing configuration variable; a correlation ID flows through redacted structured logs and OTel spans; the no-op `IUserContext` middleware is in place and the CI secret scan passes.

**Plans**: TBD
**UI hint**: no

### Phase 3: Platform

**Goal**: A session run can be placed inside an operator-controlled isolation boundary and executed durably — a fake and a local podman-compose sandbox provider work for real, every vendor boundary is an honest interface-conforming stub, and the provision → run → collect → teardown workflow runs in-process by default or on a Temporal worker behind a flag
**Depends on**: Phase 2
**Requirements**: SBOX-01, SBOX-02, SBOX-03, SBOX-04, SBOX-05, WFLW-01, WFLW-02, WFLW-03
**Success Criteria** (what must be TRUE):
  1. `FakeSandboxProvider` and `LocalPodmanComposeProvider` (via an injectable process runner, real `podman-compose` invocation skipped when podman is absent) both pass the abstract `SandboxProviderContractTests<T>` fixture, exercising provision/attach/exec/stream-logs/snapshot/teardown with explicit timeouts, idempotency keys and every `SandboxStatus` value.
  2. `SandboxLifecycle` tests prove TTL expiry triggers teardown, the orphan-reaper hook fires, teardown retries with jittered backoff, and a failed provisioning never leaves a session in `Running`; the isolation defaults (no host paths/secrets mounted, per-sandbox credentials, outbound default-deny) are explicit, documented settings.
  3. `OpenShellProvider` (typed policy model → gateway TOML + policy YAML v1), `AgentOsProvider`, `RailwaySwarmProvider` and `AttestedEnclaveProvider` (dstack-shaped attestation contract) compile against `ISandboxProvider`, return typed `NotAvailable`/`Unverified` results under the contract fixture, and each carries an owner-tagged entry in `docs/open-questions.md` with no invented vendor API calls.
  4. With `Workflows:Enabled=false` (default) `DirectSessionRunner` executes `ISessionRunWorkflow` end-to-end in-process with teardown in `finally`, passing `SessionRunWorkflowContractTests<T>` without any Temporal server; with `Workflows:Enabled=true` `OmpOS.Workflows.Worker` hosts the deterministic `SessionRunWorkflow` with retrying, heartbeating activities, the Temporal replay test passes, and `/health/ready` reflects Temporal connectivity; the routing decision criteria are documented.

**Plans**: TBD
**UI hint**: no

### Phase 4: Surfaces

**Goal**: A prompt typed into the web, desktop or mobile surface renders as live, accessible Generative UI blocks streamed from the backend — the full vertical slice runs in a real browser against the in-memory adapter with no omp, Temporal, Railway or cloud credential
**Depends on**: Phase 2 (scripted adapter + SSE API); Phase 3 for the readiness/workflow wiring the shells display
**Requirements**: UI-01, UI-02, UI-03, UI-04, WEB-01, WEB-02, DESK-01, MOB-01, SHELL-01
**Success Criteria** (what must be TRUE):
  1. In `OmpOS.UI` a user sees a conversation list, a session view with streamed messages and a composer, and every `UiBlock` variant renders — markdown, code (copy), tool-call card with status, permission card with verbatim agent options, plan panel, file-diff placeholder, error, A2UI v0.9.1 surface from a fixed catalog subset, and an unknown-block fallback showing the raw payload collapsed; bUnit tests prove the same event log renders an identical block tree and unknown events never blank or crash the view.
  2. While streaming, deltas appear incrementally, a stop/cancel control is visible, partial output is preserved with a retry affordance on failure, auto-scroll pauses on scroll-up with a jump-to-latest control, and every UI state (loading history, empty, streaming, awaiting approval, recoverable/fatal error, reconnecting, offline, success) is explicitly rendered.
  3. The composer and all controls are keyboard-operable (Enter send / Shift+Enter newline / Esc stop) with visible focus rings, labelled icon buttons, WCAG AA tokens, throttled `aria-live`, `prefers-reduced-motion` honoured and ≥44px touch targets, verified by bUnit assertions and an axe-core run against the published web app.
  4. `apps/OmpOS.Web` (Blazor WASM standalone) reads the API base URL from environment-driven config, consumes the SSE stream with response streaming enabled, and a browser smoke run completes the vertical slice — prompt → parser → session → in-memory adapter → SSE → rendered blocks — with the first block visible before stream end; `vercel.json` publishes the static output (install command, publish build, output directory, SPA rewrite, CSP/security headers) with no server component.
  5. `apps/OmpOS.App` (MAUI Blazor Hybrid) shares `OmpOS.UI` unchanged: desktop targets register an in-process `IEngineHost` spawning `omp acp` over stdio (or point at the remote backend) with window-state persistence, single-instance guard and crash-safe recovery stubbed behind interfaces; mobile targets register a remote-only `IEngineHost`, never spawn processes, and resume the SSE stream by sequence on `Window.Resumed`; the MAUI head is excluded from the Linux solution filter, built in the macOS CI job, and the README states which heads were verified where.

**Plans**: TBD
**UI hint**: yes

### Phase 5: Verify & Handoff

**Goal**: The scaffold is proven from a clean clone, every test and architecture check is green, the documentation is complete and honest, and the handoff report states exactly what is implemented, stubbed and deferred against the ultrathink acceptance criteria
**Depends on**: Phase 4
**Requirements**: VERIFY-01, VERIFY-02, VERIFY-03
**Success Criteria** (what must be TRUE):
  1. A fresh `git clone` into a temp directory followed by `./bootstrap.sh` exits 0 (locked restore, warning-free build, all tests including architecture and format verification), and the run is recorded with date and environment in `docs/runbooks/local-dev.md`.
  2. `docs/open-questions.md` lists every unverified external fact — omp `session/update` variants in real turns, A2UI payload channel from omp, agentOS hosting of omp, OpenShell gRPC contract, Railway Cloud Swarm endpoints/auth, dTEE/dstack attestation verifier, Vercel build-image details, Podman 5.x availability — each with an owner tag, and every `TODO` in code links to an entry (checked by a test or script that fails on an unlinked TODO).
  3. `docs/handoff.md` maps each acceptance criterion AC_1–AC_11 of the ultrathink specification to implemented, stubbed or deferred with evidence (test names, file paths), and `docs/architecture.md`, the ADRs, runbooks, README and CONTRIBUTING reflect the final tree.

**Plans**: TBD
**UI hint**: no

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5. Within Phase 2 the real `omp acp`/`omp --mode rpc` adapters (HARN-04, HARN-05) are the last plans so Phase 4 surface work can overlap with them (`parallelization: true`); Phase 3 and Phase 4 both depend on Phase 2's scripted adapter + API and may be planned as parallel waves, with Phase 4 consuming Phase 3's readiness/workflow wiring only for display.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Foundation | 3/3 | Complete (human-deferred: remote CI run, binary compose run) | 2026-10-20 |
| 2. Core | complete | Complete (deferred: interactive approval Q-HARN-PERMISSION) | 2026-10-21 |
| 3. Platform | complete | Complete (deferred: Q-SB-WORKER-COMPOSE, Q-WF-RETRY, Q-SB-CREDENTIALS) | 2026-10-21 |
| 4. Surfaces | complete | Complete for RCL + web head (human-deferred: MAUI binaries; outstanding: WEB-02 live deploy) | 2026-10-21 |
| 5. Verify & Handoff | complete | VERIFY-01..03 complete; clean-clone run recorded; handoff in docs/handoff.md | 2026-10-21 |

---
*Roadmap created: 2026-10-20*
*Granularity: coarse · Mode: mvp · Coverage: 49/49 v1 requirements mapped*
