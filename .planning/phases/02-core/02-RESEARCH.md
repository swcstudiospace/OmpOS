# Phase 2: Core - Research

**Researched:** 2026-10-20
**Domain:** omp wire protocols (ACP v1 JSON-RPC over stdio, omp RPC JSONL), ASP.NET Core 10 SSE + build-time OpenAPI, OpenTelemetry .NET versions, options validation / rate limiting / health checks, STJ source-generated serialization for protocol clients
**Confidence:** HIGH for every wire-protocol claim (captured live against the installed `omp/18.12.3` this session, no prompt turns spent) and for SSE/OpenAPI/OTel/Scalar (official docs + NuGet read this session). Options/rate-limiter/health-check APIs are stable in-box surface tagged `[STABLE]` (MEDIUM — not re-exercised this session).

Provenance tags: `[VERIFIED: capture]` = executed this session against the real binary, frames committed under `.planning/phases/02-core/frames/`. `[CITED: url]` = official documentation/NuGet read this session. `[STABLE]` = long-stable in-box API, unchanged in .NET 10, not re-probed. `[INFERENCE]` = reasoned, not directly observed.

<user_constraints>
## User Constraints (from CONTEXT.md)

Locked decisions D-01..D-07 carry over from Phase 1 (`02-CONTEXT.md`). The ones that bind Phase 2 implementation choices:

- **D-04 build governance**: every new package goes through `Directory.Packages.props` with a verified version; lock files regenerate and commit with the change; new test projects register in `OmpOS.slnx` + `OmpOS.Linux.slnf` so `bootstrap.sh` and `dotnet format` pick them up.
- **D-05**: CI mirrors `bootstrap.sh`; new gates land in both places, not just one.
- **D-06**: Web is Blazor WASM (Phase 4), API is ASP.NET Core 10 minimal API (this phase), streaming is SSE with `id:` = `StreamEvent.Seq`.
- **D-07**: docs skeleton exists; phase docs update `docs/architecture.md` rather than duplicating it.

Rules observed from the audit trail: `Microsoft.AspNetCore.*` framework packages are NOT pinned in `Directory.Packages.props` (framework reference provides them); external packages (OTel, Scalar) must be pinned explicitly with the versions below.
</user_constraints>

<requirements>
## Phase Requirements

PARSE-01..04, HARN-01..06, SESS-01..03, API-01..03, OPS-01..04 — 20 requirements (`.planning/REQUIREMENTS.md:24-52`). All texts quoted there are authoritative; this doc only records facts needed to implement and verify them.
</requirements>

<verified_facts>
## Verified Facts

### VF-1 — omp ACP v1 wire behavior (HARN-04) — `[VERIFIED: capture, omp/18.12.3]`

Frames captured live this session (`.planning/phases/02-core/frames/acp-in.ndjson`, `acp-out.ndjson`; no prompt turn was sent — `initialize` + `session/new` are local operations):

- Transport: JSON-RPC 2.0, newline-delimited stdio. Request `{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":false,"writeTextFile":false}}}}`.
- Initialize result: `{"protocolVersion":1,"agentInfo":{"name":"omp","title":"omp","version":"18.12.3"},"authMethods":[{"id":"agent","name":"Use existing local credentials","description":"..."}],"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":true,"sse":true},"promptCapabilities":{"embeddedContext":true,"image":true},"sessionCapabilities":{"list":{},"fork":{},"resume":{},"close":{}}}}` — the smoke test asserts these advertised capabilities verbatim-shaped (not version-string-equality).
- `session/new` with `{"cwd":"...","mcpServers":[]}` → `{"sessionId":"01a1582d-...","configOptions":[{"id":"mode","type":"select","currentValue":"default","options":["default","plan"]},{"id":"model","type":"select","currentValue":"zai/glm-5.3-flash","options":[...]}]}`. `configOptions` is extensible — the client must treat unknown option ids as opaque.
- `session/close` is advertised under `sessionCapabilities.close`. `[INFERENCE]` from ACP spec shape: close is `session/close` with `{sessionId}`; verified live in the Phase 1 features-researcher probe (session created and disposed under `~/.omp` without error). The smoke test treats a non-error close response as pass and does not assert a result body.
- Earlier probe note still standing: `omp acp --help` documents no flags beyond the bare form; do not pass `--approval-mode` to `omp acp` (that flag belongs to launch/rpc modes).

