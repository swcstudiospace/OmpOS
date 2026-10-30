# Autonogrammer

## What This Is

Autonogrammer is a chat-style agent workspace — a Grok Chat / Cursor Agent–class client whose engine is **omp** rather than Cursor. It orchestrates omp sessions and renders their streamed output as **Generative UI** (structured, interactive blocks, not plain text). It is a **Desktop ACP Harness built in C#** that grows into one full-stack product: a Blazor WebAssembly web head hosted on Vercel (Avalonia.Browser failed gate G-WEB on Avalonia 12.1.4; ADR-0004), Avalonia UI native shells for Windows, macOS, Linux, iOS and Android, and a unified ASP.NET Core API on Railway. Temporal is removed (2026-10-21, see docs/handoff.md "Temporal removal and Railway topology"): there is no worker, and session runs execute in-process in the API through `DirectSessionRunner`. Sandboxed agent execution follows the layering Rivet agentOS → NVIDIA OpenShell on podman → attested-enclave boundary.

## Core Value

A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.

## Current Milestone: v0.16.0 Autonogrammer: accounts, Cowork and Dispatch

**Update 2026-10-28:** the active milestone is v0.16.0 (13 planned phases: 66, 66.1 and 67 to 77), developed on branch `feat/autonogrammer`. It renames the product to Autonogrammer as a hard cutover (the external omp engine names stay), adds Supabase passwordless accounts, a Cowork surface beside Code (chat on the local omp engine, artifacts, Browser Use, Computer Use, projects, memory, plugins) and the Dispatch relay in the existing API. Scope, requirements and phases are in `.planning/ROADMAP.md` and `.planning/REQUIREMENTS.md` (AG-*); nothing is deployed and no live run is claimed. The goal and features below are the earlier v0.9.2 milestone text and are kept as history.

### Earlier milestone: v0.9.2 Hardening and hosted API

**Goal:** Close every v0.9.1 audit tech-debt item, the owner-deferred items OD-1 to OD-13, the human-only items HO-2 and HO-3 that run on this host (HO-4 superseded 2026-10-21), and the hosting path (Railway API, Vercel web) once the owner decides the sign-in method. Temporal Cloud is not part of the hosting path: Temporal is removed (superseded 2026-10-21, docs/handoff.md).

**Target features:**
- Tech debt programmed into phases, not parked (supersedes owner decision 7 in docs/handoff.md)
- Owner-deferred items OD-1 to OD-13: interactive approval, ~~Temporal worker wiring~~ (superseded 2026-10-21: the worker is removed, docs/handoff.md), auth provider, sandbox vendor stubs, transports, reflection, AC enumeration
- Human-only items HO-2, HO-3 run and recorded on this host (podman-compose, podman smoke test); ~~HO-4 Temporal replay~~ superseded 2026-10-21 (Temporal removed, docs/handoff.md)
- Hosting: Railway API (no worker service), Vercel web with production API origin; gated on the owner's sign-in decision. ~~Temporal Cloud~~ superseded 2026-10-21: Railway services replace it (docs/handoff.md)
- Mac builds: iOS and MacCatalyst builds on macOS, desktop and mobile spot-checks
- Records and evidence: BR-1 gate execution, clean-clone re-record, Nyquist validation, milestone re-audit

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Tech debt: every v0.9.1 audit tech-debt item closed (REQUIREMENTS.md group TD)
- [ ] Owner-deferred items OD-1 to OD-13 programmed and closed (group OD)
- [ ] Human-only items HO-2, HO-3 run and recorded on this host (group HO); HO-4 superseded 2026-10-21 (Temporal removed, docs/handoff.md)
- [ ] Hosting path: Railway API, Vercel web, after the owner decides sign-in (group HOST). Temporal Cloud superseded 2026-10-21 (docs/handoff.md)
- [ ] Mac builds and desktop/mobile spot-checks on a macOS host (group MAC)
- [ ] Records and evidence refreshed: BR-1 gate run, clean-clone re-record, Nyquist validation, milestone re-audit (group REC)

### Out of Scope

