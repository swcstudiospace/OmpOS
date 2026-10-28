<!-- GSD:project-start source:PROJECT.md -->

## Project

**Autonogrammer**

Autonogrammer is a chat-style agent workspace — a Grok Chat / Cursor Agent–class client whose engine is **omp** rather than Cursor. It orchestrates omp sessions and renders their streamed output as **Generative UI** (structured, interactive blocks, not plain text). It is a **Desktop ACP Harness built in C#** that grows into one full-stack product: a Blazor WebAssembly web head hosted on Vercel (Avalonia.Browser failed gate G-WEB on Avalonia 12.1.4; ADR-0004), Avalonia UI native shells for Windows, macOS, Linux, iOS and Android, and a unified ASP.NET Core backend on Railway, where agent runs execute in-process through the direct runner (`DirectSessionRunner`) and sandboxed agent execution follows the layering Rivet agentOS → NVIDIA OpenShell on podman → attested-enclave boundary.

**Core Value:** A prompt typed on any surface becomes an omp session whose streamed events render as live Generative UI — reliably, resumably, and inside an isolation boundary the operator controls.

### Constraints

- **Tech stack**: C# for backend, harness, parser, sandbox, workflows and all native UI in Avalonia UI (ADR-0004) for Windows, macOS, Linux, iOS and Android; the web head stays Blazor WebAssembly on the shared presentation layer until gate G-WEB passes; the .NET MAUI head retires after parity (v0.10.0 Phases 20 to 25) — user's explicit "built using C#"; the shared presentation layer bounds drift between the Avalonia heads and the Blazor web head (ADR-0004).
- **Container runtime**: podman-compose / podman only; any OCI build file must build with podman — user requirement; Docker tooling must not be emitted.
- **No invented vendor facts**: package names, versions, APIs and routes only when verified at scaffold time; everything else is an interface + stub + entry in `docs/open-questions.md` with an owner.
- **Strictness**: nullable, warnings-as-errors, analyzers, deterministic builds, central package management with committed lockfiles, formatter in CI, no TODO without owner + open-question link.
- **Secrets**: nothing committed; `.env.example` documents every variable; startup fails fast on missing required config.
- **Hosting split**: Vercel serves only the static Blazor WASM publish output; the API and the sandbox control plane run on Railway (Vercel cannot run long-lived .NET services).
- **Dev box limits**: on this Linux host the Avalonia desktop head builds with warnings as errors and its AT-SPI2 accessibility tree is readable under Xvfb (spike 2026-10-21); the Blazor WebAssembly browser publish builds with the wasm-tools workload (installed on this host), while Avalonia.Browser fails G-WEB on Avalonia 12.1.4; Android builds need the Android SDK (not installed here); iOS and macOS packaging need the Mac channel; honest reporting of what was and was not built locally.
- **Scope discipline**: structure, wiring, contracts, foundations and one vertical slice — not the full product.
- **Protocol fidelity**: ACP v1 (stable) and omp RPC protocol v1/v2 framing as documented in the installed omp; v2 ACP draft is out of scope.

<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->

