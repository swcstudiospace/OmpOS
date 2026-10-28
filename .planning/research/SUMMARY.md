# Project Research Summary

**Project:** Autonogrammer
**Domain:** C#/.NET ACP harness + chat-style agent workspace for the `omp` engine (18.12.3) with Generative UI — Blazor WASM (Vercel) + .NET MAUI Blazor Hybrid sharing one RCL, ASP.NET Core SSE backend (Railway), optional Temporal, layered podman / OpenShell / agentOS / attested-enclave sandboxes
**Researched:** 2026-10-20
**Confidence:** HIGH for the core loop (stack, protocols, surfaces, streaming); MEDIUM for the sandbox/attestation layer (vendor shapes verified, Autonogrammer integration paths not)

## Executive Summary

Autonogrammer is a **protocol projection product**: everything the user sees is a fold over two wire protocols — ACP v1 (`omp acp`, JSON-RPC 2.0 over stdio) and omp's native JSONL RPC (`omp --mode rpc`) — rendered as sequence-numbered `StreamEvent`s and `UiBlock`s. Every surveyed competitor (Zed Agent Panel, Cursor Agent, Claude Code Desktop, OpenCode) ships the same table-stakes set — session list/resume, streamed Markdown, stop/cancel with partial retention, tool-call cards, permission cards, slash palette, @-mentions, plan panel, thought display, model/mode selector — and none of them renders agent-generated interactive UI. Autonogrammer's differentiator is the **A2UI v0.9.1 surface block** on top of that baseline, plus steering/follow-up and subagent affordances that only the omp RPC transport can carry. Experts build this class of product as a single-writer session actor over an **append-only, sequence-numbered event log** with SSE `id:=Seq` and `Last-Event-ID` replay, so the log — not the connection and not the UI — is the source of truth across web, desktop and mobile.

The recommended approach is fixed by PROJECT.md and confirmed by research: **.NET 10 LTS (SDK 10.0.402, runtime 10.0.13)** everywhere — .NET 8 and 9 both die on 2026-11-10 — with ASP.NET Core 10's in-box `TypedResults.ServerSentEvents`, `System.Net.ServerSentEvents.SseParser`, built-in OpenAPI 3.1 and `AddValidation()`; `System.Text.Json` **source generation only** (reflection disabled, so trimmed WASM/MAUI builds cannot silently break); a standalone Blazor WASM head published statically to Vercel; MAUI Blazor Hybrid 10.0.110 heads built only on a macOS CI runner; `Temporalio` 1.20.0 behind `Workflows:Enabled` with a `DirectSessionRunner` default; and CLI/HTTP-shaped sandbox providers (fake → podman-compose → OpenShell(podman) → agentOS → dstack-shaped attestation boundary) with **no invented vendor SDK calls**. Build order is dependency-driven: contracts → scripted harness adapter → sessions/reducer → API/SSE → RCL + WASM vertical slice (no omp, Temporal or cloud) → real ACP/RPC adapters → sandbox + workflows → MAUI heads → hardening.

The key risks are all in the harness and the trust boundary, and all have concrete preventions. (1) Treating omp's stdio as request/response instead of two always-drained multiplexed streams, staying on RPC protocol v1 (silent 1 MiB truncation), completing prompts on `agent_end` instead of `prompt_result`, and cancelling locally instead of via `abort`/`session/cancel` — prevented by an explicit `Submitted → Admitted → Running → Yielded → Settled` state machine, id-correlated pending maps and a scripted contract-test fixture every adapter must pass. (2) Auto-approving `session/request_permission` or shipping omp's `yolo` schema default — prevented by a typed deny-by-policy hook that can only deny, never grant, and a permission card that renders agent options verbatim. (3) Misreading the sandbox layers: agentOS is V8 + Wasm (CPython-as-Wasm, **no native binaries** — omp is a Bun binary with N-API addons, so omp-inside-agentOS is unverified), OpenShell's podman driver needs Podman 5.x + cgroups v2 + user socket (the dev box has Docker and apt Podman 4.9.3), and attestation is orthogonal to isolation (Phala dstack post-mortem 2026-10-19) — prevented by capability-describing `SandboxDescriptor`s, a `PrerequisiteMissing` failure on the OpenShell stub, and an enclave stub that returns `Unverified`, never `true`.

## Key Findings

### Recommended Stack

Every version below was read from the NuGet v3 registry or the vendor's release feed on 2026-10-20 (STACK.md). The stack is deliberately **in-box first**: .NET 10 ships SSE, OpenAPI 3.1, validation, options validation, rate limiting and health checks, so the dependency list is short and every third-party package is MIT/BSD/Apache (FluentAssertions 8.x is excluded for its non-commercial licence; Newtonsoft is banned for trimming). Pin in `global.json` (`"version": "10.0.402", "rollForward": "latestPatch"`, MTP test runner), Central Package Management with transitive pinning decided **once** in Foundation, lock files only on app/test heads.