- Live production integration with a remote omp service — the harness is verified against the local `omp acp` / `omp --mode rpc` binaries only; remote WebSocket transport ships as a bounded stub.
- Railway deployment, Vercel project creation — config-as-code and runbooks only; no cloud resources are created by this milestone. Temporal Cloud setup is superseded 2026-10-21: Temporal is removed (docs/handoff.md).
- Live provisioning against Rivet agentOS, NVIDIA OpenShell, or any confidential-compute ("dTEE") vendor — stubs behind the sandbox interface; vendor APIs are not fabricated.
- Modifying or migrating the existing Railway Cloud Swarm — treated as an external system with an integration boundary.
- Authentication providers, billing, multi-tenant admin, model selection/routing UI, plugin marketplace, telemetry vendors, app-store packaging/signing — product features beyond the scaffold.
- Docker-based tooling — podman-compose is the required container orchestration; OCI build files are built with podman.
- A TypeScript web client — user chose Blazor WASM; OpenAPI is kept so a TS client could be generated later without contract drift.

## Context

**Repository state.** `~/src/repos/autonogrammer/` was empty; it sits inside the umbrella repo `/root/src/repos` (which has its own `.planning/`), and sibling projects (`agent-swarm`, `omega-prime`, …) are nested standalone git repos, so `autonogrammer` was initialised as its own git repository (`main`).