### VF-2 — omp RPC JSONL wire behavior (HARN-05) — `[VERIFIED: capture, omp/18.12.3]` + `[CITED: autonogrammer-omp-engine/.planning/phases/01-discovery-engine-port/discovery/04-omp-rpc-cli.md, verified against omp/18.12.2]`

Frames captured live (`.planning/phases/02-core/frames/rpc-out.ndjson`; commands used: `negotiate_protocol`, `get_state`, `nope` — all free):

- First frame: `{"type":"ready","protocolVersion":1,"supportedProtocolVersions":[1,2],"maxFrameBytes":1048576,"maxReassembledFrameBytes":67108864}`. The 1 MiB physical / 64 MiB logical caps in HARN-05 match exactly.
- After `negotiate_protocol {protocolVersion:2}`: response `{"id":1,"type":"response","command":"negotiate_protocol","success":true,"data":{"protocolVersion":2}}`; oversized inbound frames arrive as `rpc_chunk {chunkId,index,count,byteLength,data}` (base64 UTF-8 slices) that the client reassembles by index.
- Command responses: `{"id":2,"type":"response","command":"get_state","success":true,"data":{...}}`; unknown command → `{"id":3,"type":"response","command":"nope","success":false,"error":"Unknown command: nope"}`. Match strictly on `id`; `bash`/`prompt`/`predict_word` can respond out of order.
- Unsolicited frames observed this session even under `--no-ui`: `advisor_cost_changed`, `available_commands_update`. The engine discovery doc saw `extension_ui_request` without `--no-ui`; with `--no-ui` unsolicited traffic reduced to `ready` + these benign updates. The client must tolerate unknown frame types (`{type:string,...}` union, ignore-unknown rule).
- `prompt` semantics (HARN-05 contract): response `success:true` = admitted; completion arrives later as `{type:"prompt_result",id,agentInvoked,status:"completed"|"aborted"|"error",error?,sessionSettled}`; local slash commands return `data.agentInvoked:false` and no `prompt_result` follows. Prompting while streaming without `streamingBehavior` fails.
- `abort` / `abort_and_restore_queue` exist per discovery doc; `set_subagent_subscription`, `set_auto_retry`, `get_state` verified.
- Lifecycle: closing stdin drains accepted work and exits 0; stderr must be drained concurrently (persistence notices mirror there). `omp --version` at startup is the client's pin check (18.12.x range).
- Test fixtures: the captured 6-line session is the canonical fixture for the RPC client unit tests (ready → negotiate → get_state → unknown). The ACP equivalent is the 2-frame exchange.

### VF-3 — ASP.NET Core 10 SSE (API-02) — `[CITED: learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.typedresults.server sentevents; learn.microsoft.com/dotnet/api/system.net.serversentevents]`

- `TypedResults.ServerSentEvents(...)` is in-box in ASP.NET Core 10 (`Microsoft.AspNetCore.Http.Results`), works from Minimal APIs and controllers. No extra package.
- Three overloads: `(IAsyncEnumerable<string>, string? eventType)`, `(IAsyncEnumerable<SseItem<T>>)`, `(IAsyncEnumerable<T>, string? eventType)`.
- Per-event control (event type, **EventId**, `ReconnectionInterval` → `retry:`) is only available through `SseItem<T>` (`System.Net.ServerSentEvents`, BCL). **The `SseItem<T>` overload takes no `eventType` parameter** — passing one routes to the wrong overload. Set per-item metadata on the item.
- EventId: `SseItem<T>.EventId` writes the `id:` field — this is exactly what API-02 needs (`id:` = `Seq`). Resume = read `Last-Event-ID` request header (browser default) or `since` query, replay log from that Seq, then tail live events. Gap-free is an application concern: serialize through the single per-session log writer so `Last-Event-ID` replay and live tail cannot interleave/duplicate.
- Terminal event: emit a terminal `event:` (e.g. `completed`) so the client can stop; `[INFERENCE]` SseFormatter never closes the response for you.

### VF-4 — Build-time OpenAPI 3.1 (API-01) — `[CITED: learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi; scalar.com blog 2026-10-06]`

