# Phase 1: Foundation - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning
**Mode:** Auto-generated (discuss skipped via workflow.skip_discuss); decisions below are LOCKED — they come from the user-approved ultrathink specification (graph ut-mvg5z8lp-1497092b, nodes n2 SPE-17752 / n3 SPE-17751 / n6 SPE-17754) and the HITL answers (q1 Blazor WASM, q2 .NET MAUI), refined by the committed research in `.planning/research/`.

<domain>
## Phase Boundary

A developer can clone the repo, run one command, and get a green, warning-free, locked, formatted build of a monorepo whose shared contracts and layer direction are enforced mechanically. Deliverables: repo layout + solution, `OmpOS.Contracts`, build governance (analyzers, formatter, CPM + lock files, deterministic builds), CI, `deploy/compose.yaml` for podman-compose, docs skeleton with ADR-0001 and ADR-0002, `bootstrap.sh`. No parser/harness/API code in this phase (that is Phase 2); only the projects that make the layout and the guardrails real may contain code, plus empty-but-compiling library stubs where a project must exist for the architecture test to assert the layer graph.

</domain>

<decisions>
## Implementation Decisions

### D-01 Repository layout (ADR-0001) — LOCKED
- Root (build governance only): `OmpOS.slnx`, `Directory.Build.props`, `Directory.Build.targets` (if needed), `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`, `.gitattributes`, `README.md`, `CONTRIBUTING.md`, `bootstrap.sh`, `.env.example`, `.github/workflows/ci.yml`, `OmpOS.Linux.slnf` (solution filter excluding the MAUI head).
- `src/` libraries: `OmpOS.Contracts` (L0), `OmpOS.Parser`, `OmpOS.Harness.Abstractions`, `OmpOS.Sandbox.Abstractions` (L1), `OmpOS.Harness.Acp`, `OmpOS.Harness.Rpc`, `OmpOS.Harness.InMemory`, `OmpOS.Sessions`, `OmpOS.GenUi`, `OmpOS.Sandbox`, `OmpOS.Workflows` (L2), `OmpOS.UI` Razor Class Library (L2, references Contracts + GenUi only).
- `services/`: `OmpOS.Api`, `OmpOS.Workflows.Worker` (L3).
- `apps/`: `OmpOS.Web` (Blazor WebAssembly standalone), `OmpOS.App` (.NET MAUI Blazor Hybrid, Phase 4) (L4).
- `tests/`: one `<Project>.Tests` per src/services project that has code, plus `OmpOS.Harness.ContractTests`, `OmpOS.Sandbox.ContractTests`, `OmpOS.Api.IntegrationTests`, `OmpOS.ArchitectureTests`. In Phase 1 create only `OmpOS.Contracts.Tests` and `OmpOS.ArchitectureTests`; later phases add the rest.
- `docs/`: `architecture.md`, `decisions/ADR-0001-monorepo-layout.md`, `decisions/ADR-0002-surface-technology.md`, `open-questions.md`, `runbooks/local-dev.md`, `runbooks/sandbox.md`.
- `deploy/`: `compose.yaml` (podman-compose), `Dockerfile` placeholders are NOT created until a service exists (Phase 2/3); `vercel.json` arrives in Phase 4.
- Phase 1 may create empty-but-compiling placeholder projects for every src/services project listed above so the layer graph and solution are complete from day one; each placeholder contains only an `AssemblyInfo`/marker type with an XML doc comment, no TODO code.

### D-02 Dependency layers (ADR-0001) — LOCKED, asserted by `OmpOS.ArchitectureTests` (TngTech.ArchUnitNET.xUnitV3)
- L0 `OmpOS.Contracts` references nothing.
- L1 `Parser`, `Harness.Abstractions`, `Sandbox.Abstractions` → Contracts only.
- L2 `Harness.Acp`, `Harness.Rpc`, `Harness.InMemory`, `Sessions`, `GenUi`, `Sandbox`, `Workflows`, `UI` → L0/L1 (and `Sessions` may reference `GenUi`; `UI` references `Contracts` + `GenUi`).
- L3 `Api`, `Workflows.Worker` → L0–L2.
- L4 `Web` → Contracts + UI (+ GenUi); `App` (MAUI) → Contracts + UI + Harness.* (desktop in-process harness).
- Nothing references `OmpOS.Api` except tests. No `omp`/process-spawning assembly (`Harness.Acp`, `Harness.Rpc`) may be referenced by `OmpOS.Web`.