**Core technologies:**
- **.NET SDK 10.0.402 / `net10.0` (runtime 10.0.13, LTS to 2028-11-14)**: single TFM for contracts, services, RCL, WASM, MAUI, tests — .NET 8/9 EOL 2026-11-10; .NET 11 is RC2 STS, evaluate after GA.
- **ASP.NET Core 10 minimal APIs + `Microsoft.AspNetCore.OpenApi` 10.0.13 + `Microsoft.Extensions.ApiDescription.Server` 10.0.13**: session/prompt/approval endpoints, build-time OpenAPI 3.1 document committed so a TS client can be generated later; `Scalar.AspNetCore` 2.18.0 dev-only UI.
- **`System.Net.ServerSentEvents` + `TypedResults.ServerSentEvents` (in-box)**: `SseItem<T>.EventId` = monotonic `Seq`; client `SseParser.Create(stream).EnumerateAsync()` over `ResponseHeadersRead` — Blazor 10 enables response streaming by default, so WASM SSE works without opt-in.
- **`System.Text.Json` source generation (in-box)**: one `JsonSerializerContext` per protocol, `[JsonPolymorphic]`/`[JsonDerivedType]` for `StreamEvent`/`UiBlock`/omp frames, `JsonSerializerIsReflectionEnabledByDefault=false`, `FallBackToNearestAncestor` so unknown A2UI components hit the `unknown` fallback instead of throwing.
- **Blazor WebAssembly standalone 10.0.13** (not Blazor Web App): static publish to Vercel with `CompressionEnabled=false` (Vercel edge already compresses), immutable `_framework/*` cache headers (fingerprinted in .NET 10), `dotnet-install.sh` in `installCommand` (Vercel build image has no .NET), no AOT in the first milestone.
- **.NET MAUI Blazor Hybrid — workload/`Microsoft.Maui.Controls`/`WebView.Maui` 10.0.110**: scaffold from `maui-blazor-web -I WebAssembly`, keep MAUI head + RCL, replace the server-hosted web head with `blazorwasm`; macOS CI runner only, workload-set pinned.
- **`Temporalio` 1.20.0 + `.Extensions.Hosting` + `.Extensions.OpenTelemetry`**: worker on task queue `autonogrammer-session-runs` behind `Workflows:Enabled`; dev server `temporalio/temporal:1.9.1 server start-dev` in a `workflows` compose profile (never the deprecated `auto-setup` image).
- **podman 5.x + podman-compose 1.6.0**: mandated orchestration; `Dockerfile` (literal name, OCI-neutral) built with `podman build`, `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0-noble-chiseled`; Railway auto-detects it (Railpack's .NET provider does not fit a monorepo).
- **UI libraries**: `Markdig` 1.4.2 with `DisableHtml()` (agent output is untrusted), `Markdown.ColorCode` 3.0.1 (pure C# highlighting, no JS interop), `DiffPlex` 1.9.0 (hunks from before/after text; unified-diff parsing is in-house), no component library.
- **Ops**: OpenTelemetry 1.20.0 family via OTLP, `Microsoft.Extensions.Diagnostics.HealthChecks` (`/healthz`, `/readyz` reflecting omp binary / Temporal / sandbox state), `[OptionsValidator]` source-generated config, `Microsoft.Extensions.Http.Resilience` 10.11.0 only when a real HTTP sandbox provider appears.
- **Tests**: `xunit.v3` 4.0.2 on Microsoft.Testing.Platform, `Shouldly` 4.3.0, `NSubstitute` 6.2.0, `Verify.XunitV3` 33.3.4, `bunit` 2.12.5, `TngTech.ArchUnitNET.xUnitV3` 0.13.4, `Testcontainers` 4.16.0 (Podman via `DOCKER_HOST`, Ryuk privileged/disabled), `coverlet.MTP` 10.1.0 (not `coverlet.collector`, which is inert under MTP).
- **Analyzers/format**: SDK NetAnalyzers, `Meziantou.Analyzer` 3.0.315, `Microsoft.VisualStudio.Threading.Analyzers` 18.12.1, `BannedApiAnalyzers` 5.6.0 (bans Newtonsoft, docker shell-outs, sync-over-async), `CSharpier` 1.3.0 + `dotnet format --verify-no-changes`, `.slnx` solution.

### Expected Features

FEATURES.md binds every feature to the protocol surface that carries it and was grounded in a **live probe of `omp acp` 18.12.3**: `loadSession: true`; `sessionCapabilities` = `list`, `fork`, `resume`, `close` — **no `delete`**; `promptCapabilities` = `embeddedContext`, `image` — **no `audio`**; one `agent` auth method (omp owns credentials); `session/new` returns `configOptions` (`mode` ×2, `model` ×**739**, `thinking` ×7) **plus** legacy `modes`; first `session/update` pushes **701** slash commands. Consequences: one generic searchable config-option selector (no bespoke model picker, which also keeps "model routing UI" out of scope), a filterable command palette (never a flat menu), "delete session" as a local-index operation, and runtime capability gating instead of assumptions.

**Must have (table stakes — all P1, all in the v1 vertical slice):**
- Conversation list + open/resume — `session/list` + `session/load` (replay) / `session/resume` (no replay); titles via `session_info_update` with first-prompt-excerpt fallback (CLI RPC disables auto-titles).
- Streaming Markdown/code with copy, scroll-lock, jump-to-latest — `agent_message_chunk` / `message_update` deltas through a sanitising Markdig pipeline.
- Stop/cancel with partial output retained + retry — full ACP cancel contract (tool calls → `cancelled`, pending permissions → `cancelled`, late updates accepted until `stopReason: cancelled`); RPC `abort` / `abort_and_restore_queue`; `prompt_result.error.retryable` drives the Retry affordance.
- Tool-call cards with live status/kind icon/collapsible raw I/O; diff **placeholder**; unknown-block fallback (never silently dropped).
- Permission card bound to `session/request_permission` option kinds, keyboard-answerable, idempotent across reconnect, behind the deny-by-policy hook — the safety UI exists from day one.
- Plan/todo panel (ACP `plan` is **replace-all**, never diff-merge) and collapsed thought blocks.
- Generic config-option selector (mode/model/thinking) rendered verbatim with search; agent-initiated `config_option_update`/`current_mode_update` update the UI without a round trip.
- Slash-command palette fed by `available_commands_update` (`input.hint` ghost text) → IntentEnvelope parser; `@` picker stub (desktop file picker, web upload, mobile photo picker come later).
- A2UI surface block, read-only, catalog-bound (basic catalog), unknown component → placeholder — the Core Value.
- Accessibility baseline + keyboard shortcut sheet (Enter/Shift+Enter, Esc stop, Cmd/Ctrl+N, Ctrl+Tab; `aria-live="polite"` throttled per message/tool boundary; ≥44 px targets; reduced motion) — identical in WASM and MAUI WebView.
- SSE resume with sequence de-dup (web); mobile reconnect on `Window.Resumed` reuses it.

**Should have (v1.x, after validation):**
- Real diff renderer (DiffPlex hunks, side-by-side/stacked) — trigger: first real `diff` content.
- Desktop `terminal/*` client capability and terminal embeds — trigger: desktop head spawns `omp acp`; web/mobile advertise `terminal: false` (two render paths for "command ran").
- Queued steering/follow-up chips — omp RPC only; the first visible "better than Zed for external agents" moment; modelled as a distinct intent kind exempt from one-active-prompt rejection.
- Usage meter + compaction dividers, transient notices + desktop attention notifications, rename/export/search, fork from message, follow-the-agent file chips, `/btw` side questions.

**Defer (v2+):**
- Subagents panel, cross-surface continuity (approve from phone), A2UI v1.0 `actionResponse`, checkpoints/restore (`checkpoint`/`rewind` need sandbox snapshots), live isolation badge, goal mode, ghost text, mobile push.

**Anti-features (do not build):** billing/metering, auth providers, curated model picker/routing, plugin marketplace, client-side tool engine/MCP host, code editor panes, mobile push, offline execution, multi-user collab, voice (omp acp has no `audio`), feedback upload, synthesised "always allow" options, yolo-by-default.

### Architecture Approach

ARCHITECTURE.md describes a numbered-layer monorepo (`src/` L1 Contracts → L2 Parser/Harness.Abstractions/Harness.Acp/Harness.Rpc/Sessions/GenUi/Sandbox → L3 Workflows/Ui RCL → `services/` L4 Api/Worker → `apps/` L5 Web/Maui; `tests/` with abstract contract fixtures, architecture tests and integration tests; `docs/`, `deploy/`) whose dependency direction is asserted mechanically by ArchUnitNET. The two harness assemblies share a process/pipe skeleton but **not** framing, correlation or event vocabularies, so the vertical slice ships with `Harness.Abstractions` + the scripted adapter only, and no `omp` dependency ever reaches WASM. `Autonogrammer.GenUi` is the only module that knows both protocol vocabularies; it reduces them to `StreamEvent` and then `UiBlock`, with A2UI envelopes (`createSurface`/`updateComponents`/`updateDataModel`/`deleteSurface`, flat adjacency list, progressive rendering) passed through to an `A2uiSurfaceStore` and a catalog-bound Blazor renderer. The RCL (`Autonogrammer.Ui`) references only Contracts + GenUi and reaches the world through DI seams (`ISessionClient` HTTP+SSE | in-process, `IApprovalPresenter`, `IPlatformInfo`, `IFileAccess`, `ITerminalHost`); heads differ only in what they register.

**Major components:**
1. **`Autonogrammer.Contracts`** — single source of truth: `Session`, `Message`, `IntentEnvelope`, `UiBlock`, `StreamEvent{Seq}`, `SandboxDescriptor` (with capability fields), `WorkflowRun`, `SessionStatus ∈ Idle|Prompting|AwaitingApproval|Cancelling|Settled|Faulted`, typed errors; one `AutonogrammerJsonContext`; zero project references.
2. **`Autonogrammer.Parser`** — ordered recognizers (slash commands, @mentions/file refs, structured directives, omp magic keywords, plain chat) ending in an unremovable fallback; pure, never throws, raw text preserved byte-for-byte.
3. **`Autonogrammer.Harness.Abstractions` / `.Acp` / `.Rpc`** — `IAgentHarness`/`IHarnessSession`, explicit state machine, typed failures (`TransportClosed`, `ProtocolViolation`, `Timeout`, `AgentError`, `TransportLost`), scripted in-memory adapter + contract-test fixture; ACP = JSON-RPC 2.0 multiplexer (responses by id, agent→client requests `session/request_permission`/`fs/*`/`terminal/*`, `session/update` notifications); RPC = `ready` → `negotiate_protocol` v2 → `rpc_chunk` reassembly → id-correlated two-phase prompt (`response` then `prompt_result`, then `session_settled`) → host tools/URIs/extension UI.
4. **`Autonogrammer.Sessions`** — single-writer actor per session (`Channel` loop), one-active-prompt rule (409 `SessionBusy`), append-only gapless event log, `ISessionRepository` (in-memory), tool-approval policy hook (`Allow`/`Deny`/`AskUser`), redaction at the harness boundary.
5. **`Autonogrammer.GenUi`** — deterministic reducer protocol events → `StreamEvent` → `UiBlock` (markdown, code, tool-call, diff placeholder, error, a2ui, unknown); provenance tags for tool-derived content.
6. **`Autonogrammer.Api`** — minimal API: sessions, prompts, `GET /sessions/{id}/events` SSE with `id:=Seq` + `Last-Event-ID`/`?since=` replay + 15 s heartbeat + `204` on terminal sessions, approvals, `DELETE …/prompts/current` cancel, health, OpenAPI, CORS, rate limiter, correlation middleware.
7. **`Autonogrammer.Sandbox`** — `ISandboxProvider { Provision(spec, idempotencyKey) → Attach → Exec → StreamLogs → Snapshot → Teardown(retry) }`, TTL + orphan reaper over `autonogrammer.session=<id>` labels, composable chain `Enclave(OpenShell(podman)(agentOS(omp)))` with each layer independently stubbable; `IAttestationBoundary` over dstack `/v1/Attest|Info|Version` shapes, no-op only.
8. **`Autonogrammer.Workflows`** — `ISessionRunWorkflow` + activities (`ProvisionSandbox` → `RunPrompt` streaming into the log with heartbeats carrying `lastSeq` → `CollectArtifacts` → `TeardownSandbox` in `finally`, `Cancel` signal, `Status` query); `DirectSessionRunner` executes the same activity classes without a server; workflow assembly references only contracts + activity interfaces.
9. **`Autonogrammer.Ui` RCL + heads** — Web: `HttpSessionClient`, no harness; MAUI desktop: `InProcessSessionClient` wrapping `SessionService` + harness, spawns omp, real `IFileAccess`/`ITerminalHost`; MAUI mobile: `HttpSessionClient`, reconnect by `Seq` on `Window.Resumed`.

**Key patterns to follow:** correlate everything by `id`, never by order; `agent_end` signals yield, it is not the transcript (compacted); cancel is a protocol message (`abort` / `session/cancel`), closing stdin is shutdown; UI state is a pure fold over the log; workflows pass ids/cursors, activities do I/O; providers are CLI/HTTP-shaped and contract-tested.

### Critical Pitfalls

Top five of sixteen (PITFALLS.md); the full pitfall→phase map is in "Implications for Roadmap".

1. **stdio as request/response + v1-only framing** — one reader task per stream from process start, stderr to a bounded ring buffer, stdout as `ChannelReader<Frame>` demuxed by `id`; shutdown = close stdin → drain to EOF → await exit → interpret exit code (0 / 1 latched persistence failure); negotiate protocol v2 immediately after `ready`, validate `rpc_chunk` (`chunkId`/`index`/`count`/`byteLength`, 64 MiB reassembly cap), surface `rpc_frame_error` as a typed transport event; keep inbound frames < 1 MiB.
2. **Prompt ack ≠ completion; cancellation is protocol-level** — state machine `Submitted → Admitted → Running → Yielded → Settled` driven by `response(id)` → `prompt_result(id)` → `session_settled`; `CompleteOnce` guard; `prompt_result.error.retryable` preserved; `CancelAsync` = RPC `abort` / ACP `session/cancel` + synchronous `cancelled` answers to pending permissions; local `CancellationToken` only bounds waiting; `cancelled`/`aborted` are normal terminal states keeping partial output.
3. **Auto-approval and approval-mode precedence** — omp's schema default is `yolo`; precedence is runtime flags > `--config` overlay > project > global and ACP has **no per-session approval field** (per-session mode = separate `omp acp` process). Typed `SessionLaunchOptions` translated into flags/overlay per process; never read `~/.omp` config implicitly in CI/sandboxes; policy hook can only deny earlier; `rpc` vs `rpc-ui` (`--no-ui` fails closed on tool dialogs) chosen per adapter and documented; grep test for `--yolo` outside test fixtures.
4. **SSE that does not actually resume, and WASM that buffers** — `Seq` is the SSE `id`, non-empty always; accept `Last-Event-ID` and `?since=`; `Cache-Control: no-cache`, `X-Accel-Buffering: no`, compression off for the route, flush per event, heartbeat comment ~15 s, `204` when terminal, typed `ResumeGapError` beyond retention; client dedup by `(sessionId, Seq)` with idempotent block rendering; one SSE stream per tab (HTTP/1.1 ~6-connection cap), prefer HTTP/2 on Railway; `HttpClient.SendAsync(…, ResponseHeadersRead)` with async reads only, `WasmEnableStreamingResponse` left at default (grep test), browser smoke on the **published** build.
5. **Sandbox-layer misconceptions and secrets** — `SandboxDescriptor` carries `Isolation: Process|Container|Vm|Enclave`, `Attestation: None|Quote`, `NativeBinaries: bool`; OpenShell stub validates `podman ≥ 5`, cgroups v2, user socket and returns `PrerequisiteMissing` on the Docker-only dev box; enclave stub returns `Unverified`; secrets as `SecretRef` resolved inside activities, Temporal payload codec, redaction middleware before the event log, canary-secret test across log/SSE/stderr/workflow history; never tokens in URLs or plain env vars handed to every sandbox.

Also critical for Foundation: reflection STJ + trimming (Pitfall 8), `@rendermode` in the RCL (9), lock-file drift under CPM (10), `dotnet format` vs analyzers (11), Docker-authored compose files (13); for Platform: non-deterministic Temporal workflows (12); for Surfaces/Core: prompt injection via tool output and A2UI payloads (15).

### Where the researchers disagreed — and the evidence-backed resolution

| Topic | Positions | Resolution |
|---|---|---|
| **omp inside agentOS** | PROJECT.md: agentOS built-in agents speak ACP, so omp "is a plausible agentOS agent"; ARCHITECTURE: `[INFERENCE]`, unverified; PITFALLS corrected the brief — agentOS ships **CPython 3.13 as Wasm** (Python *is* available) but **not arbitrary native binaries**, and omp is a Bun binary loading Rust N-API `.node` addons (`omp://natives-architecture.md`). | Treat omp-in-agentOS as **unsupported until a spike proves otherwise**. agentOS stays an interface-conforming stub with `NativeBinaries: false`; **OpenShell(podman) is the first real provider** after podman-compose (ARCHITECTURE's trade-off note agrees: agentOS also needs Node on the Railway image or the hosted gateway). Open question logged below. |
| **`session/delete`** | PROJECT.md lists `session/load\|list\|close\|delete` as implemented by omp; FEATURES' live probe shows `sessionCapabilities` = `list`, `fork`, `resume`, `close` only. | The probe is ground truth for 18.12.3: **no `delete`**. "Delete session" is a local-index operation; the harness reads every session capability (incl. `loadSession`) at runtime and never assumes. |
| **Modes vs config options** | ACP marks `modes` for removal; omp returns legacy `modes` *and* `configOptions` (`mode`, `model` ×739, `thinking` ×7). | One generic config-option selector; map legacy `modes` into the `mode` category. No bespoke model picker (739 options need search, and curation is an anti-feature). |
| **SSE client implementation** | STACK: in-box `SseParser`; ARCHITECTURE/PITFALLS: "hand-written `text/event-stream` parser" so `?since=` resume works identically in WASM and MAUI. | Use the in-box `SseParser` (async-only, works on `BrowserHttpReadStream`); the resume cursor is Autonogrammer's `lastSeq` sent as **both** `Last-Event-ID` header (HttpClient can set it; only browser `EventSource` cannot) and `?since=`. No custom parser. |
| **Compose file location** | STACK: `infra/compose.yaml`; ARCHITECTURE: `deploy/podman-compose.yml`. | `deploy/compose.yaml` (podman-compose 1.6.0 reads `compose.yaml` by default; `deploy/` already holds `Dockerfile`, `railway.toml`, `vercel.json`). |
| **Parser placement** | ARCHITECTURE build-order step 1 bundles Contracts + Parser; the fixed phase spec puts the parser in Core. | Contracts in Foundation (everything compiles against them); Parser is the first plan of Core. |
| **Temporal activity set** | PROJECT.md: provision → run prompt → collect → teardown; ARCHITECTURE adds heartbeats carrying `lastSeq`, `finally` teardown with `WaitCancellationCompleted`. | Adopt ARCHITECTURE's topology verbatim; it is the same four activities with the determinism/cancellation discipline PITFALLS 12 requires. |

## Implications for Roadmap

The five phases are fixed by the ultrathink specification. Research maps onto them as follows; ARCHITECTURE's eleven-step build order is preserved inside the phases (1 → Foundation; 1–4, 6–7 → Core; 8–9 → Platform; 5, 10 → Surfaces; 11 → Verify & Handoff).

### Phase 1: Foundation
**Rationale:** Every later pitfall that is cheap now and expensive later lives here — CPM/lock-file policy (changing it later rewrites every lock file), reflection-free JSON (trimmed publish breaks otherwise), analyzer/format agreement, MAUI template warnings, Podman-not-Docker bootstrap. Contracts have zero dependencies, so they are the first compile target.
**Delivers:** Monorepo layout (`src/ services/ apps/ tests/ docs/ deploy/`), `global.json` 10.0.402 + MTP, `Directory.Build.props` (warnings-as-errors, nullable, deterministic, `EnforceCodeStyleInBuild`, trim analyzers as errors), `Directory.Packages.props` with transitive pinning, lock files on heads only + `scripts/relock`, `.editorconfig` + CSharpier + banned APIs, `.slnx`; `Autonogrammer.Contracts` with `AutonogrammerJsonContext` and `JsonSerializerIsReflectionEnabledByDefault=false`; `Autonogrammer.Architecture.Tests` (layer direction, RCL references only Contracts+GenUi, no `@rendermode` in RCL, no `WasmEnableStreamingResponse`, no `--yolo` outside fixtures, workflow assembly has no provider/HTTP refs); bootstrap script that installs Podman ≥ 5 + podman-compose 1.6.0 and **refuses Docker**; `deploy/compose.yaml` validated in CI; CI matrix (Linux: locked restore, build, test, format verify, vulnerability audit, Release publish of WASM; macOS: workload-set-pinned MAUI build, gated).
**Addresses:** PROJECT.md "builds from a clean clone", "shared contracts single source of truth", "platform foundations" (CI, analyzers, typed config skeleton).
**Avoids:** Pitfalls 8 (trimming/STJ), 9 (render modes, workload pins), 10 (lock-file drift), 11 (format vs analyzers), 13 (Docker-authored compose).

### Phase 2: Core (parser, harness, sessions, API streaming)
**Rationale:** The harness and the event log are the product's correctness core and carry the five most expensive pitfalls; the scripted adapter must land early so Surfaces can build against it, and the real ACP/RPC adapters must pass the same contract fixture so they cannot drift.
**Delivers (in this order):** `Autonogrammer.Parser` (ordered recognizers, unremovable fallback, documented max length, never throws) → `Harness.Abstractions` + scripted in-memory adapter emitting a canned ACP-shaped sequence (chunks, `tool_call`/`update`, `plan`, permission request, `config_option_update`, `available_commands_update`, A2UI envelope, stop reasons incl. `cancelled`, `retryable` error, stale `agent_end`) + abstract contract-test fixture → `Autonogrammer.Sessions` (single-writer actor, gapless `Seq` log, in-memory repository, one-active-prompt 409, approval policy hook deny-by-policy, redaction middleware, `SessionStatus`) + `Autonogrammer.GenUi` reducer with provenance and A2UI pass-through → `Autonogrammer.Api` (SSE `id:=Seq`, `Last-Event-ID`/`?since=`, heartbeat, `204` terminal, `ResumeGapError`, approvals, cancel endpoint, health, OpenAPI build-time doc, CORS, rate limiter, correlation ids, `AddValidation()` in the endpoint assembly) with the resume/dedup integration test → `Harness.Acp` against installed `omp acp` (runtime capability gating, `fs/*`/`terminal/*` handlers, `session/cancel` + `$/cancel_request`, approval mode via `SessionLaunchOptions`) → `Harness.Rpc` against `omp --mode rpc` (v2 negotiation, `rpc_chunk` decoder, two-phase prompt, `session_settled`, pinned `set_event_filter` with `messageUpdates: "delta"`, `abort`/`abort_and_restore_queue`, host tools/URIs, extension UI, `rpc` vs `rpc-ui` documented).
**Uses:** `System.Net.ServerSentEvents`, `TypedResults.ServerSentEvents`, STJ source gen, `Microsoft.Extensions.Validation`, rate limiting, health checks, `[OptionsValidator]`.
**Implements:** Components 2–6 of the architecture; patterns 1–4.
**Avoids:** Pitfalls 1 (stdio drain/exit), 2 (frame cap/`rpc_chunk`), 3 (ack vs `prompt_result`), 4 (cancellation), 5 (approval plumbing), 6 (SSE resume, server side), 15 (provenance), 16 (redaction).

### Phase 3: Platform (sandbox + workflows)
**Rationale:** Sandbox lifetime and durable runs depend on the session service and harness existing; providers are CLI/HTTP-shaped stubs plus one real local provider, so this phase is mostly contracts, config and tests — and it is where the vendor-facing uncertainty concentrates.
**Delivers:** `Autonogrammer.Sandbox` — `ISandboxProvider` with idempotency keys, timeouts, TTL, orphan reaper hosted service; `FakeSandboxProvider`; podman-compose provider (`$XDG_RUNTIME_DIR/podman/podman.sock`, labels, rootless-safe ports, `host.containers.internal`); OpenShell stub (CLI-shaped, gateway TOML `[openshell] version = 2` + `compute_driver = "podman"`, policy YAML v1 checked in, prerequisite probe → `PrerequisiteMissing`); agentOS stub (hosted-gateway shape, `NativeBinaries: false`); Railway stub; `IAttestationBoundary` over dstack `Attest`/`Info`/`Version` shapes returning `Unverified`; capability matrix test. `Autonogrammer.Workflows` — `ISessionRunWorkflow`, `DirectSessionRunner`, Temporal workflow + four activities with heartbeats/`finally` teardown/`Cancel` signal, `Temporalio.Extensions.Hosting` worker behind `Workflows:Enabled`, `Temporalio.Testing` time-skipping + replay tests, `[*.workflow.cs]` scoped suppressions, payload codec + `SecretRef`. Typed config (`Omp`, `Workflows`, `Sandbox`, `Streaming`) with fail-fast validation, `.env.example`, OTel wiring, `/readyz` reflecting omp/Temporal/sandbox, `deploy/Dockerfile` (sdk → chiseled aspnet, podman build), `railway.toml`, `workflows` compose profile with Temporal 1.9.1 dev server.
**Addresses:** PROJECT.md sandbox abstraction, durable workflow layer, security posture (no host paths/secrets mounted by default, outbound network policy), readiness endpoints.
**Avoids:** Pitfalls 12 (determinism/versioning), 13 (rootless CI compose), 14 (layer misconceptions, attestation ≠ isolation), 5 (launch options in sandboxes), 16 (SecretRef/codec).

### Phase 4: Surfaces (web/desktop/mobile + vertical slice)
**Rationale:** The RCL is a pure projection of the event log and needs Core's scripted adapter + API; MAUI heads can only be built in the macOS job, so the web head proves the slice first and the heads reuse it unchanged.
**Delivers:** `Autonogrammer.Ui` RCL — conversation list, session view, composer (Enter/Shift+Enter, `/` palette over 700+ commands with `input.hint`, `@` picker stub), block renderer (markdown via `DisableHtml()` + ColorCode, code with copy, tool-call card with status/kind/raw I/O, diff placeholder, error with `retryable`, permission card with verbatim agent options and per-process "always" scope, plan panel replace-all, collapsed thoughts, config-option selector with search, A2UI catalog-bound renderer + `A2uiSurfaceStore`, unknown-block fallback), streaming UX (stop/cancel preserving partial output, retry, scroll-lock, jump-to-latest), accessibility baseline + shortcut sheet, `ISseClient` using `SseParser` + `ResponseHeadersRead` with `Seq` dedup and `aria-live` "reconnecting…". `apps/Autonogrammer.Web` standalone WASM with `appsettings.{env}.json` API base URL, `deploy/vercel.json` (install script, `CompressionEnabled=false`, SPA rewrite, immutable `_framework`, CSP/security headers). `apps/Autonogrammer.Maui` — desktop `InProcessSessionClient` + `ProcessOmpLauncher` + real `IFileAccess`/`ITerminalHost` (advertise `fs`/`terminal: true`), mobile `HttpSessionClient` with `Window.Stopped/Resumed` + `Connectivity` backoff, window-state persistence on desktop. **Vertical slice**: prompt in the web surface → parser → session → scripted adapter → SSE → blocks, with no omp/Temporal/Railway/cloud credential; first block renders before stream end in a real browser against the published build.
**Addresses:** Every P1 feature in FEATURES.md; PROJECT.md shared chat surface, web shell, desktop/mobile shells, vertical slice.
**Avoids:** Pitfalls 7 (buffered HttpClient), 9 (RCL rules), 6 (client resume), 4 (cancel UX), 5 (card UI), 15 (sanitiser, CSP, provenance styling).

### Phase 5: Verify & Handoff
**Rationale:** PITFALLS' "Looks Done But Isn't" checklist is a ready-made verification matrix; PROJECT.md requires docs, ADRs, open questions with owners, runbooks and an implemented/stubbed/deferred report.
**Delivers:** Chaos/failure tests — 2 MB stderr + stdin-close exit-0 no-hang; >1 MiB `rpc_chunk` round-trip + interleaved rejection; stale `agent_end` ignored, `prompt_result` once; cancel-while-permission-pending (both protocols); reject path; SSE kill-and-reconnect zero gap/zero dup, `204` terminal; canary secret never in log/SSE/stderr/history; OpenShell stub `PrerequisiteMissing` on Docker-only host; enclave `Unverified`; Temporal replay + architecture tests; `dotnet publish -c Release` WASM renders the slice; macOS MAUI job green; `--locked-mode` on clean clone; rootless `podman-compose up`. Docs — `docs/architecture.md`, ADRs (net10 LTS, STJ source-gen only, standalone WASM on Vercel, own ACP/RPC clients, A2UI v0.9.1, Temporal optional, sandbox layering, deny-by-policy), `docs/open-questions.md` (list below), `docs/runbooks/local-dev.md` + `sandbox.md`, README bootstrap/run/test, CONTRIBUTING, final implemented/stubbed/deferred report.
**Avoids:** Every pitfall's verification column; prevents "green gate, hidden warning" by running format verify after build on the pinned SDK.

### Phase Ordering Rationale

- **Dependencies:** Contracts → scripted harness → sessions/reducer → API → UI is the only order in which each step is testable without omp, Temporal or cloud; real adapters and providers plug into already-tested seams via the abstract contract fixtures.
- **Grouping by architecture:** Core owns everything that knows a protocol vocabulary; Platform owns everything that owns an external resource lifetime; Surfaces owns only projection and DI; this is the same split the architecture test enforces.
- **Pitfall avoidance:** the five irreversible decisions (CPM/lock policy, reflection-free JSON, Podman-only, deny-by-policy, sequence-numbered log) are all made in Foundation/Core before any surface or provider depends on them; recovery costs for getting them wrong later are rated MEDIUM–HIGH in PITFALLS.
- **Parallelism hint for the roadmapper:** Surfaces (Phase 4) only needs Core's scripted adapter + API, not `Harness.Acp`/`Harness.Rpc`; if Core is split into plans, the real-omp adapters can be the last Core plans and overlap with early Surfaces work.

### Research Flags

Phases likely needing deeper research during planning (`/gsd:plan-phase --research-phase <N>`):
- **Phase 2 (Core):** run a scripted `omp acp` turn against a scratch repo and record every `sessionUpdate` variant actually emitted (`plan`, `agent_thought_chunk`, `usage_update`, diff/terminal content, how A2UI payloads arrive) as fixtures for the scripted adapter; empirical Railway SSE buffering/HTTP-2 check before the resume design is frozen.
- **Phase 3 (Platform):** whether omp can be hosted by agentOS at all (native addons) and which agentOS transport (hosted gateway vs Node sidecar) the stub should mirror; OpenShell's gRPC `grpc_endpoint` contract (keep CLI-shaped until found); which dstack attestation shape (`Attest`/`VersionedAttestation`) to model the verifier after; Podman 5.x on the Ubuntu 24.04 dev box (apt has 4.9.3).
- **Phase 4 (Surfaces, Vercel sub-plan only):** short spike on Vercel `libicu`/`dotnet-install` caching, SPA rewrite vs `/_framework/*` precedence, Brotli negotiation for a standalone Blazor WASM publish.

Phases with standard patterns (skip research-phase):
- **Phase 1 (Foundation):** CPM, lock files, analyzers, CSharpier, `.slnx`, `maui-blazor-web` scaffold — all official Microsoft docs, verified versions.
- **Phase 4 (Surfaces, RCL/MAUI heads):** documented `maui-blazor-web` RCL + per-head DI pattern; Blazor 10 streaming defaults; MAUI lifecycle events.
- **Phase 5 (Verify & Handoff):** checklist is already enumerated in PITFALLS.md.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Every version read from NuGet v3 / vendor release feeds on 2026-10-20; official Microsoft docs for .NET 10 features. LOW only on Vercel build-image details, ColorCode grammar coverage, Podman 5 on the dev box. |
| Features | HIGH | ACP v1 spec pages + omp bundled docs read today; `omp acp` 18.12.3 probed live; competitor docs read today. LOW for Grok Chat (no primary doc) and mobile background streaming budgets (not exercised); no prompt turn was run in the probe. |
| Architecture | HIGH (protocols, .NET, MAUI, Temporal) / MEDIUM (sandbox vendors) | ACP, omp `rpc.md`, A2UI v0.9.1, Temporal .NET, MAUI tutorial are primary; OpenShell v0.1.4 CLI/config and dstack guest API verified, their Autonogrammer integration paths are `[INFERENCE]`. |
| Pitfalls | MEDIUM | omp facts HIGH (bundled docs); vendor facts MEDIUM (primary docs fetched); omp-in-agentOS, Railway edge buffering, Vercel specifics, WebView SSE suspension are LOW/inference and flagged. |

**Overall confidence:** HIGH for Foundation, Core and Surfaces; MEDIUM for Platform. The roadmap can be built on this research without further validation except the flagged phase-research items.

### Gaps to Address

Carry each into `docs/open-questions.md` with the owner tag shown.

- **omp as an agentOS guest** (`owner: autonogrammer-maintainers`): agentOS runs V8 isolates + Wasm (CPython-as-Wasm available), omp needs native N-API addons; no doc shows an ACP agent inside the VM. Handle: agentOS stub with `NativeBinaries: false`, spike against the hosted gateway in Phase 3 research; OpenShell(podman) is the first real provider.
- **Which `sessionUpdate` variants `omp acp` emits in practice** (`owner: autonogrammer-maintainers`): capabilities were advertised, no turn was run. Handle: scripted turn in Phase 2 research → fixtures for the scripted adapter.
- **How A2UI payloads arrive from omp** (`owner: autonogrammer-maintainers`): tool output, fenced block or MCP resource / `_meta` MIME — determines the recogniser. Handle: same Phase 2 spike; until known, recognise by envelope shape (`version` + one of the four message keys) and MIME `application/a2ui+json`.
- **Steering/follow-up on the ACP path** (`owner: autonogrammer-maintainers`): ACP has no `steer`; local queue sent on `end_turn` vs hidden until the RPC adapter exists. Handle: capability flags on `IAgentHarness`; RCL hides chips on ACP in v1.
- **Desktop terminal strategy** (`owner: autonogrammer-maintainers`): own PTY in C# vs delegating to omp's bash tool. Handle: advertise `terminal: false` on all heads in v1; revisit when the desktop head spawns omp.
- **OpenShell programmatic gateway API** (`owner: autonogrammer-maintainers`): `grpc_endpoint` exists, proto not found. Handle: CLI-shaped provider until verified.
- **Attestation verifier shape** (`owner: autonogrammer-maintainers`): dstack `Attest`/`Info`/`Version` are the nearest concrete reference; no Railway "dTEE" exists. Handle: `IAttestationBoundary` no-op returning `Unverified`; Phase 3 research picks the report format.
- **Railway SSE edge behaviour** (`owner: autonogrammer-maintainers`): buffering and HTTP/2 not measured. Handle: Phase 2 empirical check; design already assumes buffering (heartbeats, `X-Accel-Buffering: no`, replay).
- **Vercel build specifics** (`owner: autonogrammer-maintainers`): `libicu` presence, `.dotnet` caching, rewrite vs static precedence, Brotli. Handle: Phase 4 spike; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` build-only workaround ready.
- **Podman 5.x on Ubuntu 24.04** (`owner: autonogrammer-maintainers`): apt has 4.9.3 (fine for compose, not for OpenShell's driver). Handle: bootstrap installs from upstream repo; OpenShell stub reports `PrerequisiteMissing` otherwise.
- **Temporal CLI image healthcheck semantics** (`owner: autonogrammer-maintainers`): `temporal operator cluster health` exit code inside `temporalio/temporal` not exercised. Handle: fall back to `nc -z localhost 7233`.
- **ColorCode grammar coverage** (`owner: autonogrammer-maintainers`): TS/TSX, Rust, YAML, Dockerfile not audited. Handle: audit in Surfaces; Prism via JS interop is the fallback.
- **A2UI v1.0 timing** (`owner: autonogrammer-maintainers`): RC with `actionResponse`; v0.9.1 is production. Handle: versioned component catalog + second `JsonSerializerContext` when v1.0 ships.
- **Mobile background streaming budget** (`owner: autonogrammer-maintainers`): iOS gives seconds; not measured. Handle: never promise live streams in background; resume by `Seq` on `Resumed`.

## Sources

### Primary (HIGH confidence)
- .NET release metadata — https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json — SDK 10.0.402 / runtime 10.0.13, LTS EOL 2028-11-14, .NET 8/9 EOL 2026-11-10, .NET 11 RC2.
- What's new in ASP.NET Core 10 — https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0 — SSE, `AddValidation()`, Blazor fingerprinting, HttpClient streaming default.
- Minimal API responses → SSE — https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses — `TypedResults.ServerSentEvents`, `SseItem<T>`.
- `SseParser` API — https://learn.microsoft.com/en-us/dotnet/api/system.net.serversentevents.sseparser
- OpenAPI in ASP.NET Core 10 — https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi — OpenAPI 3.1, build-time generation.
- STJ source generation / polymorphism — https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation, …/polymorphism
- MAUI Blazor Web App tutorial — https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app — RCL + per-head DI, render-mode constraint.
- Blazor WASM host & deploy — https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/ — compression, rewrites, trimming.
- Rate limiting, options validation generator, NuGet CPM and lock files, `dotnet format`, `dotnet sln` — learn.microsoft.com pages listed in STACK.md and PITFALLS.md.
- ACP v1 — https://agentclientprotocol.com/protocol/v1/{overview,initialization,session-setup,session-list,prompt-turn,content,tool-calls,terminals,slash-commands,session-config-options,schema}
- omp 18.12.3 bundled docs — `omp://rpc.md`, `omp://approval-mode.md`, `omp://cli-reference.md`, `omp://sdk.md`, `omp://natives-architecture.md`, `omp://tools/task.md`, `omp://tools/checkpoint.md`, `omp://tools/rewind.md`, `omp://magic-keywords.md`
- Live probe of `omp acp` 18.12.3 (`initialize`, `session/new`) — capabilities, `configOptions`, 701 commands.
- A2UI v0.9.1 — https://a2ui.org/specification/v0.9.1-a2ui/, https://github.com/google/A2UI
- Temporal .NET — https://docs.temporal.io/develop/dotnet/{workflows/basics,workflows/versioning,activities/basics,activities/execution,activities/timeouts,workers/run-worker-process}; Temporal CLI / docker-builds / `temporal server start-dev`.
- NuGet v3 registry — all package versions in STACK.md; GitHub releases for markdig 1.4.2, opentelemetry-dotnet 1.20.0, Temporalio 1.20.0, xunit.v3 4.0.2, ArchUnitNET 0.13.4, podman-compose 1.6.0, Shouldly/FluentAssertions licences.
- Railway Dockerfiles / Railpack .NET — https://docs.railway.com/builds/dockerfiles, https://railpack.com/languages/dotnet
- WHATWG HTML server-sent events — https://html.spec.whatwg.org/multipage/server-sent-events.html
- Competitor docs — Zed Agent Panel, Cursor Agents Window, Claude Code Desktop, OpenCode TUI (URLs in FEATURES.md).
- `.planning/PROJECT.md` — settled decisions and constraints.

### Secondary (MEDIUM confidence)
- NVIDIA OpenShell v0.1.4 — architecture, sandbox runtimes (podman driver prerequisites), policy schema v1, support matrix, README.
- Rivet agentOS — README ("WebAssembly + V8, not a microVM"), architecture, Python docs (CPython 3.13 as Wasm), `@rivet-dev/agentos-sandbox` 0.4.2 on npm.
- Phala dstack — overview, guest API v1, post-mortem 2026-10-19 (attestation ≠ isolation; secrets in env/logs/URLs).
- Vercel — build image, `vercel.json`, CDN compression.
- Testcontainers for .NET — Podman via `DOCKER_HOST`, Ryuk settings ("not actively tested").
- MDN `EventSource` — HTTP/1.1 ~6-connection limit; Podman release notes (pasta, 6.0 removals); podman-compose README.
- dotnet/aspnetcore#51235 — MAUI throws on explicit `@rendermode`.

### Tertiary (LOW confidence — validate during the flagged phase research)
- omp cannot run as an agentOS guest (inferred from native-addon requirement).
- Railway edge buffering / HTTP-2 behaviour for SSE.
- Vercel `libicu`, `.dotnet` caching, rewrite precedence, Brotli negotiation for Blazor publish.
- Mobile WebView background suspension of SSE; iOS background budget.
- Grok Chat feature set (no primary documentation fetched).
- ColorCode grammar coverage for omp's output languages.

---
*Research completed: 2026-10-20*
*Ready for roadmap: yes*
*Tracking: ultrathink node n3 — SPE-17751 (https://linear.app/swcstudio/issue/SPE-17751)*