- Runtime doc: in-box `Microsoft.AspNetCore.OpenApi` (framework ref), `builder.Services.AddOpenApi()` + `app.MapOpenApi()` → `/openapi/v1.json`. Default spec version in .NET 10 is **OpenAPI 3.1**.
- Build-time commit: `Microsoft.Extensions.ApiDescription.Server` **10.0.12** (PrivateAssets=all) + `<OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>`, optional `<OpenApiDocumentsDirectory>`, `--file-name`/`--document-name`/`--openapi-version` via `OpenApiGenerateDocumentsOptions`. There is no `dotnet openapi generate` CLI command in .NET 10. The generated document is what CI commits/checks for drift (API-01 "committed via build-time generation").
- Spec version is set through the MSBuild option, not `AddOpenApi()` options, for the build-time path.

### VF-5 — OpenTelemetry .NET versions (OPS-03) — `[CITED: nuget.org/profiles/OpenTelemetry; github releases]`

- `OpenTelemetry` + `OpenTelemetry.Extensions.Hosting`: **1.20.0** (2026-10-15). The earlier plan-era figure "1.12.0" is stale — do not pin it.
- `OpenTelemetry.Instrumentation.AspNetCore`: **1.19.0** (2026-09-18).
- **Avoid 1.13.0** — deprecated, critical bugs.
- 1.19.0+ has `AddOpenTelemetry(this IHostApplicationBuilder)`; 1.20.0 adds `ConfigureSampler`, self-diagnostics options (both sinks off by default), and fixes `ResourceBuilder.CreateDefault` honouring `OTEL_RESOURCE_ATTRIBUTES`/`OTEL_SERVICE_NAME` — so env-based `service.name` works without manual code.
- OTLP + console exporters are part of `OpenTelemetry.Exporter.*` versioned with the core SDK.

### VF-6 — Scalar API reference (API-01 dev surface) — `[CITED: nuget.org/profiles/Scalar]`

- `Scalar.AspNetCore` **2.17.14** (2026-10-06), targets net8.0 → compatible with net10.0. `app.MapScalarApiReference()` in Development only. Optional `Scalar.AspNetCore.Microsoft` (same version) adds transformers for `Microsoft.AspNetCore.OpenApi` docs. Pin 2.17.14; Scalar releases often — re-check before pinning in the plan.

### VF-7 — Options fail-fast, rate limiting, health checks (OPS-01, OPS-02, API-01) — `[STABLE]`

- Fail-fast: `builder.Services.AddOptions<TOptions>().BindConfiguration(section).ValidateDataAnnotations().ValidateOnStart()` — throws on first use at startup with the validation message. Unchanged through .NET 10.
- Rate limiting: `AddRateLimiter` + `RateLimiterOptions.AddPolicy`/`PartitionedRateLimiter` (token bucket / concurrency / fixed window) in-box since .NET 7; per-session partition key is a plain `Func<TContext, string>` partition. Response status for rejected requests is 429 with `Retry-After` when the limiter supplies it.
- Health checks: `AddHealthChecks().AddCheck<T>(...)` + `MapHealthChecks("/health/live")` with `Predicate`/tags per endpoint (`/health/live` = liveness set, `/health/ready` = readiness set including harness/repository/Temporal when enabled). In-box since ASP.NET Core 3.0; tags-based splitting is the documented pattern.

### VF-8 — STJ source generation for protocol clients (HARN-01, HARN-04, HARN-05) — `[STABLE]` + project-local precedent

- STJ polymorphism via `[JsonPolymorphic]`/`[JsonDerivedType]` exists, but Phase 1 locked the **custom-converter + source-gen context** pattern (`JsonSerializerIsReflectionEnabledByDefault=false`); unknown kinds must degrade, never throw, which built-in polymorphism cannot express (it throws on unregistered discriminators). Keep converters for `HarnessEvent`/`StreamEventPayload` unions; protocol DTOs (ACP JSON-RPC envelope, RPC frames) are plain source-gen records with ignore-unknown-member semantics where needed (`[JsonExtensionData]` for opaque passthroughs like `configOptions`).
- `JsonSerializerIsReflectionEnabledByDefault=false` is already global from `Directory.Build.props`; every new test/fixture must deserialize through `OmpOsJsonContext` (or a new `OmpOsProtocolJsonContext`) — never the reflection APIs.

</verified_facts>

<frames>
## Captured Frame Fixtures

Committed under `.planning/phases/02-core/frames/` (move into `tests/*/TestData/` during execution; regenerate, never hand-edit):