### D-03 Contracts ownership (ADR-0001) — LOCKED
`OmpOS.Contracts` owns: `Session`, `Message`, `SessionStatus { Idle, Running, AwaitingToolApproval, Completed, Failed, Cancelled }`, `IntentEnvelope`, `IntentKind`, `Diagnostic`, `UiBlock` variants (`MarkdownBlock`, `CodeBlock`, `ToolCallBlock`, `PermissionBlock`, `PlanBlock`, `DiffPlaceholderBlock`, `ErrorBlock`, `A2uiSurfaceBlock`, `UnknownBlock`), `StreamEvent` (with `long Seq`, `SessionId`, `CorrelationId`, timestamp, polymorphic payload), `SandboxDescriptor` (incl. `Isolation { Process, Container, Vm, Enclave }`, `Attestation { None, Quote }`, `NativeBinaries`), `SandboxStatus { Requested, Provisioning, Ready, Attached, Degraded, TearingDown, Destroyed, Failed }`, `WorkflowRun`, `WorkflowRunStatus`, `OmpOSError`/`ErrorCode`, `ContractVersion` (semver string constant + `X-OmpOS-Contract-Version` header name). One `OmpOsJsonContext : JsonSerializerContext` with `[JsonPolymorphic]`/`[JsonDerivedType]` discriminators (`kind`) and `FallBackToNearestAncestor` for unknown block/event kinds; `JsonSerializerIsReflectionEnabledByDefault=false` set in `Directory.Build.props`. Contracts target `net10.0` (single TFM everywhere).

### D-04 Build governance (n6) — LOCKED
- `global.json`: `{"sdk": {"version": "10.0.402", "rollForward": "latestPatch"}, "test": {"runner": "Microsoft.Testing.Platform"}}`.
- `Directory.Build.props`: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest-all` (or `latest` + `AnalysisMode=All`), `EnforceCodeStyleInBuild=true`, `Deterministic=true`, `ContinuousIntegrationBuild` when `CI=true`, `ManagePackageVersionsCentrally=true`, `RestorePackagesWithLockFile=true`, `RestoreLockedMode` when `CI=true`, `JsonSerializerIsReflectionEnabledByDefault=false`, `GenerateDocumentationFile=true`, `LangVersion=latest`, `InvariantGlobalization` left default. Analyzer packages via CPM `GlobalPackageReference`: `Meziantou.Analyzer`, `Microsoft.VisualStudio.Threading.Analyzers`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` with a `BannedSymbols.txt` banning `Newtonsoft.Json`, `System.Diagnostics.Process.Start(string)` outside `OmpOS.Harness.*`/`OmpOS.Sandbox` (use a conditional include), and `Task.Result`/`.Wait()`.
- `Directory.Packages.props`: central versions verified in `.planning/research/STACK.md` (xunit.v3 4.0.2, Shouldly 4.3.0, NSubstitute 6.2.0, TngTech.ArchUnitNET.xUnitV3 0.13.4, coverlet.MTP 10.1.0, Markdig 1.4.2, DiffPlex 1.9.0, Microsoft.AspNetCore.OpenApi 10.0.13, Temporalio 1.20.0, OpenTelemetry 1.20.0, bunit 2.12.5, Verify.XunitV3 33.3.4). Only versions that `dotnet restore` actually resolves may be committed — the executor verifies by restoring; if a version does not resolve, pick the latest resolvable stable and note it in the ADR.
- `.editorconfig` is the single rule source for formatting and code-style severities; `dotnet format --verify-no-changes` must pass; CSharpier is NOT added (one formatter only — `dotnet format`).
- Lock files: committed `packages.lock.json` for every project; CI uses `dotnet restore --locked-mode`.
- Test projects use xunit.v3 on Microsoft.Testing.Platform (`<OutputType>Exe</OutputType>`, `UseMicrosoftTestingPlatformRunner`), Shouldly assertions, `coverlet.MTP` for coverage.

