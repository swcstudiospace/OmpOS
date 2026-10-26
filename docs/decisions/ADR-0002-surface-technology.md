# ADR-0002: Surface technology

**Status update 2026-10-21:** the UI stack decisions in this record (MAUI heads and the Blazor RCL shared by Blazor WebAssembly and MAUI) are superseded by ADR-0004 (Avalonia UI). Other decisions here stand.

- Status: Partially superseded by ADR-0004
- Date: 2026-10-20
- Deciders: ompos-maintainers
- Scope: Phase 1 (Foundation), built in Phase 4

## Context

One prompt must render as Generative UI on web, desktop, and mobile from a single C# codebase, streamed from one backend, with omp as the engine. The user chose Blazor WASM and .NET MAUI directly; this ADR records each choice with its rejected alternatives and consequences.

## Decisions

- Web = Blazor WebAssembly standalone 10.0, published statically to Vercel (`CompressionEnabled=false`; the edge compresses), API base URL from environment-driven config. Rejected: a TypeScript client (user decision; one UI codebase avoids drift), Blazor Web App server hosting (Vercel serves static files only; long-lived .NET services run on Railway). Consequence: the OpenAPI 3.1 document is kept so a TS client could be generated later without contract drift.
- Native = .NET MAUI Blazor Hybrid head (`apps/OmpOS.App`) sharing `OmpOS.UI` unchanged; desktop targets host the harness in-process and spawn `omp acp` over stdio, mobile targets consume the backend only and resume by sequence. Rejected: Avalonia/Uno/console shells (no shared Razor story with the web head). Consequence: the MAUI head is excluded from `OmpOS.Linux.slnf` and built only in the macOS CI job; never built locally on Linux.
- Backend-to-client streaming = Server-Sent Events with `id:` = sequence number, `Last-Event-ID` plus `?since=` resume, ~15 s heartbeat. Rejected: WebSocket/SignalR (proxy buffering, no sticky-session-free resume, HTTP/1.1 connection caps per tab). Consequence: clients de-duplicate by `(sessionId, Seq)` and render idempotently.
- Engine integration = custom C# ACP v1 client (`omp acp`, JSON-RPC 2.0 over stdio) and custom omp RPC JSONL client (`omp --mode rpc`, `ready` → protocol v2 `rpc_chunk` reassembly). Rejected: community ACP .NET packages (user asked for owned components; none covers omp RPC) and wrapping omp as `IChatClient` (hides the sequence-numbered event model). Consequence: OmpOS owns framing, correlation, and the explicit harness state machine.
- Durable workflows = Temporal .NET SDK behind `Workflows:Enabled` (default false) with an in-process `DirectSessionRunner`; the vertical slice runs with no Temporal server. Rejected: Temporal-required day one (blocks the credential-free slice) and no durability story at all (session runs need provision → run → collect → teardown guarantees). Consequence: workflow code stays deterministic and provider-free; activities do the I/O.
- **Amendment 2026-10-21:** the Temporal part of the durable-workflows bullet above is superseded. Temporal is removed, and session runs execute in-process through `DirectSessionRunner` in the API. There is no worker service. See [handoff decision](../handoff.md#temporal-removal-and-railway-topology-2026-10-21).
- Generative UI = owned `UiBlock` contract (markdown, code, tool-call, diff placeholder, error, unknown fallback) plus an `a2ui` block carrying A2UI v0.9.1 payloads rendered by a catalog-bound Blazor renderer. Rejected: inventing a bespoke rich-UI protocol (adopts the open standard instead) and raw-HTML rendering of agent output (untrusted; Markdig runs with HTML disabled). Consequence: unknown components render through the fallback, never blank the view.
- Sandbox layering = fake provider → local podman-compose provider → OpenShell (podman driver) → agentOS stub → attested-enclave stub with a dstack-shaped attestation contract. Rejected: Docker-based tooling (user requirement; podman only) and fabricating vendor APIs (every stub returns typed `NotAvailable`/`Unverified` and links an open question). Consequence: only the fake and local providers execute for real in this milestone.

Package versions pinned in `Directory.Packages.props` are verified against the registry at scaffold time; any version that fails to restore falls back to the latest resolvable stable and is noted here. No fallback was needed.

## Phase 4 amendment — EngineHost library (2026-10-20)

D-01 (ADR-0001) did not list `src/OmpOS.EngineHost`. The `IEngineHost` seam — in-process desktop host over `InMemoryOmpAdapter` or an `OmpAcpClient` session created through an injectable factory, and remote-only mobile host over the API with SSE resume (`Last-Event-ID` plus `?since=`, Seq de-duplication, `Reconnect()` for resume-on-foreground) — lives in a dedicated L3 composition library referencing only Contracts, Harness.Abstractions, Harness.InMemory, and Harness.Acp. Rationale: host selection, resume, and de-duplication are unit-testable on Linux as plain net10.0 code without MAUI workloads, and a library that binds concrete adapters is a composition root, so it sits above them and needs no same-layer exceptions. Window-state persistence, single-instance guard, and crash-safe session recovery are interfaces with no-op defaults; per-platform backends are a later shell task. `OmpOS.EngineHost` belongs in both `OmpOS.slnx` and `OmpOS.Linux.slnf`; the MAUI head stays out of the filter and builds only in the macOS `shells` CI job.