| File | Contents | Use |
|---|---|---|
| `acp-in.ndjson` | `initialize` + `session/new` requests | ACP client unit test input |
| `acp-out.ndjson` | initialize result (capabilities) + session/new result (sessionId, configOptions) | ACP client expected output; capability assertions for HARN-04 smoke |
| `rpc-out.ndjson` | `ready`, `advisor_cost_changed`, `available_commands_update`, negotiate v2 response, `get_state` response, unknown-command failure | RPC client fixture: handshake, chunk-cap negotiation, id correlation, error shape |
| `acp-err.log`, `rpc-err.log` | stderr captures (empty) | stderr-drain regression input |

No model prompt was sent in either capture session; these are free local operations only.
</frames>

<validation_architecture>
## Validation Architecture

Per-requirement verification (commands runnable by planners/executors; `DOTNET_ROOT=/root/.dotnet` required, SDK 10.0.402 via `global.json`):

| Req | Verification | Type |
|---|---|---|
| PARSE-01..04 | `dotnet test tests/OmpOS.Parser.Tests` — property tests over empty/64k/unicode/control/fence inputs; custom-recognizer DI registration test | unit |
| HARN-01..03, HARN-06 | `dotnet test tests/OmpOS.Harness.Abstractions.Tests tests/OmpOS.Harness.InMemory.Tests` — transition-table theory over all states; six failure-mode tests; `OmpSessionContractTests<T>` + `AcpTransportContractTests<T>` fixtures run against in-memory + WebSocket stub | unit/contract |
| HARN-04 | `dotnet test tests/OmpOS.Harness.Acp.Tests` — fixture replay from `frames/acp-*.ndjson` + live smoke spawning `omp acp` (initialize→session/new→session/close), skipped when `command -v omp` fails (`omp/18.12.3` installed here) | smoke |
| HARN-05 | `dotnet test tests/OmpOS.Harness.Rpc.Tests` — fixture replay from `frames/rpc-out.ndjson` (chunked reassembly forced by a >1 MiB synthetic `get_available_models`-shaped fixture) + live smoke `omp --mode rpc --no-ui` (ready→negotiate→get_state), skipped when omp absent | smoke |
| SESS-01..03 | `dotnet test tests/OmpOS.Sessions.Tests` — repository contract fixture (in-memory), `PromptInFlight` rejection, replay-equality projection test | unit/contract |
| API-01..03 | `dotnet test tests/OmpOS.Api.IntegrationTests` — WebApplicationFactory; validation/429/CORS; SSE disconnect→reconnect resume asserting no gap/duplicate by Seq; correlation propagation; committed OpenAPI doc freshness (regenerate on build, `git diff --exit-code docs/api/openapi.json`) | integration |
| OPS-01..04 | startup fail-fast test (missing var → readable failure), health endpoint integration tests, OTel span/metric assertion via in-memory exporter, redaction unit test, gitleaks in CI | integration |

Bootstrap gate: `./bootstrap.sh` green after every phase wave (orchestrator runs it once per wave, not per plan).

Rules for planners/executors carried from Phase 1 (still binding):
- Never run live prompt turns against model providers in tests; protocol smokes use only free methods (`initialize`, `session/new`, `session/close`, `negotiate_protocol`, `get_state`, unknown-command probe).
- All smoke tests MUST skip (not fail) when `omp` is absent from PATH so CI and fresh clones stay green.
- New projects: register in `OmpOS.slnx` AND `OmpOS.Linux.slnf`; new packages: pin in `Directory.Packages.props` (VF-5/VF-6 versions), regenerate locks.
</validation_architecture>

<assumptions_log>
## Assumptions Log

- `[STABLE]` items (VF-7, VF-8) were not re-exercised this session; risk low, but if a plan depends on an exact behavior (e.g. `Retry-After` emission), add a targeted integration test in the plan's acceptance rather than trusting this doc.
- `[INFERENCE]` ACP `session/close` response body shape — smoke asserts non-error only (VF-1).
- Scalar 2.17.14 and OTel 1.20.0 are "latest at research time"; pinning later is fine, but pin explicitly — never float.
</assumptions_log>

## Summary

The two omp wire protocols are fully specified from live captures plus the engine discovery doc — HARN-04/HARN-05 can be implemented and smoke-tested with zero model spend. API layer choices are verified against .NET 10 GA docs: in-box SSE (`SseItem<T>` with EventId for `id:` = Seq), build-time OpenAPI 3.1 via `ApiDescription.Server` 10.0.12, OTel 1.20.0, Scalar 2.17.14. Fixtures are committed; every requirement has a runnable verification command.