**Toolchain on the dev box (Linux x64).** .NET SDK 8.0.131 only (no workloads); node 24.18 / bun 1.4 / pnpm 11.8; Docker 29 present but Podman/podman-compose absent (apt offers podman 4.9.3 — OpenShell's podman driver documents Podman 5.x); no Temporal CLI (scaffold-time fact; not needed after the Temporal removal on 2026-10-21, docs/handoff.md); Vercel CLI 59, Railway CLI 5.63, gh 2.45; dot.net and nuget.org reachable (the .NET 10 SDK can be installed per-user via `dotnet-install.sh`). .NET MAUI heads cannot be built on Linux (no macOS/iOS/Windows targets; Android needs the workload + JDK + Android SDK) — they are built in a CI job on a macOS runner.

**omp (the engine) — verified from the installed `omp` 18.12.3 and its bundled docs.** `omp acp` runs an Agent Client Protocol server over stdio (ACP v1 stable, JSON-RPC 2.0, newline-delimited; `initialize`, `authenticate`, `session/new|load|list|close|delete`, `session/prompt`, `session/cancel`, `session/update` notifications, `session/request_permission`, `fs/read_text_file`, `fs/write_text_file`, `terminal/*`; approval behaviour via `tools.approvalMode`, `--yolo`, `--config` overlays). `omp --mode rpc` runs omp's own JSONL protocol (not JSON-RPC): a `ready` frame advertising protocol versions 1/2 and a 1 MiB frame cap, `negotiate_protocol` v2 for `rpc_chunk` reassembly up to 64 MiB, commands such as `prompt`, `steer`, `follow_up`, `abort`, `get_state`, `set_model`, `set_thinking_level`, `get_entries`, `set_host_tools`, `bash`, plus session/agent event frames; responses are matched by `id`, never by order. omp's magic keywords (`ultrathink`, `orchestrate`, `workflowz`, `jevify`) have documented matching rules (lowercase standalone prose; code fences, inline code and HTML ignored) that the prompt parser mirrors.

**Sandbox stack — verified facts that correct the brief.** Rivet agentOS (`@rivet-dev/agentos`, Rust core, Apache-2.0, announced 2026-04) is a lightweight VM on V8 isolates + Wasm inside a Node process — not a microVM; it mounts external sandboxes (Local/Docker/E2B via `@rivet-dev/agentos-sandbox`) and its built-in agents (Pi, Claude Code, OpenCode) speak ACP, so omp (a Pi derivative) is a plausible agentOS agent. NVIDIA OpenShell (Apache-2.0, Rust; gateway + supervisor + sandbox) supports `docker`, `podman`, `vm` and `kubernetes` compute drivers — the podman driver needs Podman 5.x, cgroups v2 and an active user socket, configured with `[openshell.gateway] compute_driver = "podman"`; its policy schema v1 (`filesystem_policy`, `landlock`, `network_policies`, default-deny network) is already exercised in `~/src/repos/omega-prime/omega_prime/hosting/openshell/sandbox-policy.yaml`. No Railway product called "dTEE" exists and Railway Sandboxes carry no TEE attestation; the nearest real system is Phala dstack (Intel TDX confidential VMs, on-chain KMS, `/var/run/dstack.sock` guest agent; October 8–12 2026 guest-agent incident disclosed). Railway prior art lives in `~/src/repos/agent-substrate` (five Railway services over Tailscale) and `~/src/repos/omega-prime` (`infra_railway_*` tools).

**Ecosystem versions verified at scaffold time.** ~~Temporal .NET SDK `Temporalio` 1.20.0 (+ `Temporalio.Extensions.Hosting`, `.OpenTelemetry`); Standalone Activities GA.~~ Temporal SDK superseded 2026-10-21: Temporal is removed (docs/handoff.md). Generative UI: A2UI v0.9.1 current (`application/a2ui+json`, flat component list, `createSurface`/`updateComponents`/`updateDataModel`/`deleteSurface`), v1.0 candidate; no official Blazor renderer. AG-UI .NET SDK 1.0.0 (`AGUI.Abstractions/Formatting/Client/Server`, targets .NET 8) exists as an interoperability option. ACP has no official .NET SDK (community: `LibAcp`, `AgentClientProtocol`, `SvcSystems.AI.AgentClientProtocol`); the user asked for custom C# components, so Autonogrammer owns its ACP and omp-RPC clients. `maui-blazor-web` template (.NET 9/10) produces MAUI head + Blazor Web App + shared RCL.

**Process.** Planned by ultrathink (graph `ut-mvg5z8lp-1497092b`, Linear SPE-17749…SPE-17798, Notion task 3ffbc1a0c7ae8112aa70d2e15b81929f); executed autonomously with GSD (`skip_discuss`, standard granularity, research + plan-check + verifier + code review on). Shipping goes through ultrathink-ship (PR + Greptile review to 5/5).

## Constraints

- **Tech stack**: C# for backend, harness, parser, sandbox, workflows and all native UI in Avalonia UI (ADR-0004) for Windows, macOS, Linux, iOS and Android; the web head stays Blazor WebAssembly on the shared presentation layer until gate G-WEB passes; the .NET MAUI head retires after parity (v0.10.0 Phases 20 to 25) — user's explicit "built using C#"; the shared presentation layer bounds drift between the Avalonia heads and the Blazor web head (ADR-0004).
- **Container runtime**: podman-compose / podman only; any OCI build file must build with podman — user requirement; Docker tooling must not be emitted.
- **No invented vendor facts**: package names, versions, APIs and routes only when verified at scaffold time; everything else is an interface + stub + entry in `docs/open-questions.md` with an owner.
- **Strictness**: nullable, warnings-as-errors, analyzers, deterministic builds, central package management with committed lockfiles, formatter in CI, no TODO without owner + open-question link.
- **Secrets**: nothing committed; `.env.example` documents every variable; startup fails fast on missing required config.
- **Hosting split**: Vercel serves only the static Blazor WASM publish output; the API and sandbox control plane run on Railway, and there is no worker service since Temporal was removed on 2026-10-21 (docs/handoff.md) (Vercel cannot run long-lived .NET services).
- **Dev box limits**: on this Linux host the Avalonia desktop head builds with warnings as errors and its AT-SPI2 accessibility tree is readable under Xvfb (spike 2026-10-21); the Blazor WebAssembly browser publish builds with the wasm-tools workload (installed on this host), while Avalonia.Browser fails G-WEB on Avalonia 12.1.4; Android builds need the Android SDK (not installed here); iOS and macOS packaging need the Mac channel; honest reporting of what was and was not built locally.
- **Scope discipline**: structure, wiring, contracts, foundations and one vertical slice — not the full product.
- **Protocol fidelity**: ACP v1 (stable) and omp RPC protocol v1/v2 framing as documented in the installed omp; v2 ACP draft is out of scope.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Web surface = Blazor WebAssembly (static) on Vercel, not a TypeScript client | Superseded in part by ADR-0004 (2026-10-21). Original rationale: User chose it (HITL q1); keeps one C# codebase for the five native heads; the web head is Blazor over the shared presentation layer (ADR-0004); static publish is Vercel-hostable | — Pending |
| Native shells = Avalonia UI heads (ADR-0004); the MAUI Blazor Hybrid head retires after parity (v0.10.0 Phase 25) | Superseded in part by ADR-0004 (2026-10-21). Original rationale: User chose it (HITL q2); official Microsoft pattern (`maui-blazor-web`); built in macOS CI job because Linux cannot build MAUI heads | — Pending |
| Engine integration = custom C# ACP v1 client (`omp acp`, JSON-RPC/stdio) **and** omp RPC JSONL client (`omp --mode rpc`) | Verified from installed omp docs; user asked for custom components; both protocols give full control (ACP for portability, RPC for omp-native features) | — Pending |
| Backend → client streaming = Server-Sent Events with `id:` = sequence number | Works through Railway/Vercel edges without sticky sessions; `Last-Event-ID` maps directly to resume-from-sequence; HttpClient streaming works in WASM and MAUI | — Pending |
| Generative UI = own `UiBlock` contract (markdown, code, tool-call, diff placeholder, error, unknown) plus an `a2ui` block carrying A2UI v0.9.1 payloads rendered by a catalog-bound Blazor renderer | Covers the required minimum deterministically; adopts the open standard for rich generated surfaces without inventing one | — Pending |
| ~~Temporal optional behind `Workflows:Enabled`; `DirectSessionRunner` when off~~ | Superseded 2026-10-21 by the row below (docs/handoff.md). Original rationale: User said "if necessary"; vertical slice must run without a Temporal server | — Superseded |
| Temporal removed; session runs execute in-process in the API through `DirectSessionRunner`; no worker service; hosted on Railway (project Ultrathink, environment production) with TimescaleDB session log, Dragonfly coordination and GreptimeDB telemetry | Decided 2026-10-21 in docs/handoff.md ("Temporal removal and Railway topology"); replaces the Temporal row above, so the vertical slice needs no Temporal server | — Pending |
| Sandbox layering modelled as composable providers: agentOS (lightweight VM, ACP agent host) → OpenShell (podman driver) → attested-enclave boundary (dstack-shaped contract) | Matches the user's layering with verified facts; "dTEE" has no concrete API so it stays an attestation interface | — Pending |
| .NET 10 SDK (LTS) pinned in `global.json`, installed per-user | Current LTS; needed for `maui-blazor-web`; .NET 8 reaches EOL 2026-11 | — Pending |
| Single dev principal with reserved `OwnerId` on Session; auth boundary as middleware with no-op provider | HITL q4 default; keeps multi-user possible without building auth providers | — Pending |
| Monorepo layout `src/ services/ apps/ tests/ docs/` with numbered dependency layers asserted by an architecture test | Enforces apps → services → libraries → contracts direction mechanically | — Pending |
| `.planning/` committed, no GSD branching (`git.branching_strategy: none`) | Greenfield repo; ultrathink-ship owns the PR/branch flow | — Pending |
| Avalonia for the five native heads; web head stays Blazor WebAssembly (G-WEB failed on Avalonia 12.1.4); MAUI head retires after parity; Razor library stays for the web head | Accepted 2026-10-21 (ADR-0004): MAUI supports Android, iOS, Mac Catalyst and Windows only (no Linux or browser target); Avalonia 12.1.4 (MIT) covers all six targets; Avalonia.Browser failed G-WEB on 12.1.4, so the web head stays Blazor WebAssembly | Accepted 2026-10-21, amended: web stays Blazor WebAssembly after G-WEB failed |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-10-21 after starting milestone v0.9.2; Temporal supersession applied 2026-10-21 (docs/handoff.md)*