## Technology Stack

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| .NET SDK / TFM `net10.0` | SDK **10.0.402**, runtime **10.0.13** (2026-10-13); LTS, EOL **2028-11-14** | Single TFM for every project (contracts, services, RCL, WASM, MAUI heads, tests) | Current LTS; .NET 8 **and** 9 both reach EOL on **2026-11-10**, so any 8/9 choice is dead within weeks. .NET 11 is RC2 "go-live" (STS, 2026-10-13) — ship on 10, evaluate 11 after GA. Pin in `global.json` with `"rollForward": "latestPatch"`. Source: [releases-index.json](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json). **[HIGH]** |
| ASP.NET Core minimal APIs | 10.0.13 (shared framework) | Backend on Railway: session/prompt endpoints, SSE stream, health, OpenAPI | Minimal APIs got first-class validation (`AddValidation()`), `TypedResults.ServerSentEvents`, OpenAPI 3.1 and `Json+PipeReader` deserialization in .NET 10 — everything the harness backend needs is in-box. Source: [What's new in ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0). **[HIGH]** |
| `Microsoft.AspNetCore.OpenApi` | 10.0.13 | Runtime OpenAPI 3.1 document at `/openapi/v1.json` | Built-in generator (OpenAPI 3.1 + JSON Schema 2020-12 since .NET 10, XML-doc population, transformers). Swashbuckle is redundant. Add `Microsoft.Extensions.ApiDescription.Server` 10.0.13 to emit the document at build time and commit it, so a TS client can be generated later without contract drift. Source: [Generate OpenAPI documents](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0). **[HIGH]** |
| `System.Net.ServerSentEvents` + `TypedResults.ServerSentEvents` | in-box .NET 10 (package 10.0.13 only for down-level) | Server: `IAsyncEnumerable<SseItem<StreamEvent>>` → `text/event-stream`; client: `SseParser.Create(stream).EnumerateAsync()` | `SseItem<T>` carries `EventId` (use the monotonic sequence number) and `ReconnectionInterval`; the client honours `Last-Event-ID` for resume. Blazor 10 enables `HttpClient` response streaming by default (`BrowserHttpReadStream`), which is what makes SSE consumption from WASM work without opt-in. Sources: [Minimal API responses → SSE](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0), [SseParser API](https://learn.microsoft.com/en-us/dotnet/api/system.net.serversentevents.sseparser?view=net-10.0). **[HIGH]** |
| `System.Text.Json` source generation | 10.0.13 (in-box) | All JSON-RPC 2.0 (ACP) and omp JSONL frames, `UiBlock`/A2UI payloads, OpenAPI DTOs | One `JsonSerializerContext` per protocol with `[JsonSerializable]` for every frame; `[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]` + `[JsonDerivedType]` for omp event frames; `JsonElement` for JSON-RPC `params`/`result` decoded per-method; set `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>` so any reflection fallback throws in tests instead of silently working on CoreCLR and breaking under trimming (WASM) / AOT. Sources: [STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation), [STJ polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism). **[HIGH]** |
| Blazor WebAssembly standalone | `Microsoft.AspNetCore.Components.WebAssembly` 10.0.13, `.DevServer` 10.0.13 | Web surface, static publish to Vercel | Standalone (not Blazor Web App) because Vercel hosts static files only and the backend is on Railway. .NET 10 adds client-side fingerprinting (`#[.{fingerprint}]`), `<link rel="preload" id="webassembly">`, and removes `BlazorCacheBootResources`. Source: [What's new → Blazor](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0). **[HIGH]** |
| .NET MAUI Blazor Hybrid | workload `maui` **10.0.110**; `Microsoft.Maui.Controls` 10.0.110; `Microsoft.AspNetCore.Components.WebView.Maui` 10.0.110 | Native shells (macOS/Windows/iOS/Android) hosting the shared RCL in `BlazorWebView` | Only supported way to ship the same Razor components as native apps. Scaffold with `dotnet new maui-blazor-web -I WebAssembly`, keep the MAUI head + RCL, and **replace** the generated Blazor Web App head with a standalone `blazorwasm` project (the template's web head is server-hosted). The RCL's `InteractiveRenderSettings` indirection is unnecessary when both heads are fully interactive — delete it. Sources: [maui-blazor-web tutorial](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0), [dotnet/maui releases](https://github.com/dotnet/maui/releases). **[HIGH]** for versions/template; **[MEDIUM]** for the "swap the web head" adaptation (inference from the template's documented output). |
| Temporal .NET SDK | `Temporalio` **1.20.0** (2026-09-28, MIT, targets `netstandard2.0`/`netcoreapp3.1`/`net462` so `net10.0` is fine); `Temporalio.Extensions.Hosting` 1.20.0; `Temporalio.Extensions.OpenTelemetry` 1.20.0 | Optional durable `ISessionRunWorkflow` behind `Workflows:Enabled` | `AddHostedTemporalWorker(...)`, `.AddWorkflow<T>()`, `.AddScopedActivities<T>()`, `AddTemporalClient(...)` give generic-host + DI integration out of the box. gRPC gzip compression is the default since 1.17. Do **not** take the experimental `Temporalio.Extensions.WorkflowStreams` / Cloud Run / Lambda packages. Sources: [sdk-dotnet releases](https://github.com/temporalio/sdk-dotnet/releases), [Hosting README](https://github.com/temporalio/sdk-dotnet/blob/main/src/Temporalio.Extensions.Hosting/README.md), nuspec. **[HIGH]** |
| Temporal dev server container | image **`temporalio/temporal:1.9.1`** (Temporal CLI; `server start-dev --ip 0.0.0.0 --db-filename /home/temporal/temporal.db`; ports 7233 gRPC, 8233 UI) | Local/CI Temporal for the optional workflow layer | This is what Temporal's own `docker-compose-dev.yml` uses now. **`temporalio/auto-setup` is deprecated** (docker-builds README; last tag 1.29.7, 2026-06) and `temporalio/docker-compose` is archived. Use `temporalio/server` 1.32.x + `temporalio/admin-tools` + `temporalio/ui` only when you need Postgres-backed persistence that mirrors Temporal Cloud. Sources: [samples-server/compose](https://github.com/temporalio/samples-server/tree/main/compose), [docker-builds deprecation](https://github.com/temporalio/docker-builds), [CLI README](https://github.com/temporalio/cli), [`temporal server start-dev` reference](https://docs.temporal.io/cli/server), Docker Hub tag listings. **[HIGH]** |
| podman + podman-compose | **podman-compose 1.6.0** (2026-06-03, PyPI); Podman ≥ 4.6 for compose, **Podman 5.x** for the OpenShell driver | Local orchestration of Temporal dev server, Postgres (if adopted) and the sandbox provider's containers; OCI builds via `podman build` | podman-compose 1.6.0 honours `depends_on: condition: service_healthy` (1.4.0+), `--wait/--wait-timeout`, `COMPOSE_PROFILES`, `include`, `healthcheck.start_interval`, `Containerfile` detection, and reads `compose.yaml` by default. Ubuntu 24.04 apt ships Podman 4.9.3 (enough for compose), 25.04 ships 5.4.2 and 25.10 ships 5.7.0 (needed by OpenShell's podman driver). Sources: [podman-compose releases](https://github.com/containers/podman-compose/releases), [`podman_compose.py` default filenames](https://github.com/containers/podman-compose/blob/main/podman_compose.py), [packages.ubuntu.com](https://packages.ubuntu.com/search?keywords=podman&searchon=names&exact=1&suite=all&section=all). **[HIGH]** |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Markdig` | **1.4.2** (2026-10-19, BSD-2; `IsAotCompatible` for net8+) | Markdown → HTML for the `markdown` UiBlock in the RCL | Always. Build one `MarkdownPipeline` with `.UseAdvancedExtensions().DisableHtml()` — agent output is untrusted, so raw HTML must never reach `MarkupString`. Source: [markdig releases](https://github.com/xoofx/markdig/releases). **[HIGH]** |
| `HtmlSanitizer` | 9.2.1039 (MIT) | Defence-in-depth sanitiser if raw HTML ever has to be allowed (e.g. A2UI `Text` with markup) | Only if `DisableHtml()` is relaxed. **[MEDIUM]** (version from NuGet; need not be adopted at all) |
| `Markdown.ColorCode` + `ColorCode.Html` | 3.0.1 / 2.0.15 (MIT) | Server-free syntax highlighting of fenced code blocks inside Markdig output | Use for the `code` block; pure C#, so it works identically in WASM and MAUI with no JS interop. **[MEDIUM]** (versions/licences verified; rendering quality vs. Prism/highlight.js not benchmarked) |
| `DiffPlex` | 1.9.0 (Apache-2.0) | `InlineDiffBuilder`/`SideBySideDiffBuilder` for the file-diff block | When the diff is computed from before/after text. DiffPlex does **not** parse unified-diff text; the parser for omp's diff payloads is an in-house contracts component. **[HIGH]** for the package, **[MEDIUM]** for the parsing split |
| `OpenTelemetry`, `.Extensions.Hosting`, `.Exporter.OpenTelemetryProtocol`, `.Instrumentation.AspNetCore`, `.Instrumentation.Http`, `.Instrumentation.Runtime` | all **1.20.0** (core 1.20.0 released 2026-10-15) | Traces/metrics/logs via OTLP from the backend and Temporal worker | Always in the backend; `Temporalio.Extensions.OpenTelemetry` 1.20.0 plugs workflow/activity spans into the same tracer. Avoid `OpenTelemetry.Api` < 1.15.3 (GHSA-g94r-2vxg-569j, noted in Temporal 1.13.1/1.12.1 release notes). Sources: [opentelemetry-dotnet releases](https://github.com/open-telemetry/opentelemetry-dotnet/releases), NuGet. **[HIGH]** |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | 10.0.13 | `/healthz` (liveness) and `/readyz` (readiness: omp binary present, Temporal reachable when enabled, sandbox provider ok) | Always; `AddHealthChecks().AddCheck<T>()` + `MapHealthChecks`. Add `AspNetCore.HealthChecks.Npgsql` 9.0.0 only if Postgres is adopted. **[HIGH]** |
| `Microsoft.Extensions.Options` (`[OptionsValidator]` source generator) | 10.0.13 | Strongly-typed, startup-validated config (`Omp`, `Workflows`, `Sandbox`, `Streaming`) | Always: `AddOptions<T>().BindConfiguration("...").ValidateOnStart()` with a `[OptionsValidator] partial class`; no reflection-based `ValidateDataAnnotations()` needed. Source: [Options validation source generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator). **[HIGH]** |
| `Microsoft.Extensions.Validation` | 10.0.13 | `builder.Services.AddValidation()` for minimal-API request bodies/query/headers | Always on the backend. Caveat: the generator only sees validatable types in the assembly where `AddValidation()` is called — if endpoints live in a separate assembly, call it there too. Source: [What's new → Validation support in Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0). **[HIGH]** |
| `Microsoft.AspNetCore.RateLimiting` (shared framework) | 10.0.13 | `AddRateLimiter` policies (token bucket per owner on `POST /sessions/{id}/prompt`; concurrency limiter on the SSE endpoint) | Always; no package to add. Source: [Rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0). **[HIGH]** |
| `Microsoft.Extensions.Http.Resilience` | 10.11.0 | Standard resilience handler (retry/timeout/circuit-breaker, Polly 8.8.0 underneath) for backend → sandbox-provider HTTP calls | When a real sandbox provider (Railway/agentOS/OpenShell) is wired; not for the SSE client (SSE resume is protocol-level, not retry-level). **[HIGH]** |
| `Scalar.AspNetCore` | 2.18.0 (MIT) | Interactive OpenAPI UI in Development only | Optional; ASP.NET Core ships the document, not a UI. **[HIGH]** |
| `AGUI.Abstractions` / `AGUI.Server` / `AGUI.Client` | 1.0.1 (MIT; PROJECT.md recorded 1.0.0 — 1.0.1 is now the latest stable) | AG-UI interoperability | Only if an AG-UI client must consume the stream; the native `StreamEvent` contract stays primary. **[HIGH]** on version; adoption is optional |
| A2UI payloads | spec **v0.9.1** (`application/a2ui+json`; v1.0 is a release candidate; `v1_0` folder exists in the repo) | `a2ui` UiBlock carrying `createSurface`/`updateComponents`/`updateDataModel`/`deleteSurface` | Model the v0.9.1 message set as STJ source-generated records with `JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor` so unknown component types render through the `unknown` fallback instead of throwing. Source: [google/A2UI README](https://github.com/google/A2UI) (note: "current production release is v0.9.1"). **[HIGH]** on version, **[MEDIUM]** on the handling pattern |

### Test Stack

| Library | Version | Purpose | Why this one |
|---------|---------|---------|--------------|
| `xunit.v3` | **4.0.2** (4.0.0 shipped 2026-08-14; Apache-2.0) + `xunit.runner.visualstudio` 4.0.1 + `xunit.analyzers` 2.2.0 | Unit/integration test framework | v3 projects are executables, run natively on Microsoft.Testing.Platform v2, support Native AOT and full test-case parallelism; `async void` tests fail fast; `IAsyncLifetime` is `IAsyncDisposable`. xunit 2.9.3 is maintenance-only. Sources: [v3 migration](https://xunit.net/docs/getting-started/v3/migration), [4.0.0 release notes](https://xunit.net/releases/v3/4.0.0). **[HIGH]** |
| Microsoft.Testing.Platform (MTP) mode of `dotnet test` | MTP 2.5.1 via xunit.v3; enable with `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }` | Test runner mode for the .NET 10 SDK | xunit 4.0 dropped MTP v1 and expects MTP mode on the .NET 10 SDK ("Testing with VSTest target is no longer supported… on .NET 10 SDK and later"). Use `dotnet test --solution Autonogrammer.slnx`, `--report-trx` via `Microsoft.Testing.Extensions.TrxReport` 2.5.1. Source: [Testing with dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test). **[HIGH]** |
| `Shouldly` | **4.3.0** (BSD-3-Clause) | Assertions | FluentAssertions 8.x moved to the **Xceed Community License** (non-commercial only; commercial use requires a paid licence) — see *What NOT to Use*. Shouldly is permissively licensed and feature-adequate. 5.0.0-preview.2 exists; stay on 4.3.0. Sources: [Shouldly LICENSE](https://github.com/shouldly/shouldly/blob/master/LICENSE.txt), [FluentAssertions LICENSE](https://github.com/fluentassertions/fluentassertions/blob/main/LICENSE). **[HIGH]** |
| `NSubstitute` | 6.2.0 (BSD-3-Clause) | Mocking for provider/transport seams | Preferred over Moq (4.21.0) because of Moq's 2023 SponsorLink incident and NSubstitute's cleaner API; most seams should use the deterministic in-memory adapters the requirements mandate rather than mocks. **[HIGH]** on versions/licences, **[MEDIUM]** on preference |
| `TngTech.ArchUnitNET` + `TngTech.ArchUnitNET.xUnitV3` | **0.13.4** (2026-08-19, Apache-2.0) | Architecture tests enforcing apps → services → libraries → contracts | Actively maintained with a dedicated xUnit v3 adapter. **NetArchTest.Rules 1.3.2 has had no release since 2021-05** — do not use. Sources: [ArchUnitNET releases](https://github.com/TNG/ArchUnitNET/releases), [NetArchTest releases](https://github.com/BenMorris/NetArchTest/releases). **[HIGH]** |
| `Verify.XunitV3` | 33.3.4 (MIT) | Snapshot tests for parser `IntentEnvelope`s, STJ frame encodings, OpenAPI document, rendered `UiBlock` HTML | Use `Verify.XunitV3`, not `Verify.Xunit` (31.12.5, v2 adapter). **[HIGH]** |
| `bunit` | 2.12.5 (MIT) | Razor component tests for the RCL (block renderer, composer, live region) | Always for the shared RCL; runs in plain xunit, no browser. **[HIGH]** |
| `Testcontainers` (+ `Testcontainers.PostgreSql` if Postgres) | 4.16.0 (MIT) | Integration tests against the Temporal dev server and the local podman-compose sandbox provider | Works against Podman through the Docker-compatible API: `DOCKER_HOST=unix:///run/user/$UID/podman/podman.sock` with `TESTCONTAINERS_RYUK_DISABLED=true` or `ryuk.container.privileged=true`. Testcontainers documents that non-Docker runtimes are "not actively tested", so gate these tests behind a trait. Source: [custom configuration](https://dotnet.testcontainers.org/custom_configuration/). **[HIGH]** on version, **[MEDIUM]** on the Podman recipe (documented env vars; Podman specifically is community-established, not vendor-tested) |
| `coverlet.MTP` | 10.1.0 (MIT) | Coverage under MTP (`dotnet test --coverlet`) | `coverlet.collector` is a VSTest data collector and is inert in MTP mode. `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0 works but ships under Microsoft's proprietary licence terms, not MIT. Source: [coverlet README → MTP integration](https://github.com/coverlet-coverage/coverlet). **[HIGH]** |
| `Microsoft.Playwright` | 1.63.0 | Optional browser E2E of the Vercel preview (SSE resume, a11y smoke) | Later milestone; not needed for the vertical slice. **[HIGH]** on version |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| `global.json` | Pin SDK + test runner | `{ "sdk": { "version": "10.0.402", "rollForward": "latestPatch" }, "test": { "runner": "Microsoft.Testing.Platform" } }`. Railpack also reads `sdk.version` if Railway ever builds without a Containerfile. **[HIGH]** |
| Central Package Management | One `Directory.Packages.props` with every `<PackageVersion>`; `ManagePackageVersionsCentrally=true`, `CentralPackageTransitivePinningEnabled=true` | Transitive pinning lets you lift vulnerable transitive packages (e.g. `OpenTelemetry.Api`) without a top-level reference; use `GlobalPackageReference` for analyzers that every project needs. Source: [Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management). **[HIGH]** |
| NuGet lock files | `RestorePackagesWithLockFile=true` in `Directory.Build.props`; CI restores with `RestoreLockedMode=true` (`dotnet restore --locked-mode`) | Satisfies the "locked restore" requirement; commit every `packages.lock.json`. Source: [PackageReference → locking dependencies](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies). **[HIGH]** |
| NuGet audit | `NuGetAudit=true` (default) + `NuGetAuditMode=all` + `NuGetAuditLevel=low` | Flags vulnerable transitive packages at restore; pairs with transitive pinning. **[MEDIUM]** (property names from NuGet docs; not re-read this session) |
| Build hygiene (`Directory.Build.props`) | `TreatWarningsAsErrors`, `Nullable=enable`, `ImplicitUsings=enable`, `Deterministic=true`, `ContinuousIntegrationBuild=true` on CI, `AnalysisLevel=latest-all`, `EnforceCodeStyleInBuild=true`, `AccelerateBuildsInVisualStudio` off for WASM | Meets "zero analyzer warnings, nullable, deterministic". Source Link is included in the .NET 8+ SDK — no `Microsoft.SourceLink.GitHub` package needed. **[HIGH]** on properties, **[MEDIUM]** on in-box Source Link (not re-read this session) |
| Analyzers | `Microsoft.CodeAnalysis.NetAnalyzers` (in SDK, 10.0.402 line), `Meziantou.Analyzer` 3.0.315 (MIT), `Microsoft.VisualStudio.Threading.Analyzers` 18.12.1 (VSTHRD rules for the async RPC layer), `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0 with a `BannedSymbols.txt` that bans `Newtonsoft.Json.*`, `System.Diagnostics.Process.Start("docker"…)` wrappers and sync-over-async | All as `GlobalPackageReference`. `xunit.analyzers` arrives with `xunit.v3`. SonarAnalyzer.CSharp 10.35 is optional noise. **[HIGH]** on versions, **[MEDIUM]** on the ban list design |
| Formatter | `CSharpier` 1.3.0 (MIT) as a `dotnet tool` in `dotnet-tools.json`; `dotnet format` for analyzer fixes | CSharpier is opinionated and fast; `dotnet format --verify-no-changes` stays in CI for `.editorconfig` rules CSharpier does not cover. **[HIGH]** |
| Solution format | `.slnx` (`dotnet sln migrate`) | XML solution format supported by the .NET 10 SDK; `Microsoft.Build.Traversal` 4.1.82 (`dirs.proj`) only if you need per-layer build graphs in CI. Source: [dotnet sln](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln). **[HIGH]** |
| Versioning | `MinVer` 8.0.0 (tag-driven) | Simpler than Nerdbank.GitVersioning 3.10.94 for a single-repo monorepo; optional until packages or app bundles are versioned. **[MEDIUM]** |
| Workloads | `dotnet workload install maui` (macOS CI runner only); `wasm-tools` only when enabling AOT/relinking | Keep the Vercel build workload-free: no AOT in the first milestone (install time + Amazon Linux build container). **[MEDIUM]** |
| Container base images | `mcr.microsoft.com/dotnet/sdk:10.0` (build stage) → `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled` (runtime) in a `Dockerfile` built with `podman build` | Railway auto-detects a file literally named `Dockerfile` (or `RAILWAY_DOCKERFILE_PATH`); the format is OCI-neutral, so podman builds it locally and Railway builds it remotely. Source: [Railway Dockerfiles](https://docs.railway.com/builds/dockerfiles). **[HIGH]** on Railway behaviour, **[MEDIUM]** on the exact chiseled tag (standard MCR tag family; not re-listed this session) |

## Installation

# .NET 10 SDK, per-user (dev box has only 8.0.131)

# podman-compose 1.6.0 (podman itself: apt on Ubuntu 24.04 gives 4.9.3 — fine for compose;

# OpenShell's podman driver needs 5.x, which is a separate host prerequisite)

# Local tools

# Temporal dev server (only when Workflows:Enabled); UI on :8233

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| `net10.0` LTS | `net11.0` (RC2 go-live, STS) | Only after 11 GA, and only if it brings something the harness needs (OpenAPI 3.2, `DOTNET_TEST_RUNNER` env var); STS cadence is a cost for a greenfield product. |
| Hand-written STJ source-generated JSON-RPC client | `StreamJsonRpc` 2.26.150 (MIT; `SystemTextJsonFormatter` + `NewLineDelimitedMessageHandler`) | If you decide not to own the JSON-RPC layer. It defaults to Newtonsoft and is shaped for VS extensibility; it cannot speak omp's non-JSON-RPC JSONL protocol at all, so you would still write that client. PROJECT.md records the decision to own both. |
| `Microsoft.AspNetCore.OpenApi` | `Swashbuckle.AspNetCore` 10.3.1 | Only if you need Swagger UI *and* refuse Scalar; Swashbuckle is community-maintained and no longer in templates since .NET 9, and would duplicate document generation. |
| Scalar (dev only) | Swagger UI (`Swashbuckle.AspNetCore.SwaggerUI`) | Team preference; both are UI-only consumers of the built-in document. |
| Shouldly 4.3.0 | FluentAssertions **7.2.2** (Apache-2.0, still patched 2026-03) | If you need FA's `BeEquivalentTo` graph comparison and accept staying off the 8.x line forever. Never 8.x in a commercial product without a licence. |
| ArchUnitNET 0.13.4 | Roslyn analyzer + `ProjectReference` graph test | For the apps→services→libraries→contracts rule alone, a 30-line test that parses `.csproj` references is enough; ArchUnitNET adds type-level rules (no `UiBlock` subclasses outside contracts, etc.). |
| podman-compose | .NET Aspire 13.6.1 AppHost (supports Podman via `ASPIRE_CONTAINER_RUNTIME=podman`) | If the team wants a C# orchestrator and dashboard for local dev. It is a second orchestration convention next to the mandated podman-compose; defer until there is a concrete gap. **[MEDIUM]** on the Podman env var. |
| Temporal dev server (`temporalio/temporal`) | `temporalio/server` 1.32.1 + `temporalio/admin-tools` + Postgres + `temporalio/ui` 2.56.0 | When you need persistence/visibility parity with Temporal Cloud or multi-service topology in CI. |
| Pure-C# highlighting (ColorCode) | Prism/highlight.js via JS interop | If ColorCode's grammar coverage proves insufficient for the languages omp emits; costs JS interop and a bundle. |
| No UI component library (own RCL) | `Microsoft.FluentUI.AspNetCore.Components` 5.0.1 or `MudBlazor` 9.11.0 | If the chat surface needs data grids/dialogs faster than the team can build accessible ones; both add significant WASM payload and their own design tokens. |
| Standalone Blazor WASM on Vercel | Blazor Web App (Auto/Server) on Railway | If SSR/first-paint or server interactivity becomes a requirement; then Vercel is just a CDN in front of Railway and the MAUI template's web head fits as-is. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Docker / Docker Compose / `docker` CLI shelling | Out of scope by decision; Testcontainers and Railway only need the Docker *API* / OCI file format, which Podman provides. | `podman`, `podman-compose` 1.6.0, `podman build -f Dockerfile` (file named `Dockerfile` so Railway auto-detects it). |
| `Newtonsoft.Json` | Reflection-only, incompatible with trimming/AOT (WASM/MAUI), a second serializer convention; STJ 10 covers polymorphism, `JsonElement`, `PipeReader` streaming. | STJ source generation; ban the namespace via `BannedSymbols.txt`. |
| `FluentAssertions` 8.x | Xceed Community License: non-commercial use only; commercial use needs a paid licence (verified in the repo LICENSE). | `Shouldly` 4.3.0 (BSD-3) or FA 7.2.2 (Apache-2.0). |
| `NetArchTest.Rules` | Last release 2021-05-23; no xUnit v3 adapter; Mono.Cecil-era limitations. | `TngTech.ArchUnitNET.xUnitV3` 0.13.4. |
| `xunit` 2.9.3 / `Verify.Xunit` / `TngTech.ArchUnitNET.xUnit` | v2 line; VSTest-only; xunit 4.0 has moved to MTP v2 and `dotnet test` on the .NET 10 SDK should run MTP mode. | `xunit.v3` 4.0.2 + `Verify.XunitV3` + `TngTech.ArchUnitNET.xUnitV3`. |
| `coverlet.collector` under MTP mode | VSTest data collector; does nothing when `dotnet test` runs in MTP mode. | `coverlet.MTP` 10.1.0 (`--coverlet`). |
| `Microsoft.Testing.Extensions.CodeCoverage` | Proprietary Microsoft licence terms (no SPDX expression on NuGet). | `coverlet.MTP` (MIT). |
| `Swashbuckle.AspNetCore` | Duplicate generator; removed from templates since .NET 9; OpenAPI 3.1 support lags the built-in package. | `Microsoft.AspNetCore.OpenApi` + Scalar for UI. |
| `temporalio/auto-setup` image | Deprecated by Temporal (docker-builds README); last tag 1.29.7 from 2026-06 while `temporalio/server` is at 1.32.1. | `temporalio/temporal:1.9.1 server start-dev` (dev) or `temporalio/server` + `admin-tools` (prod-like). |
| `temporalio/docker-compose` repo | Archived; superseded by `temporalio/samples-server/compose`. | samples-server compose files as reference. |
| Experimental Temporal packages (`Temporalio.Extensions.WorkflowStreams`, `.Gcp.CloudRun.Id`, Lambda worker packages) | Marked experimental in 1.17–1.20 release notes; API churn (1.19 renamed/removed Standalone Activity members). | Core `Temporalio` + `Extensions.Hosting` + `Extensions.OpenTelemetry`. |
| `Moq` | SponsorLink incident (2023) eroded trust; nothing it does that NSubstitute lacks here. | `NSubstitute` 6.2.0, and the mandated in-memory adapters. |
| `Microsoft.Extensions.AI` / `Microsoft.Agents.AI` / `ModelContextProtocol` as the harness core | omp is the engine; its RPC/ACP protocols are not `IChatClient` or MCP. Wrapping them in `IChatClient` would hide the sequence-numbered event model the product depends on. | Own `IAcpClient` / `IOmpRpcClient` contracts (PROJECT.md decision); MCP only if a tool server is exposed later. |
| `@oh-my-pi/pi-coding-agent` SDK from .NET | It is an in-process Bun/TypeScript surface; omp's own docs say to use RPC mode for cross-language integration. | `omp acp` (ACP v1 over stdio) and `omp --mode rpc` (JSONL). |
| `BlazorCacheBootResources`, `BlazorEnableCompression` MSBuild props | Removed/renamed in .NET 8–10 (`CompressionEnabled` is the current property). | `CompressionEnabled`, fingerprinting via `#[.{fingerprint}]`. |
| Blazor WASM AOT (`RunAOTCompilation`) in the first milestone | Needs the `wasm-tools` workload in the Vercel build container and multiplies build time; the chat surface is I/O-bound, not CPU-bound. | IL trimming only; revisit after measuring. |
| Vercel `builds` legacy key / `now.json` | Deprecated (`now.json` support ended 2026-03-31; `builds` cannot be combined with `functions`). | `vercel.json` with `buildCommand`/`installCommand`/`outputDirectory`. |

## Stack Patterns by Variant

- Use `DirectSessionRunner` in-process; no `Temporalio` worker is registered and the `workflows` compose profile is not started.
- Because the Temporal packages still compile in, keep them in a separate `Autonogrammer.Workflows.Temporal` project so the contracts/services layers never reference `Temporalio`.
- `AddTemporalClient(...)` + `AddHostedTemporalWorker("autonogrammer-sessions").AddWorkflow<SessionRunWorkflow>().AddScopedActivities<SessionRunActivities>()`; wire `Temporalio.Extensions.OpenTelemetry` into the shared `TracerProvider`.
- Local: `temporalio/temporal:1.9.1` via compose profile; CI: Testcontainers starts the same image under Podman.
- Shell out to `podman` with explicit `--format json` and parse with STJ source-gen types; never parse human output. Idempotency keys map to container labels (`io.autonogrammer.session=<id>`) so the orphan reaper is a `podman ps --filter label=…` query.
- Interface-conforming stubs only; `Microsoft.Extensions.Http.Resilience` is added when a real HTTP boundary appears.
- Route through `HtmlSanitizer` before `MarkupString`; everywhere else keep Markdig's `DisableHtml()`.
- Keep `SseItem<T>.ReconnectionInterval` short, emit a `: keepalive` comment item every ~15 s, and rely on `Last-Event-ID` resume — the backend already has the sequence-numbered log.

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| .NET SDK 10.0.402 | `Microsoft.AspNetCore.*` 10.0.13, `Microsoft.Extensions.*` 10.0.13 and 10.11.0 lines, `System.Text.Json` 10.0.13 | Two Microsoft.Extensions version lines coexist by design (runtime-aligned 10.0.x vs. "enrichment" 10.11.x such as Http.Resilience/ServiceDiscovery/AI). **[HIGH]** |
| xunit.v3 4.0.2 | Microsoft.Testing.Platform 2.5.1, `xunit.runner.visualstudio` 4.0.1, `xunit.analyzers` 2.2.0, .NET 10 SDK in MTP mode | MTP v1 support dropped in 4.0; needs `global.json` test runner setting on .NET 10 SDK. **[HIGH]** |
| TngTech.ArchUnitNET.xUnitV3 0.13.4 | xunit.v3 4.x | Dedicated v3 adapter package. **[HIGH]** |
| Verify.XunitV3 33.3.4 | xunit.v3 4.x | Separate from `Verify.Xunit` (v2). **[HIGH]** |
| coverlet.MTP 10.1.0 | MTP ≥ 1.7 / xunit.v3 | Run with `dotnet test --coverlet`. **[HIGH]** |
| Temporalio 1.20.0 | net10.0 (via netstandard2.0), OpenTelemetry ≥ 1.15.3 | Transitively pin `OpenTelemetry.Api` ≥ 1.15.3 (advisory GHSA-g94r-2vxg-569j). **[HIGH]** |
| Temporalio 1.20.0 | Temporal dev server from CLI 1.9.x (`temporalio/temporal:1.9.1`), server 1.30–1.32 | Dev server is an embedded SQLite server; parity with Cloud features (Nexus etc.) not required by the harness. **[MEDIUM]** |
| Microsoft.Maui.Controls 10.0.110 | `Microsoft.AspNetCore.Components.WebView.Maui` 10.0.110, ASP.NET Core 10.0.13 RCL | MAUI packages version with the workload (10.0.1xx), not with ASP.NET Core (10.0.x); both must be .NET 10. **[HIGH]** |
| Blazor WASM 10.0.13 | `System.Net.ServerSentEvents` in-box; `HttpClient` streaming default on | Any sync `Stream.Read` on the response stream throws in WASM — the SSE client must be fully async. **[HIGH]** |
| podman-compose 1.6.0 | Podman ≥ 4.6 (hang fixed in 1.4.0), Podman 5.x | OpenShell's podman driver requires Podman 5.x + cgroups v2 + user socket (PROJECT.md). Ubuntu 24.04 apt = 4.9.3 → compose OK, OpenShell driver not. **[HIGH]** |
| Testcontainers 4.16.0 | Podman socket via `DOCKER_HOST` | Ryuk needs `ryuk.container.privileged=true` or `TESTCONTAINERS_RYUK_DISABLED=true` under rootless Podman. **[MEDIUM]** |
| Railway Railpack | `global.json` `sdk.version` / `TargetFramework` | Railpack's .NET provider only triggers when a `*.csproj` sits at the service root; a monorepo needs the `Dockerfile` route (or a per-service root directory). Source: [railpack.com/languages/dotnet](https://railpack.com/languages/dotnet). **[HIGH]** |

## Open Questions (LOW confidence — resolve in phase research)

- **Vercel build runtime details.** Whether `libicu` is present in the Amazon Linux 2023 build image was not verified; the `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` build-only workaround is standard but untested on Vercel here. Also unverified: whether Vercel caches `./.dotnet` between builds (assume not; budget ~30–60 s per build for `dotnet-install.sh`). **[LOW]**
- **Vercel rewrite vs. static-file precedence** for `/_framework/*` when a catch-all `rewrites` entry exists — documented behaviour is "filesystem first" for static deployments, but not exercised against a Blazor publish in this session. **[MEDIUM→verify]**
- **Podman 5.x on the Ubuntu 24.04 dev box** — apt offers 4.9.3; 5.4.2/5.7.0 ship in Ubuntu 25.04/25.10. Whether to upgrade the OS, build Podman from source, or run OpenShell's driver elsewhere is an environment decision, not a stack one. **[LOW]**
- **Temporal CLI image healthcheck** — `temporal operator cluster health` inside `temporalio/temporal` is the natural probe; exit-code behaviour not exercised. **[MEDIUM]**
- **ColorCode grammar coverage** for the languages omp emits (TS/TSX, Rust, YAML, Dockerfile) was not audited. **[LOW]**
- **A2UI v1.0** — the README calls v1.0 a release candidate and the repo already has a `v1_0` spec folder; model v0.9.1 now, keep the renderer's component catalog versioned so v1.0 can be added as a second `JsonSerializerContext`. **[MEDIUM]**

## Sources

- [builds.dotnet.microsoft.com releases-index.json](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json) — .NET 10.0.13 / SDK 10.0.402, LTS EOL 2028-11-14; .NET 8/9 EOL 2026-11-10; .NET 11 RC2 go-live. HIGH
- [NuGet v3 flat-container index](https://api.nuget.org/v3-flatcontainer/) — every package version and (via nuspec) licence expression listed above, read 2026-10-20. HIGH
- [xunit.net — Migrating from v2 to v3](https://xunit.net/docs/getting-started/v3/migration) and [Core Framework v3 4.0.0 release notes](https://xunit.net/releases/v3/4.0.0) — MTP v2 default, AOT, parallelism, breaking changes. HIGH
- [Testing with 'dotnet test' (MTP mode)](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test) — `global.json` `test.runner`, `--solution`, `--project`, `--test-modules`. HIGH
- [FluentAssertions LICENSE (main)](https://github.com/fluentassertions/fluentassertions/blob/main/LICENSE) and [8.0.0 release](https://github.com/fluentassertions/fluentassertions/releases/tag/8.0.0) — Xceed Community License, non-commercial only; [7.2.2 (2026-03-16)](https://github.com/fluentassertions/fluentassertions/releases) still Apache-2.0. HIGH
- [Shouldly LICENSE](https://github.com/shouldly/shouldly/blob/master/LICENSE.txt) — BSD-3-Clause. HIGH
- [ArchUnitNET releases](https://github.com/TNG/ArchUnitNET/releases) (0.13.4, 2026-08-19) vs [NetArchTest releases](https://github.com/BenMorris/NetArchTest/releases) (1.3.2, 2021-05-23). HIGH
- [coverlet README](https://github.com/coverlet-coverage/coverlet) — `coverlet.MTP` for Microsoft.Testing.Platform. HIGH
- [Generate OpenAPI documents (ASP.NET Core 10)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0) — OpenAPI 3.1 since .NET 10, UI not included, `Microsoft.Extensions.ApiDescription.Server` build-time generation. HIGH
- [What's new in ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0) — SSE (`TypedResults.ServerSentEvents`), `AddValidation()`, `Microsoft.Extensions.Validation`, Blazor fingerprinting/preload, HttpClient streaming default, `BlazorCacheBootResources` removal. HIGH
- [Create responses in Minimal APIs → Server-Sent Events](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-10.0) and [SseParser API](https://learn.microsoft.com/en-us/dotnet/api/system.net.serversentevents.sseparser?view=net-10.0). HIGH
- [Rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0); [Options validation source generator](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-validation-generator). HIGH
- [STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) and [polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism). HIGH
- [Host and deploy Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0) — compression (`CompressionEnabled`, `loadBootResource` + `decode.min.js`), SPA rewrite requirement. HIGH
- [Build a .NET MAUI Blazor Hybrid app with a Blazor Web App](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app?view=aspnetcore-10.0) — `dotnet new maui-blazor-web -I {INTERACTIVITY}`, `InteractiveRenderSettings`. HIGH
- [dotnet/maui releases](https://github.com/dotnet/maui/releases) — 10.0.110 current stable, 11.0.100-rc.2. HIGH
- [Vercel build image](https://vercel.com/docs/builds/build-image) (Amazon Linux 2023, `installCommand` for extra packages), [vercel.json reference](https://vercel.com/docs/project-configuration/vercel-json), [Vercel CDN compression](https://vercel.com/docs/how-vercel-cdn-works/compression) (Brotli/gzip for `wasm`, `javascript`, `json`). HIGH
- [Temporal sdk-dotnet releases](https://github.com/temporalio/sdk-dotnet/releases) (1.20.0, 2026-09-28), [Temporalio.Extensions.Hosting README](https://github.com/temporalio/sdk-dotnet/blob/main/src/Temporalio.Extensions.Hosting/README.md), Temporalio 1.20.0 nuspec (MIT, netstandard2.0). HIGH
- [temporalio/samples-server compose](https://github.com/temporalio/samples-server/tree/main/compose) (`docker-compose-dev.yml` uses `temporalio/temporal` + `server start-dev`), [temporalio/docker-builds](https://github.com/temporalio/docker-builds) (auto-setup deprecated), [temporalio/docker-compose](https://github.com/temporalio/docker-compose) (archived), [Temporal CLI README](https://github.com/temporalio/cli), [`temporal server start-dev` reference](https://docs.temporal.io/cli/server), Docker Hub tags for `temporalio/temporal` (1.9.1), `temporalio/server` (1.32.1), `temporalio/ui` (2.56.0), `temporalio/auto-setup` (1.29.7). HIGH
- [podman-compose releases](https://github.com/containers/podman-compose/releases) (v1.6.0, 2026-06-03), [podman_compose.py default file names](https://github.com/containers/podman-compose/blob/main/podman_compose.py), [packages.ubuntu.com podman](https://packages.ubuntu.com/search?keywords=podman&searchon=names&exact=1&suite=all&section=all). HIGH
- [Testcontainers for .NET — custom configuration](https://dotnet.testcontainers.org/custom_configuration/) and [system requirements](https://dotnet.testcontainers.org/) — `DOCKER_HOST`, Ryuk settings, "Docker alternatives … not actively tested". HIGH / MEDIUM for Podman
- [NuGet Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) and [PackageReference lock files](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies). HIGH
- [Railway Dockerfiles](https://docs.railway.com/builds/dockerfiles), [Railway Railpack](https://docs.railway.com/builds/railpack), [Railpack .NET provider](https://railpack.com/languages/dotnet). HIGH
- [google/A2UI README](https://github.com/google/A2UI) — v0.9.1 production, v1.0 RC. HIGH
- [markdig releases](https://github.com/xoofx/markdig/releases) — 1.4.2 (2026-10-19). HIGH
- [opentelemetry-dotnet releases](https://github.com/open-telemetry/opentelemetry-dotnet/releases) — core 1.20.0 (2026-10-15). HIGH
- `omp://sdk.md` (bundled omp 18.12.3 docs) — SDK is Bun/TypeScript in-process; cross-language integration is RPC mode. HIGH

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-fast` for a trivial task inline, with no subagents and no PLAN.md
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