### D-05 Bootstrap and CI (n6) — LOCKED
- `bootstrap.sh` (bash, `set -euo pipefail`): ensures the SDK pinned in `global.json` is available (uses `$DOTNET_ROOT`/`~/.dotnet` via `dotnet-install.sh --jsonfile global.json --install-dir ~/.dotnet` when missing), exports `DOTNET_ROOT`/`PATH`, then `dotnet restore OmpOS.Linux.slnf --locked-mode`, `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror`, `dotnet test OmpOS.Linux.slnf -c Release --no-build`, `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`; exits non-zero on any failure; prints a summary. Never installs Docker; checks `podman-compose` presence and prints a hint (non-fatal) when absent.
- `.github/workflows/ci.yml`: job `build-test` on `ubuntu-latest` (`actions/setup-dotnet` with `global-json-file`, `dotnet restore --locked-mode`, build `-warnaserror`, test with TRX/coverage artifacts, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive` failing on any vulnerability, secret scan with `gitleaks/gitleaks-action`, `podman-compose config -f deploy/compose.yaml` using `pip install podman-compose`); job `shells` on `macos-15` that installs the `maui` workload and builds `apps/OmpOS.App` only when the project exists (guarded step), `continue-on-error: false`. Concurrency group per ref, `permissions: contents: read`.
- `deploy/compose.yaml`: services `temporal` (`image: temporalio/temporal:1.9.1`, command `server start-dev --ip 0.0.0.0 --db-filename /data/temporal.db`, ports 7233/8233, volume, healthcheck via `nc -z localhost 7233` fallback) under profile `workflows`; a commented note that `podman-compose --profile workflows up` is the only supported invocation; no `version:` key; no Docker-specific extensions.

### D-06 Surface technology (ADR-0002) — LOCKED (HITL q1/q2)
- Web = Blazor WebAssembly standalone 10.0 hosted as static files on Vercel (static publish, `CompressionEnabled=false`, env-driven API base URL). TypeScript client rejected by the user; OpenAPI 3.1 document kept for future generation.
- Native = .NET MAUI Blazor Hybrid (`Microsoft.Maui.Controls` 10.0.x) one head project `apps/OmpOS.App` targeting `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0`, sharing `OmpOS.UI`; desktop targets host the harness in-process, mobile targets are remote-only; excluded from `OmpOS.Linux.slnf`; built in the macOS CI job. Avalonia/Uno/console rejected (recorded with rationale).
- Backend→client streaming = SSE (`TypedResults.ServerSentEvents`, `id:` = Seq, `Last-Event-ID` + `?since=` resume); WebSocket/SignalR rejected with rationale (proxies, HTTP/2 multiplexing, resume semantics).
- Engine integration = custom C# ACP v1 client (`omp acp`, JSON-RPC 2.0 over stdio) and omp RPC JSONL client (`omp --mode rpc`); community ACP .NET packages (LibAcp, AgentClientProtocol, SvcSystems.AI.AgentClientProtocol) rejected because the user asked for owned components and none covers omp RPC.
- Durable workflows = Temporalio 1.20.0 behind `Workflows:Enabled` (default false) with `DirectSessionRunner`; Generative UI = own `UiBlock` contract + A2UI v0.9.1 surface block; sandbox layering = fake → local podman-compose → OpenShell(podman) → agentOS stub → attested-enclave stub (dstack-shaped). ADR-0002 records each with alternatives and consequences.

### D-07 Docs skeleton — LOCKED
- `README.md`: what OmpOS is (two paragraphs from PROJECT.md), prerequisites, `./bootstrap.sh`, run/test commands, repo map table, "What is verified where" table (Linux: libraries/API/web/tests; macOS CI: MAUI head), links to docs.
- `CONTRIBUTING.md`: branch/commit conventions (`Refs SPE-xxxxx`), formatting, lock-file update procedure (`dotnet restore --force-evaluate`), adding a project (update `.slnx`, `.slnf`, architecture test layer map), TODO policy (owner tag + `docs/open-questions.md` link, enforced by a test in Phase 5).
- `docs/architecture.md`: layer diagram (mermaid), component list from research ARCHITECTURE.md §components, data flow summary.
- `docs/open-questions.md`: table `ID | Question | Why it matters | Default taken | Owner | Status`, seeded from `.planning/research/SUMMARY.md` open questions (14) and the spec ambiguities Q1–Q8, owner `ompos-maintainers`.
- `docs/runbooks/local-dev.md`: bootstrap, running the API/web later (placeholders allowed only as "added in Phase N" notes), `podman-compose --profile workflows up`, troubleshooting (SDK path, lock-file drift).
- `docs/runbooks/sandbox.md`: podman 5.x / cgroups v2 / user socket prerequisites for OpenShell, local compose provider notes.

### Claude's Discretion
File naming inside projects, exact analyzer severity tuning in `.editorconfig` (must keep warnings-as-errors green), choice between `latest-all` and `latest`+`AnalysisMode=All`, mermaid diagram styling, README wording.

</decisions>

<code_context>
## Existing Code Insights

The repository contains only `.planning/` and `AGENTS.md` (GSD-generated). Toolchain facts (verified): .NET SDK 10.0.402 is installed at `/root/.dotnet` (system `/usr/lib/dotnet` has only 8.0.131 — executors must `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`); node 24 / bun 1.4 / pnpm 11.8; Docker present but must not be used; podman/podman-compose absent locally (CI installs podman-compose via pip for `config` validation only); `omp` 18.12.3 installed (not needed in Phase 1). nuget.org reachable.

</code_context>

<specifics>
## Specific Ideas

- Solution file uses the `.slnx` format (`dotnet new sln --format slnx` or `dotnet sln migrate`); `OmpOS.Linux.slnf` lists every project except `apps/OmpOS.App`.
- Architecture test encodes the layer map as data (dictionary project→layer) so adding a project is a one-line change; include a negative test proving the rule fires (e.g. a test-only assembly fixture or an assertion on a synthetic violation) so the gate is demonstrably live, not just green.
- Contracts tests: round-trip every `UiBlock`/`StreamEvent` variant through `OmpOsJsonContext`, prove unknown `kind` deserializes to `UnknownBlock`/`UnknownEvent` (never throws), prove `Seq` ordering comparer, prove `ContractVersion` constant format.
- `.gitattributes` with `* text=auto eol=lf`; `.gitignore` from `dotnet new gitignore` plus `.env`, `*.user`, `TestResults/`.
- Every file created in this phase is git-tracked source; no generated artifacts committed except `packages.lock.json` and (later) `openapi.json`.

</specifics>

<deferred>
## Deferred Ideas

- Parser, harness, sessions, API, GenUi reducer, UI components — Phase 2/4.
- `Dockerfile`s, `railway.toml`, `vercel.json` — Phase 2 (API) / Phase 4 (web).
- TODO-link-to-open-question enforcement test — Phase 5 (documented in CONTRIBUTING now).

</deferred>
