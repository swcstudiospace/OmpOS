# Phase 1: Foundation - Research

**Researched:** 2026-10-20
**Domain:** .NET 10 monorepo governance (SDK pinning, CPM + lock files, analyzers, `dotnet format`, xunit.v3 on Microsoft.Testing.Platform, ArchUnitNET layer tests, STJ source-generated contracts, GitHub Actions, podman-compose, bootstrap script)
**Confidence:** HIGH — every toolchain claim below was exercised against the real SDK 10.0.402 in a scratch solution (`/tmp/ompos-probe`, `/tmp/ompos-vuln`, `/tmp/ompos-compose`, `/tmp/tcli`) or read from official documentation this session. Items that could not be exercised locally (podman itself, macOS runner, gitleaks licence) are tagged `[ASSUMED]` or `[CITED]`.

Provenance tags: `[VERIFIED: probe …]` = executed this session against SDK 10.0.402 / nuget.org / PyPI / Docker Hub / the real `temporal` 1.9.1 binary (HIGH). `[CITED: url]` = read from official documentation this session (MEDIUM per `classify-confidence --provider webfetch --verified`). `[ASSUMED]` = training knowledge (LOW) → listed in the Assumptions Log.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### D-01 Repository layout (ADR-0001) — LOCKED
- Root (build governance only): `OmpOS.slnx`, `Directory.Build.props`, `Directory.Build.targets` (if needed), `Directory.Packages.props`, `global.json`, `.editorconfig`, `.gitignore`, `.gitattributes`, `README.md`, `CONTRIBUTING.md`, `bootstrap.sh`, `.env.example`, `.github/workflows/ci.yml`, `OmpOS.Linux.slnf` (solution filter excluding the MAUI head).
- `src/` libraries: `OmpOS.Contracts` (L0), `OmpOS.Parser`, `OmpOS.Harness.Abstractions`, `OmpOS.Sandbox.Abstractions` (L1), `OmpOS.Harness.Acp`, `OmpOS.Harness.Rpc`, `OmpOS.Harness.InMemory`, `OmpOS.Sessions`, `OmpOS.GenUi`, `OmpOS.Sandbox`, `OmpOS.Workflows` (L2), `OmpOS.UI` Razor Class Library (L2, references Contracts + GenUi only).
- `services/`: `OmpOS.Api`, `OmpOS.Workflows.Worker` (L3).
- `apps/`: `OmpOS.Web` (Blazor WebAssembly standalone), `OmpOS.App` (.NET MAUI Blazor Hybrid, Phase 4) (L4).
- `tests/`: one `<Project>.Tests` per src/services project that has code, plus `OmpOS.Harness.ContractTests`, `OmpOS.Sandbox.ContractTests`, `OmpOS.Api.IntegrationTests`, `OmpOS.ArchitectureTests`. In Phase 1 create only `OmpOS.Contracts.Tests` and `OmpOS.ArchitectureTests`; later phases add the rest.
- `docs/`: `architecture.md`, `decisions/ADR-0001-monorepo-layout.md`, `decisions/ADR-0002-surface-technology.md`, `open-questions.md`, `runbooks/local-dev.md`, `runbooks/sandbox.md`.
- `deploy/`: `compose.yaml` (podman-compose), `Dockerfile` placeholders are NOT created until a service exists (Phase 2/3); `vercel.json` arrives in Phase 4.
- Phase 1 may create empty-but-compiling placeholder projects for every src/services project listed above so the layer graph and solution are complete from day one; each placeholder contains only an `AssemblyInfo`/marker type with an XML doc comment, no TODO code.

#### D-02 Dependency layers (ADR-0001) — LOCKED, asserted by `OmpOS.ArchitectureTests` (TngTech.ArchUnitNET.xUnitV3)
- L0 `OmpOS.Contracts` references nothing.
- L1 `Parser`, `Harness.Abstractions`, `Sandbox.Abstractions` → Contracts only.
- L2 `Harness.Acp`, `Harness.Rpc`, `Harness.InMemory`, `Sessions`, `GenUi`, `Sandbox`, `Workflows`, `UI` → L0/L1 (and `Sessions` may reference `GenUi`; `UI` references `Contracts` + `GenUi`).
- L3 `Api`, `Workflows.Worker` → L0–L2.
- L4 `Web` → Contracts + UI (+ GenUi); `App` (MAUI) → Contracts + UI + Harness.* (desktop in-process harness).
- Nothing references `OmpOS.Api` except tests. No `omp`/process-spawning assembly (`Harness.Acp`, `Harness.Rpc`) may be referenced by `OmpOS.Web`.

#### D-03 Contracts ownership (ADR-0001) — LOCKED
`OmpOS.Contracts` owns: `Session`, `Message`, `SessionStatus { Idle, Running, AwaitingToolApproval, Completed, Failed, Cancelled }`, `IntentEnvelope`, `IntentKind`, `Diagnostic`, `UiBlock` variants (`MarkdownBlock`, `CodeBlock`, `ToolCallBlock`, `PermissionBlock`, `PlanBlock`, `DiffPlaceholderBlock`, `ErrorBlock`, `A2uiSurfaceBlock`, `UnknownBlock`), `StreamEvent` (with `long Seq`, `SessionId`, `CorrelationId`, timestamp, polymorphic payload), `SandboxDescriptor` (incl. `Isolation { Process, Container, Vm, Enclave }`, `Attestation { None, Quote }`, `NativeBinaries`), `SandboxStatus { Requested, Provisioning, Ready, Attached, Degraded, TearingDown, Destroyed, Failed }`, `WorkflowRun`, `WorkflowRunStatus`, `OmpOSError`/`ErrorCode`, `ContractVersion` (semver string constant + `X-OmpOS-Contract-Version` header name). One `OmpOsJsonContext : JsonSerializerContext` with `[JsonPolymorphic]`/`[JsonDerivedType]` discriminators (`kind`) and `FallBackToNearestAncestor` for unknown block/event kinds; `JsonSerializerIsReflectionEnabledByDefault=false` set in `Directory.Build.props`. Contracts target `net10.0` (single TFM everywhere).

#### D-04 Build governance (n6) — LOCKED
- `global.json`: `{"sdk": {"version": "10.0.402", "rollForward": "latestPatch"}, "test": {"runner": "Microsoft.Testing.Platform"}}`.
- `Directory.Build.props`: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`, `AnalysisLevel=latest-all` (or `latest` + `AnalysisMode=All`), `EnforceCodeStyleInBuild=true`, `Deterministic=true`, `ContinuousIntegrationBuild` when `CI=true`, `ManagePackageVersionsCentrally=true`, `RestorePackagesWithLockFile=true`, `RestoreLockedMode` when `CI=true`, `JsonSerializerIsReflectionEnabledByDefault=false`, `GenerateDocumentationFile=true`, `LangVersion=latest`, `InvariantGlobalization` left default. Analyzer packages via CPM `GlobalPackageReference`: `Meziantou.Analyzer`, `Microsoft.VisualStudio.Threading.Analyzers`, `Microsoft.CodeAnalysis.BannedApiAnalyzers` with a `BannedSymbols.txt` banning `Newtonsoft.Json`, `System.Diagnostics.Process.Start(string)` outside `OmpOS.Harness.*`/`OmpOS.Sandbox` (use a conditional include), and `Task.Result`/`.Wait()`.
- `Directory.Packages.props`: central versions verified in `.planning/research/STACK.md` (xunit.v3 4.0.2, Shouldly 4.3.0, NSubstitute 6.2.0, TngTech.ArchUnitNET.xUnitV3 0.13.4, coverlet.MTP 10.1.0, Markdig 1.4.2, DiffPlex 1.9.0, Microsoft.AspNetCore.OpenApi 10.0.13, Temporalio 1.20.0, OpenTelemetry 1.20.0, bunit 2.12.5, Verify.XunitV3 33.3.4). Only versions that `dotnet restore` actually resolves may be committed — the executor verifies by restoring; if a version does not resolve, pick the latest resolvable stable and note it in the ADR.
- `.editorconfig` is the single rule source for formatting and code-style severities; `dotnet format --verify-no-changes` must pass; CSharpier is NOT added (one formatter only — `dotnet format`).
- Lock files: committed `packages.lock.json` for every project; CI uses `dotnet restore --locked-mode`.
- Test projects use xunit.v3 on Microsoft.Testing.Platform (`<OutputType>Exe</OutputType>`, `UseMicrosoftTestingPlatformRunner`), Shouldly assertions, `coverlet.MTP` for coverage.

#### D-05 Bootstrap and CI (n6) — LOCKED
- `bootstrap.sh` (bash, `set -euo pipefail`): ensures the SDK pinned in `global.json` is available (uses `$DOTNET_ROOT`/`~/.dotnet` via `dotnet-install.sh --jsonfile global.json --install-dir ~/.dotnet` when missing), exports `DOTNET_ROOT`/`PATH`, then `dotnet restore OmpOS.Linux.slnf --locked-mode`, `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror`, `dotnet test OmpOS.Linux.slnf -c Release --no-build`, `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`; exits non-zero on any failure; prints a summary. Never installs Docker; checks `podman-compose` presence and prints a hint (non-fatal) when absent.
- `.github/workflows/ci.yml`: job `build-test` on `ubuntu-latest` (`actions/setup-dotnet` with `global-json-file`, `dotnet restore --locked-mode`, build `-warnaserror`, test with TRX/coverage artifacts, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive` failing on any vulnerability, secret scan with `gitleaks/gitleaks-action`, `podman-compose config -f deploy/compose.yaml` using `pip install podman-compose`); job `shells` on `macos-15` that installs the `maui` workload and builds `apps/OmpOS.App` only when the project exists (guarded step), `continue-on-error: false`. Concurrency group per ref, `permissions: contents: read`.
- `deploy/compose.yaml`: services `temporal` (`image: temporalio/temporal:1.9.1`, command `server start-dev --ip 0.0.0.0 --db-filename /data/temporal.db`, ports 7233/8233, volume, healthcheck via `nc -z localhost 7233` fallback) under profile `workflows`; a commented note that `podman-compose --profile workflows up` is the only supported invocation; no `version:` key; no Docker-specific extensions.

#### D-06 Surface technology (ADR-0002) — LOCKED (HITL q1/q2)
- Web = Blazor WebAssembly standalone 10.0 hosted as static files on Vercel (static publish, `CompressionEnabled=false`, env-driven API base URL). TypeScript client rejected by the user; OpenAPI 3.1 document kept for future generation.
- Native = .NET MAUI Blazor Hybrid (`Microsoft.Maui.Controls` 10.0.x) one head project `apps/OmpOS.App` targeting `net10.0-android;net10.0-ios;net10.0-maccatalyst;net10.0-windows10.0.19041.0`, sharing `OmpOS.UI`; desktop targets host the harness in-process, mobile targets are remote-only; excluded from `OmpOS.Linux.slnf`; built in the macOS CI job. Avalonia/Uno/console rejected (recorded with rationale).
- Backend→client streaming = SSE (`TypedResults.ServerSentEvents`, `id:` = Seq, `Last-Event-ID` + `?since=` resume); WebSocket/SignalR rejected with rationale (proxies, HTTP/2 multiplexing, resume semantics).
- Engine integration = custom C# ACP v1 client (`omp acp`, JSON-RPC 2.0 over stdio) and omp RPC JSONL client (`omp --mode rpc`); community ACP .NET packages (LibAcp, AgentClientProtocol, SvcSystems.AI.AgentClientProtocol) rejected because the user asked for owned components and none covers omp RPC.
- Durable workflows = Temporalio 1.20.0 behind `Workflows:Enabled` (default false) with `DirectSessionRunner`; Generative UI = own `UiBlock` contract + A2UI v0.9.1 surface block; sandbox layering = fake → local podman-compose → OpenShell(podman) → agentOS stub → attested-enclave stub (dstack-shaped). ADR-0002 records each with alternatives and consequences.

#### D-07 Docs skeleton — LOCKED
- `README.md`: what OmpOS is (two paragraphs from PROJECT.md), prerequisites, `./bootstrap.sh`, run/test commands, repo map table, "What is verified where" table (Linux: libraries/API/web/tests; macOS CI: MAUI head), links to docs.
- `CONTRIBUTING.md`: branch/commit conventions (`Refs SPE-xxxxx`), formatting, lock-file update procedure (`dotnet restore --force-evaluate`), adding a project (update `.slnx`, `.slnf`, architecture test layer map), TODO policy (owner tag + `docs/open-questions.md` link, enforced by a test in Phase 5).
- `docs/architecture.md`: layer diagram (mermaid), component list from research ARCHITECTURE.md §components, data flow summary.
- `docs/open-questions.md`: table `ID | Question | Why it matters | Default taken | Owner | Status`, seeded from `.planning/research/SUMMARY.md` open questions (14) and the spec ambiguities Q1–Q8, owner `ompos-maintainers`.
- `docs/runbooks/local-dev.md`: bootstrap, running the API/web later (placeholders allowed only as "added in Phase N" notes), `podman-compose --profile workflows up`, troubleshooting (SDK path, lock-file drift).
- `docs/runbooks/sandbox.md`: podman 5.x / cgroups v2 / user socket prerequisites for OpenShell, local compose provider notes.

### Claude's Discretion
File naming inside projects, exact analyzer severity tuning in `.editorconfig` (must keep warnings-as-errors green), choice between `latest-all` and `latest`+`AnalysisMode=All`, mermaid diagram styling, README wording.

### Deferred Ideas (OUT OF SCOPE)
- Parser, harness, sessions, API, GenUi reducer, UI components — Phase 2/4.
- `Dockerfile`s, `railway.toml`, `vercel.json` — Phase 2 (API) / Phase 4 (web).
- TODO-link-to-open-question enforcement test — Phase 5 (documented in CONTRIBUTING now).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FOUND-01 | One command (`./bootstrap.sh`) builds and tests the Linux-buildable solution from a clean clone, installs/uses the pinned SDK via `global.json`, exits non-zero on failure | §VF-8 (`dotnet-install.sh --jsonfile` + "already installed" exit 0), §VF-2/9 (`.slnf` accepted by restore/build/test/format), file shape §FS-7 |
| FOUND-02 | Nullable, TWAE, analyzers at `AnalysisLevel` latest, deterministic, `ContinuousIntegrationBuild` in CI, enforced from `Directory.Build.props` | §VF-4 (props verified by building), §FS-2, pitfalls P-1..P-4 (which rules fire under `latest-all`) |
| FOUND-03 | CPM + committed `packages.lock.json` per project; CI restores `--locked-mode` | §VF-4 (lock files generated for every project incl. libraries; NU1004 on drift; `--force-evaluate` relock), §FS-3, pitfall P-5 (`CI=true` is set on the dev box) |
| FOUND-04 | `.editorconfig` single rule source; `dotnet format --verify-no-changes` passes | §VF-9 (`dotnet format <slnf>` works, exit code 2 on drift), §FS-4 (template defaults that must be overridden), pitfall P-6 |
| FOUND-05 | CI: locked restore, TWAE build, tests, format verify, `dotnet list package --vulnerable`, `podman-compose config`; gated macOS MAUI job | §VF-6 (action versions, `global-json-file`, cache, gitleaks v3 licence), §VF-7, §FS-5 (ci.yml skeleton), pitfalls P-7 (`--vulnerable` exits 0), P-8 (`podman-compose config` needs `--profile` and a `podman` binary) |
| FOUND-06 | `OmpOS.Contracts` defines all shapes once with STJ source generation | §VF-5 (unknown `kind` throws under `[JsonPolymorphic]`; verified custom-converter pattern that never throws and round-trips raw payload), §FS-8 code |
| FOUND-07 | ArchUnitNET test fails on upward layer references or non-test references to `OmpOS.Api` | §VF-3 (API/usings verified; negative test pattern; **unused `ProjectReference` is invisible to ArchUnitNET** → add csproj-graph test), §FS-9 |
| FOUND-08 | `compose.yaml` validated by `podman-compose config`; Temporal dev server via CLI image; no Docker-only tooling | §VF-7 (tag 1.9.1 exists; image = alpine + `temporal` binary; `temporal operator cluster health` exit codes verified live), §FS-6 |
| FOUND-09 | Docs skeleton exists with content | §FS-10 (content sources per file; open-questions seed list) |
</phase_requirements>

## Summary

Phase 1 is a governance-and-contracts phase whose risk is almost entirely "a locked decision names a flag/version/behaviour that does not do what the decision assumes". Every such flag was therefore executed this session on the pinned SDK 10.0.402 rather than looked up. The toolchain decisions in D-04/D-05 hold up: `.slnx` + `.slnf` work end-to-end with restore/build/test/format/`list package`; `global.json` `test.runner` puts `dotnet test` into MTP mode where a `.slnf` is accepted positionally and via `--solution`; xunit.v3 4.0.2 + Shouldly 4.3.0 + ArchUnitNET.xUnitV3 0.13.4 + coverlet.MTP 10.1.0 + TrxReport 2.5.1 restore, build warning-free under `latest-all`, and produce TRX + Cobertura artifacts; CPM with `GlobalPackageReference` analyzers and `RestorePackagesWithLockFile` produces a lock file for **every** project (as D-04 requires) and `--locked-mode` fails with NU1004 on drift.

Four findings change what the planner should write, and none of them contradicts a locked decision's *intent*: (1) **`FallBackToNearestAncestor` does not make unknown `kind` values deserialize** — STJ throws `JsonException: Read unrecognized type discriminator id 'a2ui'`; the verified fix is a small `JsonConverter<UiBlock>` on the abstract base that maps known kinds to the source-generated derived `JsonTypeInfo`s and everything else to `UnknownBlock` (raw payload preserved, byte-identical round trip). (2) **ArchUnitNET cannot see a `ProjectReference` that no type uses** (the compiler drops it from metadata), so with Phase 1's empty placeholder projects the layer test must also parse the `.csproj` graph; the ArchUnitNET rules remain for type-level enforcement once code exists, and a rule over zero types *fails* in ArchUnitNET, so every placeholder needs its marker type. (3) **`dotnet list package --vulnerable` exits 0 even when vulnerabilities are present** — CI must fail on the JSON output (or rely on NuGet audit, which under `TreatWarningsAsErrors` already turns NU1903 into a restore error). (4) **`gitleaks/gitleaks-action@v2` is a Node 20 action and no longer runs on GitHub-hosted runners after 2026-09-16** per the action's own README — use `@v3`; likewise `actions/checkout@v7` and `actions/setup-dotnet@v6` are current.

Two environment facts matter for execution: the dev box exports `CI=true`, so the `RestoreLockedMode`/`ContinuousIntegrationBuild` conditions in `Directory.Build.props` are *active locally* (any dependency change needs `dotnet restore --force-evaluate`); and `podman` is absent locally, so `podman-compose config` can only run in CI (ubuntu-24.04 runners ship Podman 4.9.3) — `podman-compose config` also prints `services: {}` unless `--profile workflows` is passed, and it is a YAML normaliser, not a schema validator.

**Primary recommendation:** Keep every D-04/D-05 flag exactly as locked, apply the four corrections above (custom converter, csproj-graph test, JSON-parsed vulnerability gate, gitleaks v3 / checkout v7 / setup-dotnet v6), and seed `.editorconfig` from `dotnet new editorconfig` with the five overrides listed in §FS-4 so that `latest-all` + `-warnaserror` + `dotnet format --verify-no-changes` are green on day one.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| SDK pinning / bootstrap | Build tooling (repo root: `global.json`, `bootstrap.sh`) | CI (`actions/setup-dotnet` reads the same `global.json`) | One source of truth for the SDK version; both local and CI consume it |
| Compiler/analyzer policy | Build tooling (`Directory.Build.props`, `.editorconfig`) | — | MSBuild import chain applies it to every project without per-project opt-in |
| Package versions + lock files | Build tooling (`Directory.Packages.props`, `packages.lock.json`) | CI (`--locked-mode`) | CPM owns versions; lock files make restore reproducible; CI enforces |
| Shared contracts (`OmpOS.Contracts`) | Library tier L0 | — | Zero dependencies so every layer (WASM, MAUI, API, tests) compiles against it |
| JSON serialization of contracts | Library tier L0 (`OmpOsJsonContext`, converters) | — | Source-generated, reflection disabled; consumers only pass `JsonTypeInfo` |
| Layer-direction enforcement | Test tier (`OmpOS.ArchitectureTests`) | CI (fails the `dotnet test` step) | Mechanical gate; csproj-graph + ArchUnitNET |
| CI pipeline | CI (`.github/workflows/ci.yml`) | — | Runs the same commands as `bootstrap.sh` plus audit/scan/compose checks |
| Local dependency stack | Deploy tooling (`deploy/compose.yaml`) | CI (`podman-compose config` validation) | podman-compose only; Temporal dev server under a profile |
| Documentation skeleton | Docs (`README.md`, `CONTRIBUTING.md`, `docs/**`) | — | Human-facing; content sourced from `.planning/` research |

## Verified Facts (numbered to match the planner's question list)

### VF-1 — `global.json`, MTP mode, xunit.v3 4.0.x under `dotnet test`, TRX, coverage, package versions

- `global.json` `{"sdk":{"version":"10.0.402","rollForward":"latestPatch"},"test":{"runner":"Microsoft.Testing.Platform"}}` is accepted; `dotnet --version` → `10.0.402`. `[VERIFIED: probe /tmp/ompos-probe/global.json; dotnet --version]`
- MTP mode of `dotnet test` is enabled by that `test.runner` key; docs: "To enable this mode, add the following configuration to your global.json file: `{ "test": { "runner": "Microsoft.Testing.Platform" } }`"; `dotnet test MySolution.sln` "should become `dotnet test --solution MySolution.sln`", `--project` for a project, `--test-modules` for a dll. `[CITED: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test]`
- In MTP mode, **both** `dotnet test OmpOS.Linux.slnf …` (positional, as D-05 writes it) and `dotnet test --solution OmpOS.Linux.slnf …` run the test executables and print `Test run summary: Passed! total: N …`, exit 0. `[VERIFIED: probe — both invocations run, 6 and 8 tests passed]`
- Test project shape that works warning-free: `<OutputType>Exe</OutputType>`, `<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>`, `<IsTestProject>true</IsTestProject>`, package refs `xunit.v3`, `Shouldly`, `TngTech.ArchUnitNET.xUnitV3`, `coverlet.MTP`, `Microsoft.Testing.Extensions.TrxReport` (no `Microsoft.NET.Test.Sdk`, no `xunit.runner.visualstudio`, no `coverlet.collector`). `[VERIFIED: probe tests/Lib.Tests/Probe.Lib.Tests.csproj]`. xunit docs confirm `UseMicrosoftTestingPlatformRunner` replaces the xunit native CLI with the MTP CLI; `TestingPlatformDotnetTestSupport` is the SDK 8/9 mechanism and is unnecessary once `global.json` selects MTP. `[CITED: https://xunit.net/docs/getting-started/v3/microsoft-testing-platform]`
- xunit.v3 build gate: a non-test project that references `xunit.v3` fails with "xUnit.net v3 test projects must be executable (set project property '<OutputType>Exe</OutputType>')". `[VERIFIED: probe — error from xunit.v3.core.mtp-v2.targets(15,5)]`
- The SDK 10.0.402 `dotnet new xunit` template still scaffolds **xunit 2.9.3 + Microsoft.NET.Test.Sdk + coverlet.collector (VSTest)** — do not use the template; hand-write the test csproj. `[VERIFIED: probe — `dotnet new xunit` output]`
- TRX + coverage under MTP: `dotnet test OmpOS.Linux.slnf -c Release --no-build --report-trx --coverlet --coverlet-output-format cobertura --results-directory TestResults` produces `TestResults/<Project>_net10.0_x64.trx` and `TestResults/coverage.cobertura.<timestamp>.xml`, exit 0. The `--` separator is **not** needed in MTP mode (docs: "Even though `--` is no longer required in MTP mode, you can still use it"). The MTP options exposed by the compiled test exe include `--report-trx`, `--results-directory`, `--coverlet`, `--coverlet-output-format`, `--coverlet-threshold`, `--filter-*`, `--minimum-expected-tests`, `--fail-skips`, `--fail-warns`. `[VERIFIED: probe — `Probe.Lib.Tests --help` and the artifact paths above]`. `--report-trx` requires `Microsoft.Testing.Extensions.TrxReport` in each test project (docs: "The `--report-trx` option isn't built into MTP. Each targeted test application must register the extension by referencing Microsoft.Testing.Extensions.TrxReport"). `[CITED: same learn page]`
- Versions on nuget.org (flat-container index + registration, read 2026-10-20): `[VERIFIED: nuget.org v3 API]`

| Package | Latest stable | Published | Notes |
|---|---|---|---|
| xunit.v3 | **4.0.2** | 2026-10-09 | 50.1 M downloads, verified publisher |
| Shouldly | **4.3.0** | 2025-01-23 | 5.0.0-preview.2 exists — stay on 4.3.0 |
| TngTech.ArchUnitNET.xUnitV3 | **0.13.4** | 2026-08-20 | `TngTech.ArchUnitNET` also has a `2.1.0-draft` prerelease — ignore |
| coverlet.MTP | **10.1.0** | 2026-09-27 | depends on Microsoft.Testing.Platform 2.4.1 |
| Microsoft.Testing.Extensions.TrxReport | **2.5.1** | 2026-10-07 | matches Microsoft.Testing.Platform 2.5.1 |
| Meziantou.Analyzer | **3.0.315** | 2026-10-20 | |
| Microsoft.VisualStudio.Threading.Analyzers | **18.12.1** | 2026-10-12 | |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | **5.6.0** | 2026-07-02 | |
| NSubstitute | 6.2.0 | — | not needed in Phase 1 |
| xunit.analyzers / xunit.runner.visualstudio | 2.2.0 / 4.0.1 | — | analyzers arrive transitively with xunit.v3; runner.visualstudio not needed under MTP |

### VF-2 — `.slnx` + `.slnf` on SDK 10

- `dotnet new sln --name OmpOS --format slnx` creates `OmpOS.slnx` (XML `<Solution>` with `<Folder>`/`<Project Path=…>`); `dotnet sln OmpOS.slnx add <csproj>` adds projects into folders derived from the path. `[VERIFIED: probe — Probe.slnx content shown by probe]`
- `dotnet new slnf --name OmpOS.Linux --parent-solution OmpOS.slnx` scaffolds `{"solution":{"path":"OmpOS.slnx","projects":[]}}`; the template help states the parent may be "sln or slnx". Fill `projects` with repo-relative csproj paths (forward slashes). `[VERIFIED: probe — template output + `dotnet new slnf -h`]`
- A `.slnf` whose parent is a `.slnx` is accepted by `dotnet restore`, `dotnet build`, `dotnet test` (positional and `--solution`), `dotnet format`, and `dotnet list <slnf> package --vulnerable --include-transitive`. No `.sln` fallback is needed. `[VERIFIED: probe — all five commands exit 0 against Probe.Linux.slnf → Probe.slnx]`

### VF-3 — ArchUnitNET 0.13.4 xUnit v3 API and negative-test proof

- Namespaces/usings that compile against 0.13.4: `using ArchUnitNET.Domain; using ArchUnitNET.Loader; using ArchUnitNET.xUnitV3; using static ArchUnitNET.Fluent.ArchRuleDefinition;`. Loading: `new ArchLoader().LoadAssemblies(typeof(T1).Assembly, typeof(T2).Assembly).Build()` → `Architecture`. Rule: `Types().That().ResideInAssembly(assembly).Should().NotDependOnAny(Types().That().ResideInAssembly(other)).Because("…")`; `rule.Check(architecture)` throws `ArchUnitNET.xUnitV3.FailedArchRuleException` on violation; `rule.HasNoViolations(architecture)` returns `bool` for assertion-style tests. `[VERIFIED: probe tests/Lib.Tests/UnitTest1.cs — compiles, negative test produced FailedArchRuleException]`. The user guide documents the same shapes (`ResideInAssembly("ExampleAssembly")`, `.As("Example Layer")`, `NotDependOnAny`, `Slices().Matching(...).Should().BeFreeOfCycles()`). `[CITED: https://archunitnet.readthedocs.io/en/latest/guide/]`
- **Negative test that proves the gate fires:** assert a rule that *must* be violated (e.g. "the test assembly must not depend on Contracts") returns `HasNoViolations == false` and `Check` throws `FailedArchRuleException`. `[VERIFIED: probe — `Negative_Rule_Fires` passed with `ARCH-NEGATIVE: ArchUnitNET.xUnitV3.FailedArchRuleException`]`
- **Unused `ProjectReference` is invisible.** A placeholder assembly containing only a marker type and a `ProjectReference` to another project has `GetReferencedAssemblies() == [System.Runtime]` and the rule "placeholder should not depend on referenced project" **passes**. The C# compiler only records assemblies whose types are used. `[VERIFIED: probe — `EMPTY-REFS: System.Runtime`, `EMPTY-RULE-PASSES: True`]` → the layer test must additionally parse `ProjectReference` items from every `.csproj` (§FS-9).
- **A rule whose object set is empty fails** (`Types().That().ResideInAssembly("DoesNotExist").Should().NotDependOnAny(...)` → `HasNoViolations == false`). Every placeholder project therefore needs its marker type (D-01 already requires it), and the loader must receive every assembly in the layer map. `[VERIFIED: probe — `NO-TYPES-RULE-PASSES: False`]`
- Loading placeholders: the ArchitectureTests project must `ProjectReference` every src/services project so their DLLs land in its output; load them with `typeof(<Marker>).Assembly` or `Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "OmpOS.X.dll"))` — the latter worked for an assembly with no used types. `[VERIFIED: probe EmptyTests.cs]`

### VF-4 — `Directory.Build.props` / `Directory.Packages.props` correctness

All of the following were set in the probe's `Directory.Build.props` and produced a warning-free Release build with `-warnaserror`: `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`, **`AnalysisLevel=latest-all`** (valid — it activated CA1062/CA1303/CA1707/CA1716, which are off at `latest-recommended`), `EnforceCodeStyleInBuild=true`, `Deterministic=true`, `GenerateDocumentationFile=true`, `JsonSerializerIsReflectionEnabledByDefault=false` (confirmed at runtime: `JsonSerializer.Serialize(new { a = 1 })` → `InvalidOperationException: Reflection-based serialization has been disabled for this application`), `ManagePackageVersionsCentrally=true`, `RestorePackagesWithLockFile=true`, `NuGetAuditMode=all`, `NuGetAuditLevel=low`, and conditional `ContinuousIntegrationBuild=true` + `RestoreLockedMode=true` under `'$(CI)' == 'true'`. `[VERIFIED: probe Directory.Build.props + build/test output]`

- `Directory.Packages.props` with `ManagePackageVersionsCentrally=true`, `CentralPackageTransitivePinningEnabled=true`, `<PackageVersion>` items and `<GlobalPackageReference Include="Meziantou.Analyzer|Microsoft.VisualStudio.Threading.Analyzers|Microsoft.CodeAnalysis.BannedApiAnalyzers" Version=…/>` restores; the global analyzers run in every project (MA0048, VSTHRD002, RS0030 all fired). `[VERIFIED: probe]`
- Lock files: `RestorePackagesWithLockFile=true` writes `packages.lock.json` (`"version": 2`) for **every** project including class libraries (D-04's "every project" is satisfied automatically; PITFALLS.md's "heads only" is superseded by the locked decision). `[VERIFIED: probe — src/Lib, src/Empty, tests/Lib.Tests all have lock files]`
- Drift detection: changing a `PackageVersion` (Shouldly 4.3.0 → 4.2.1) or adding a `ProjectReference` and restoring in locked mode fails with `error NU1004: The package reference Shouldly version has changed from [4.3.0, ) to [4.2.1, ). The packages lock file is inconsistent … pass an explicit --force-evaluate option`. `dotnet restore <slnf> --force-evaluate` regenerates. `[VERIFIED: probe]`
- NuGet audit under TWAE: a vulnerable package makes **restore itself fail** — `error NU1903: Warning As Error: Package 'System.Text.Json' 8.0.0 has a known high severity vulnerability` (exit 1), whereas plain restore only warns (exit 0). `[VERIFIED: probe /tmp/ompos-vuln]`
- `NU1510` (SDK 10 package pruning): explicitly referencing a package that is already part of the shared framework on net10.0 (e.g. `System.Text.Json`) is a warning → **error under TWAE**. Do not add `PackageVersion`/`PackageReference` for `System.Text.Json`, `Microsoft.Extensions.*` abstractions that the framework provides. `[VERIFIED: probe — `error NU1510: Warning As Error: PackageReference System.Text.Json will not be pruned`]`
- `CentralPackageTransitivePinningEnabled`: no `CentralTransitive` entries appeared in the probe lock files because nothing needed pinning; the property restores cleanly. Changing it later rewrites lock files (PITFALLS 10) — decide once, now. `[VERIFIED: probe restore; rewrite consequence CITED: PITFALLS.md]`

### VF-5 — STJ source generation for the `kind`-discriminated hierarchies

- **Unknown discriminator on read throws** even with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]`: `System.Text.Json.JsonException: Read unrecognized type discriminator id 'a2ui'`. A payload with **no** `kind` throws `NotSupportedException: The JSON payload for polymorphic interface or abstract type 'UiBlock' must specify a type discriminator`. `FallBackToNearestAncestor` governs **serialization** of undeclared derived types (docs: "you can use the FallBackToNearestAncestor setting to fall back to the contract of the nearest declared derived type"). `[VERIFIED: probe — exceptions quoted from test output; CITED: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism]`
- `IgnoreUnrecognizedTypeDiscriminators = true` only works when the base is **concrete** (payload becomes the base type, discriminator and unknown fields are lost); on an abstract base it still throws the `NotSupportedException` above. It cannot produce a typed `UnknownBlock` with `OriginalKind`/raw payload. `[VERIFIED: probe Ev.cs/EvTests.cs — `IGNORE-ABSTRACT: NotSupportedException`, `IGNORE-CONCRETE: no exception`]`
- **Working pattern (recommended for `UiBlock` and `StreamEvent` payloads):** `[JsonConverter(typeof(UiBlockConverter))]` on the abstract base; the converter holds a static `kind ↔ Type` map, on read parses a `JsonDocument`, resolves known kinds through `options.GetTypeInfo(type)` (source-generated, AOT-safe) and returns `UnknownBlock { Id, OriginalKind, Raw = root.Clone() }` for unknown/missing kinds; on write serializes `UnknownBlock.Raw` verbatim, otherwise `JsonSerializer.SerializeToNode(value, options.GetTypeInfo(type))` then `JsonObject.Insert(0, "kind", …)`. Every derived type is listed in `[JsonSerializable]`. Verified behaviour: known kind writes `{"kind":"code","language":"cs","code":"x","id":"1"}`; unknown `{"kind":"a2ui","id":"9","surface":{"x":1}}` reads as `UnknownBlock(OriginalKind="a2ui", Id="9")` and re-serializes **byte-identical**; missing `kind` reads as `UnknownBlock(OriginalKind=null)`; arrays mix known and unknown; reflection stays disabled; trim/AOT analyzers (`IsTrimmable`, `IsAotCompatible`) are silent. Code in §FS-8. `[VERIFIED: probe src/Lib/UiBlock.cs + tests]`
- `required` members: with `[JsonSourceGenerationOptions(PropertyNamingPolicy = CamelCase, DefaultIgnoreCondition = WhenWritingNull, UseStringEnumConverter = true)]`, `required string Id { get; init; }` round-trips; a known kind missing a required property throws `JsonException` (STJ enforces `required`) — tests should assert that explicitly. `[VERIFIED: probe for the round trip; ASSUMED for the missing-required exception message text]`
- D-03's wording "`FallBackToNearestAncestor` for unknown block/event kinds" is satisfied in *intent* (unknown kinds never throw) by the converter; keep the attribute off the base when the converter is present (a converter replaces the polymorphic contract for that type). `[VERIFIED: probe — converter-based base compiles and behaves without `[JsonPolymorphic]`]`

### VF-6 — GitHub Actions

- `actions/setup-dotnet`: latest release **v6.0.0** (2026-07-16); README examples use `@v6` with `actions/checkout@v7` (checkout latest **v7.0.1**, 2026-07-20). v5 = node24 upgrade, v6 = ESM internals, "No changes to action inputs, outputs, or behavior". `global-json-file: <path>` installs the SDK from `global.json`; "The action supports `latest*` variants of the rollForward field … When set to `latestPatch` … the action installs the appropriate SDK version"; `sdk.version` must be fully qualified (`10.0.402` is). `cache: true` + `cache-dependency-path:` hashes `packages.lock.json` files ("If lock file does not exist, this action throws error"); with caching, set `NUGET_PACKAGES: ${{ github.workspace }}/.nuget/packages` and `DisableImplicitNuGetFallbackFolder=true` to avoid NU1403. `[CITED: https://github.com/actions/setup-dotnet/blob/main/README.md; VERIFIED: GitHub releases API for tags]`
- `gitleaks/gitleaks-action`: latest **v3.0.0** (2026-05-30). README: "**v3 migrates the GitHub Actions runtime from Node 20 to Node 24.** … **September 16, 2026:** Node 20 is removed from GitHub-hosted runners entirely. `gitleaks-action@v2` will stop working regardless of any opt-out flag." Env: `GITHUB_TOKEN` (automatic) and `GITLEAKS_LICENSE` — "required for organizations, not required for user accounts"; a free key is obtained at gitleaks.io. Needs `actions/checkout` with `fetch-depth: 0`. **D-05's `gitleaks/gitleaks-action` must be pinned `@v3`.** `[CITED: https://github.com/gitleaks/gitleaks-action/blob/master/README.md; VERIFIED: releases API]`
- `actions/upload-artifact` latest **v7.0.2** (2026-10-07). `[VERIFIED: releases API]`
- Runner labels: `ubuntu-latest` = Ubuntu 24.04 (x64) with **Podman 4.9.3, Python 3.12.3, pipx 1.16.7, .NET SDKs 8/9/10 incl. 10.0.401** preinstalled (10.0.402 is *not* preinstalled → `setup-dotnet` installs it). macOS: `macos-15` (arm64) and `macos-15-intel`/`macos-15-large` (x64) exist; `macos-latest` = `macos-26` (arm64); macOS 14 is deprecated. macOS 15/26 images preinstall .NET SDKs up to 10.0.400 and Xcode 16.4 (default) … 26.3. `[CITED: https://github.com/actions/runner-images/blob/main/README.md and images/macos/macos-15-arm64-Readme.md, images/ubuntu/Ubuntu2404-Readme.md]`
- podman-compose on PyPI: latest **1.6.0**, `requires_python >= 3.9`; `pip install podman-compose==1.6.0` resolves. `[VERIFIED: PyPI JSON API; pip install in /tmp/pcvenv]`
- `dotnet workload install maui` on `macos-15` was **not** exercised (no macOS here). `[ASSUMED]` — Phase 4 risk; Phase 1 only needs the guarded job skeleton.

### VF-7 — `temporalio/temporal` CLI image and healthcheck

- Docker Hub tags for `temporalio/temporal`: `1.9.1` (2026-09-14) is the newest versioned tag; `latest`/`release` point at the same date; previous `1.8.3`, `1.8.2`. `[VERIFIED: hub.docker.com v2 tags API]`
- Image definition (temporalio/cli `Dockerfile`): `FROM alpine:3.24.1`, `apk add --no-cache ca-certificates tzdata`, binary at `/usr/local/bin/temporal`, `adduser -u 1000 -D temporal`, `USER temporal`, `ENTRYPOINT ["temporal"]`. Consequences: compose `command:` is the CLI's sub-command (`server start-dev …`); the process runs as uid 1000 with home `/home/temporal`; busybox is present (so `nc` exists, but busybox `nc -z` support is `[ASSUMED]`). `[CITED: https://github.com/temporalio/cli/blob/main/Dockerfile]`
- Temporal's own dev compose uses exactly: `image: temporalio/temporal:latest`, `ports: 7233:7233, 8233:8233`, `volumes: temporal-data:/home/temporal`, `command: server start-dev --ip 0.0.0.0 --db-filename /home/temporal/temporal.db`. `[CITED: https://github.com/temporalio/samples-server/blob/main/compose/docker-compose-dev.yml]`
- CLI reference: `start-dev` flags `--db-filename/-f`, `--ip`, `--port/-p` (gRPC, default 7233), `--ui-port` (default gRPC+1000 = 8233), `--headless`, `--namespace/-n`, `--http-port`, `--metrics-port`, `--log-level`. `[CITED: https://docs.temporal.io/cli/server]`
- **Healthcheck verified with the real 1.9.1 binary:** `temporal operator cluster health --address 127.0.0.1:7233` prints `SERVING` and exits **0** when the dev server is up; exits **1** ("connection refused … No Temporal server is running") when it is not. Port 7233 listens on `*:7233` with `--ip 0.0.0.0`; `--db-filename` creates the SQLite file at the given path. `[VERIFIED: /tmp/tcli — `temporal version 1.9.1 (Server 1.32.0, UI 2.54.1)`]` → compose `healthcheck.test: ["CMD","temporal","operator","cluster","health","--address","127.0.0.1:7233"]` is the primary probe; `nc -z` is only a fallback and unverified.
- Volume path: D-05 says `/data/temporal.db`. Because the container runs as uid 1000 and `/data` does not exist in the image, a named volume mounted at `/data` is created by the runtime (root-owned on Docker; Podman behaviour `[ASSUMED]`) and the write may fail. Temporal's own compose mounts the volume at `/home/temporal` (exists, owned by `temporal`). **Recommendation:** `volumes: temporal-data:/home/temporal` + `--db-filename /home/temporal/temporal.db`; if the planner keeps `/data`, add the Podman `:U` mount option (`temporal-data:/data:U`) and note it in the runbook. `[CITED: Dockerfile + samples-server compose; ownership consequence ASSUMED]`

### VF-8 — `dotnet-install.sh`

- Flags present in the current script: `--jsonfile <JSONFILE>` ("Determines the SDK version from a user specified global.json file"), `-i, --install-dir <DIR>`, `--skip-non-versioned-files`, `--dry-run`, `--version`, `--channel`. The jsonfile parser reads the `"sdk"` block (`awk '/"sdk"/,/}/'`) — it reads `version` and ignores `rollForward`, so the exact `10.0.402` is resolved. `[VERIFIED: /tmp/dotnet-install.sh downloaded from https://dot.net/v1/dotnet-install.sh; dry run → `dotnet-sdk-10.0.402-linux-x64.tar.gz`, repeatable invocation `--version "10.0.402" --install-dir "/root/.dotnet"`]`
- Already-installed behaviour: `is_dotnet_package_installed` → `say "$asset_name with version '$effective_version' is already installed."` then **`exit 0`** — safe to call unconditionally from `bootstrap.sh`, but the script needs network for the version/URL resolution step even when installed, so `bootstrap.sh` should first check `"$DOTNET_ROOT/sdk/10.0.402"` (or `dotnet --version` under the repo's `global.json`) and only download the script when missing. `[VERIFIED: script lines 1430-1434, 1488-1491]`
- The dev box: system `/usr/lib/dotnet` has only 8.0.131; user install at `/root/.dotnet` has 10.0.402 → `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH` is required before any `dotnet` call (CONTEXT.md + `dotnet --list-sdks`). `[VERIFIED: probe]`

### VF-9 — Solution filter acceptance by `dotnet build` / `dotnet format`

- `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` and `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` both accept the filter. `dotnet format --verify-no-changes` exits **2** and lists `error WHITESPACE` / `error FINALNEWLINE` / `error IDE0005` diagnostics when formatting is off; exit 0 when clean. `dotnet format OmpOS.Linux.slnf --no-restore` (no verify) rewrites files; a subsequent verify passes and the TWAE build still passes (format and build agree when `.editorconfig` is the only rule source). `[VERIFIED: probe]`
- `.slnf` excluding `apps/OmpOS.App` is simply a `projects` list without that path; nothing else is needed. `[VERIFIED: probe mechanics]`

### VF-10 — BannedApiAnalyzers `BannedSymbols.txt`

- Syntax = documentation-comment ID + optional `;description`: `T:N.BannedType;Don't use BannedType`, `` T:N.BannedType`1 ``, `M:N.BannedType.BannedMethod(System.Int32)`, `P:N.BannedType.BannedProperty`, `N:N` (whole namespace). Files named `BannedSymbols.txt` or `BannedSymbols.*.txt` must be `<AdditionalFiles Include=…/>`. `[CITED: https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md]`
- Entries that fire (RS0030, error under TWAE): `` P:System.Threading.Tasks.Task`1.Result;Use await `` → "The symbol 'Task<TResult>.Result' is banned in this project"; `M:System.Threading.Tasks.Task.Wait();Use await`; `M:System.Diagnostics.Process.Start(System.String);…` → "The symbol 'Process.Start(string)' is banned". `N:Newtonsoft.Json;…` parses (no Newtonsoft reference existed to trigger it). `Microsoft.VisualStudio.Threading.Analyzers` independently reports `VSTHRD002` on `.Wait()`/`.Result`. `[VERIFIED: probe src/Lib/Banned.cs]`
- Per-project scoping: include the root file for everyone and a second file only outside the process-spawning projects — `<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />` plus `<AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.NoProcess.txt" Condition="!$(MSBuildProjectName.StartsWith('OmpOS.Harness.')) And '$(MSBuildProjectName)' != 'OmpOS.Sandbox' And !$(MSBuildProjectName.EndsWith('Tests'))" />` in `Directory.Build.props`. The `MSBuildThisFileDirectory` + root include was exercised; the multi-file/conditional split is the documented `BannedSymbols.*.txt` mechanism. `[VERIFIED: single-file include; CITED: multi-file naming]`

## Standard Stack

### Core (Phase 1 consumes)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET SDK | 10.0.402 (LTS) | Everything | Pinned in `global.json`; installed at `/root/.dotnet` `[VERIFIED: probe]` |
| xunit.v3 | 4.0.2 | Test framework on MTP v2 | `[VERIFIED: nuget.org]` |
| Shouldly | 4.3.0 | Assertions (BSD-3) | `[VERIFIED: nuget.org]` |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.4 | Layer rules | `[VERIFIED: nuget.org + probe]` |
| coverlet.MTP | 10.1.0 | Coverage under MTP (`--coverlet`) | `[VERIFIED: probe produced cobertura]` |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | `--report-trx` | `[VERIFIED: probe produced .trx]` |
| Meziantou.Analyzer | 3.0.315 | Extra analyzers (GlobalPackageReference) | `[VERIFIED: nuget.org + probe]` |
| Microsoft.VisualStudio.Threading.Analyzers | 18.12.1 | VSTHRD rules | `[VERIFIED]` |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | RS0030 bans | `[VERIFIED]` |
| podman-compose | 1.6.0 (PyPI) | Compose validation in CI | `[VERIFIED: PyPI]` |
| temporalio/temporal image | 1.9.1 | Local Temporal dev server | `[VERIFIED: Docker Hub + binary]` |
| actions/checkout, actions/setup-dotnet, actions/upload-artifact, gitleaks/gitleaks-action | v7, v6, v7, v3 | CI | `[VERIFIED: GitHub releases API]` |

### Supporting (declared in `Directory.Packages.props` now, consumed in later phases)
D-04 lists NSubstitute 6.2.0, Markdig 1.4.2, DiffPlex 1.9.0, Microsoft.AspNetCore.OpenApi 10.0.13, Temporalio 1.20.0, OpenTelemetry 1.20.0, bunit 2.12.5, Verify.XunitV3 33.3.4 — versions from STACK.md (read from nuget.org on 2026-10-20 by the stack researcher). Declaring a `PackageVersion` that no project references is harmless under CPM (no restore, no lock-file entry), so the executor may pre-declare them; only NSubstitute 6.2.0 was re-verified this session. `[VERIFIED: NSubstitute 6.2.0; CITED: STACK.md for the rest]`

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom `JsonConverter<UiBlock>` for unknown kinds | Concrete base + `IgnoreUnrecognizedTypeDiscriminators` | Loses `OriginalKind` and the raw payload; forces a non-abstract base — rejected (D-03 needs `UnknownBlock`) |
| csproj-graph test + ArchUnitNET | ArchUnitNET only | Blind to unused `ProjectReference`s (VF-3) — must combine |
| JSON-parsed `dotnet list package --vulnerable` gate | Rely on NuGet audit (NU1903 as error at restore) | Audit already fails restore under TWAE; the explicit list step is D-05's visible report — keep both, make the list step fail on findings |
| `dotnet new editorconfig` + overrides | Hand-written `.editorconfig` | Template is 389 lines of sensible defaults; overriding 5 keys is less drift than authoring |

**Installation:** none beyond the SDK — all packages arrive via `dotnet restore` from `Directory.Packages.props`. `pip install podman-compose==1.6.0` in CI only.

## Package Legitimacy Audit

> `gsd_run query package-legitimacy check` supports only `npm|pypi|crates` (`Error: Usage: … --ecosystem <npm|pypi|crates>`), so NuGet signals were collected directly from the nuget.org search/registration APIs on 2026-10-20. `[VERIFIED]`

| Package | Registry | Age | Downloads | Source Repo / Owner | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| xunit.v3 | NuGet | 4.0.0 since 2026-08; project since 2024 | 50.1 M | xunit.net (verified publisher) | OK | Approved |
| Shouldly | NuGet | 10+ yrs | 129.1 M | docs.shouldly.org / github.com/shouldly | OK | Approved |
| TngTech.ArchUnitNET.xUnitV3 | NuGet | 0.13 line 2025–2026 | 0.79 M | TNG Technology Consulting (github.com/TNG/ArchUnitNET) | OK | Approved (lower downloads reflect the v3 adapter's age, same publisher as the 2019-era core package) |
| coverlet.MTP | NuGet | 10.0.0 2026 | 1.03 M | github.com/coverlet-coverage/coverlet (verified) | OK | Approved |
| Meziantou.Analyzer | NuGet | 6+ yrs | 25.5 M | github.com/meziantou/Meziantou.Analyzer (verified) | OK | Approved |
| Microsoft.VisualStudio.Threading.Analyzers | NuGet | 8+ yrs | 138.4 M | microsoft.github.io/vs-threading (verified) | OK | Approved |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | NuGet | 6+ yrs | 49.1 M | github.com/dotnet/roslyn (verified) | OK | Approved |
| Microsoft.Testing.Extensions.TrxReport | NuGet | 2024+ | 23.9 M | github.com/microsoft/testfx (verified) | OK | Approved |
| podman-compose | PyPI | 1.x since 2023 | n/a | github.com/containers/podman-compose | OK | Approved |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Recommended File Shapes

### FS-1 `global.json` `[VERIFIED: probe]`
```json
{
  "sdk": { "version": "10.0.402", "rollForward": "latestPatch" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

### FS-2 `Directory.Build.props` `[VERIFIED: probe — identical property set built warning-free]`
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-all</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Deterministic>true</Deterministic>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    <NuGetAuditMode>all</NuGetAuditMode>
    <NuGetAuditLevel>low</NuGetAuditLevel>
    <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
    <IsTrimmable>true</IsTrimmable>
  </PropertyGroup>

  <PropertyGroup Condition="'$(CI)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
    <RestoreLockedMode>true</RestoreLockedMode>
  </PropertyGroup>

  <!-- Test projects: xunit.v3 on Microsoft.Testing.Platform -->
  <PropertyGroup Condition="$(MSBuildProjectName.EndsWith('Tests'))">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <OutputType>Exe</OutputType>
    <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
    <IsTrimmable>false</IsTrimmable>
  </PropertyGroup>

  <ItemGroup>
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" Link="BannedSymbols.txt" />
    <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.NoProcess.txt" Link="BannedSymbols.NoProcess.txt"
                     Condition="!$(MSBuildProjectName.StartsWith('OmpOS.Harness.')) And '$(MSBuildProjectName)' != 'OmpOS.Sandbox' And !$(MSBuildProjectName.EndsWith('Tests'))" />
  </ItemGroup>
</Project>
```
Notes: `IsTrimmable=true` on libraries makes IL warnings build-time errors (PITFALLS 8); it was silent on the contracts probe. `DisableImplicitNuGetFallbackFolder` is the setup-dotnet README's NU1403 guard. Test projects keep `GenerateDocumentationFile=true` (needed for IDE0005) and silence `CS1591` via `.editorconfig` (§FS-4). `[VERIFIED: probe for CS1591 + IDE0005 behaviour]`

### FS-3 `Directory.Packages.props` `[VERIFIED: probe for the Phase-1 subset]`
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup Label="Tests">
    <PackageVersion Include="xunit.v3" Version="4.0.2" />
    <PackageVersion Include="Shouldly" Version="4.3.0" />
    <PackageVersion Include="NSubstitute" Version="6.2.0" />
    <PackageVersion Include="TngTech.ArchUnitNET.xUnitV3" Version="0.13.4" />
    <PackageVersion Include="coverlet.MTP" Version="10.1.0" />
    <PackageVersion Include="Microsoft.Testing.Extensions.TrxReport" Version="2.5.1" />
  </ItemGroup>
  <ItemGroup Label="Analyzers (every project)">
    <GlobalPackageReference Include="Meziantou.Analyzer" Version="3.0.315" />
    <GlobalPackageReference Include="Microsoft.VisualStudio.Threading.Analyzers" Version="18.12.1" />
    <GlobalPackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="5.6.0" />
  </ItemGroup>
  <!-- Later phases (declared now so versions are decided once; unreferenced PackageVersions do not affect lock files):
       Markdig 1.4.2, DiffPlex 1.9.0, Microsoft.AspNetCore.OpenApi 10.0.13, Temporalio 1.20.0, OpenTelemetry 1.20.0, bunit 2.12.5, Verify.XunitV3 33.3.4 -->
</Project>
```
Do **not** add `PackageVersion` entries for `System.Text.Json` or other shared-framework packages (NU1510 → error).

### FS-4 `.editorconfig` highlights
Generate with `dotnet new editorconfig` (389 lines, SDK 10.0.402) then override these template defaults, which otherwise break D-04/D-01: `[VERIFIED: probe — template values quoted from the generated file; each override exercised]`

| Template line (verbatim) | Override | Why |
|---|---|---|
| `insert_final_newline = false` | `insert_final_newline = true` | `.gitattributes` `* text=auto eol=lf` + POSIX files; template value caused `error FINALNEWLINE` noise |
| `dotnet_separate_import_directive_groups = true` | `= false` | Otherwise every file needs blank lines between `System`/third-party/own usings (`error WHITESPACE: Insert '\n'`) |
| (absent) | `end_of_line = lf` | Match `.gitattributes` |
| (absent) | `dotnet_diagnostic.IDE0005.severity = warning` | Unused usings are **not** reported by build or format at the template's default; at `warning` both fail (TWAE → error IDE0005) |
| (absent) | `dotnet_diagnostic.CA1303.severity = none` | `latest-all` enables CA1303 (literal strings → resource table) — pure noise for an app |
| (absent) `[tests/**.cs]` | `dotnet_diagnostic.CA1707.severity = none`, `dotnet_diagnostic.CS1591.severity = none`, `dotnet_diagnostic.CA1062.severity = none` | Underscored test names; no XML docs on public test classes; no null guards in test helpers |
| (absent) | `dotnet_diagnostic.MA0048.severity = none` **or** keep one-type-per-file | Meziantou MA0048 "File name must match type name" fires under TWAE; D-03 lists ~30 contract types — decide the file convention explicitly (Claude's discretion: recommend one record per file and keep MA0048 on) |
| `csharp_style_namespace_declarations = file_scoped:suggestion` | `:warning` | Enforce file-scoped namespaces consistently |

Other `latest-all` rules that fired on realistic code and are *legitimate*: `CA1062` (null-guard public parameters — use `ArgumentNullException.ThrowIfNull`), `CA1716` (namespace segment that is a VB keyword, e.g. `Lib`; `OmpOS.*` names are safe). `[VERIFIED: probe]`

### FS-5 `.github/workflows/ci.yml` skeleton `[CITED: setup-dotnet / gitleaks READMEs; VERIFIED: command behaviours]`
```yaml
name: ci
on:
  push: { branches: [main] }
  pull_request:
permissions:
  contents: read
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
env:
  CI: "true"                       # activates RestoreLockedMode + ContinuousIntegrationBuild
  DOTNET_NOLOGO: "1"
  DOTNET_CLI_TELEMETRY_OPTOUT: "1"
  NUGET_PACKAGES: ${{ github.workspace }}/.nuget/packages
jobs:
  build-test:
    runs-on: ubuntu-latest        # Ubuntu 24.04: podman 4.9.3, python 3.12 preinstalled
    steps:
      - uses: actions/checkout@v7
        with: { fetch-depth: 0 }  # gitleaks needs history
      - uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json
          cache: true
          cache-dependency-path: '**/packages.lock.json'
      - run: dotnet restore OmpOS.Linux.slnf --locked-mode
      - run: dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror
      - run: dotnet test OmpOS.Linux.slnf -c Release --no-build --report-trx --coverlet --coverlet-output-format cobertura --results-directory TestResults
      - uses: actions/upload-artifact@v7
        if: always()
        with: { name: test-results, path: TestResults }
      - run: dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore
      - name: Vulnerable packages (list exits 0 even on findings — parse JSON)
        run: |
          dotnet list OmpOS.Linux.slnf package --vulnerable --include-transitive --format json > vuln.json
          python3 - <<'PY'
          import json,sys
          d=json.load(open("vuln.json"))
          hits=[p for pr in d["projects"] for fw in pr.get("frameworks",[]) for k in ("topLevelPackages","transitivePackages") for p in fw.get(k,[]) if p.get("vulnerabilities")]
          print(json.dumps(hits,indent=1)); sys.exit(1 if hits else 0)
          PY
      - uses: gitleaks/gitleaks-action@v3
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          GITLEAKS_LICENSE: ${{ secrets.GITLEAKS_LICENSE }}   # required for org repos
      - name: Validate compose (podman-compose needs the podman binary and the profile)
        run: |
          pipx install podman-compose==1.6.0
          podman-compose --profile workflows -f deploy/compose.yaml config --services | grep -qx temporal
          podman-compose --profile workflows -f deploy/compose.yaml config -q
  shells:
    runs-on: macos-15
    needs: build-test
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with: { global-json-file: global.json }
      - name: MAUI head (only when the project exists)
        if: hashFiles('apps/OmpOS.App/OmpOS.App.csproj') != ''
        run: |
          dotnet workload install maui
          dotnet restore apps/OmpOS.App/OmpOS.App.csproj --locked-mode
          dotnet build apps/OmpOS.App/OmpOS.App.csproj -c Release --no-restore -warnaserror
```
`hashFiles(...) != ''` is the standard GitHub expression for "file exists" `[ASSUMED]`. `cache-dependency-path` glob `[ASSUMED]` (README shows a single path; the action uses `@actions/glob` hashing). The vulnerability JSON shape (`projects[].frameworks[].topLevelPackages[].vulnerabilities[]`, `"version": 1`) `[VERIFIED: probe /tmp/ompos-vuln]`.

### FS-6 `deploy/compose.yaml` `[VERIFIED: podman-compose 1.6.0 config parses it (with a podman shim); CLI health command verified live]`
```yaml
# OmpOS local dependency stack (podman-compose 1.6.0+). The ONLY supported invocation:
#   podman-compose --profile workflows up
# No `version:` key (Compose Spec), no Docker-only extensions.
services:
  temporal:
    image: docker.io/temporalio/temporal:1.9.1
    container_name: ompos-temporal
    command: server start-dev --ip 0.0.0.0 --db-filename /home/temporal/temporal.db
    ports:
      - "7233:7233"   # gRPC frontend
      - "8233:8233"   # Web UI
    volumes:
      - temporal-data:/home/temporal   # image user `temporal` (uid 1000) owns this path
    healthcheck:
      test: ["CMD", "temporal", "operator", "cluster", "health", "--address", "127.0.0.1:7233"]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 10s
    profiles: ["workflows"]
volumes:
  temporal-data:
```
Use the fully-qualified `docker.io/` image name so rootless Podman does not prompt for a registry (`[ASSUMED]` — Podman's short-name resolution behaviour; harmless on Docker-compatible runtimes).

### FS-7 `bootstrap.sh` outline `[VERIFIED: every command exercised on the probe; dotnet-install flags verified]`
```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
SDK_VERSION=$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])' 2>/dev/null || sed -n 's/.*"version": *"\([^"]*\)".*/\1/p' global.json | head -1)
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
if [ ! -d "$DOTNET_ROOT/sdk/$SDK_VERSION" ]; then
  echo "Installing .NET SDK $SDK_VERSION into $DOTNET_ROOT"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --jsonfile global.json --install-dir "$DOTNET_ROOT"
fi
dotnet --version                      # must print 10.0.402 (global.json resolution)
dotnet restore OmpOS.Linux.slnf --locked-mode
dotnet build  OmpOS.Linux.slnf -c Release --no-restore -warnaserror
dotnet test   OmpOS.Linux.slnf -c Release --no-build --report-trx --results-directory TestResults
dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore
command -v podman-compose >/dev/null || echo "hint: podman-compose not found; install podman + 'pipx install podman-compose==1.6.0' to run deploy/compose.yaml"
echo "bootstrap: OK (SDK $(dotnet --version), locked restore, Release build, tests, format)"
```
`set -e` makes any non-zero step abort with that step's exit code (format verify → 2, test failure → non-zero, NU1004 → 1). Never calls `docker`.

### FS-8 Contracts: unknown-kind-safe polymorphism `[VERIFIED: probe src/Lib/UiBlock.cs — behaviour quoted in VF-5]`
```csharp
// OmpOS.Contracts/Ui/UiBlock.cs
[JsonConverter(typeof(UiBlockConverter))]
public abstract record UiBlock { public required string Id { get; init; } }
public sealed record MarkdownBlock : UiBlock { public required string Markdown { get; init; } }
public sealed record UnknownBlock : UiBlock { public string? OriginalKind { get; init; } public JsonElement Raw { get; init; } }

// OmpOS.Contracts/Ui/UiBlockConverter.cs
public sealed class UiBlockConverter : JsonConverter<UiBlock>
{
    private const string Discriminator = "kind";
    private static readonly Dictionary<string, Type> KindToType = new(StringComparer.Ordinal)
    { ["markdown"] = typeof(MarkdownBlock), /* …every variant… */ ["unknown"] = typeof(UnknownBlock) };
    private static readonly Dictionary<Type, string> TypeToKind = KindToType.ToDictionary(p => p.Value, p => p.Key);

    public override UiBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        string? kind = root.TryGetProperty(Discriminator, out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
        if (kind is not null && KindToType.TryGetValue(kind, out var type) && type != typeof(UnknownBlock))
            return (UiBlock?)root.Deserialize(options.GetTypeInfo(type));   // source-generated, AOT-safe
        var id = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() ?? "" : "";
        return new UnknownBlock { Id = id, OriginalKind = kind, Raw = root.Clone() };
    }

    public override void Write(Utf8JsonWriter writer, UiBlock value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer); ArgumentNullException.ThrowIfNull(value); ArgumentNullException.ThrowIfNull(options);
        if (value is UnknownBlock { Raw.ValueKind: JsonValueKind.Object } u) { u.Raw.WriteTo(writer); return; }
        var type = value.GetType();
        var node = JsonSerializer.SerializeToNode(value, options.GetTypeInfo(type)) as JsonObject
                   ?? throw new JsonException($"Cannot serialize {type}.");
        node.Insert(0, Discriminator, TypeToKind[type]);
        node.WriteTo(writer, options);
    }
}

// OmpOS.Contracts/Serialization/OmpOsJsonContext.cs
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, UseStringEnumConverter = true)]
[JsonSerializable(typeof(UiBlock))] [JsonSerializable(typeof(UiBlock[]))]
[JsonSerializable(typeof(MarkdownBlock))] [JsonSerializable(typeof(UnknownBlock))] /* every derived type + StreamEvent family + Session … */
public sealed partial class OmpOsJsonContext : JsonSerializerContext;
```
Apply the same converter shape to `StreamEvent`'s polymorphic payload (`UnknownEvent`). Enums (`SessionStatus`, `SandboxStatus`, …) serialize as strings via `UseStringEnumConverter = true` (generated converters; no reflection). Keep each derived record `sealed`; keep one record per file if MA0048 stays enabled.

### FS-9 Architecture tests: layer map as data + csproj graph + ArchUnitNET `[VERIFIED: ArchUnitNET API + invisibility finding; csproj parsing pattern ASSUMED but uses only System.Xml.Linq]`
```csharp
public static class LayerMap
{
    public static readonly IReadOnlyDictionary<string, int> Layers = new Dictionary<string, int>
    {
        ["OmpOS.Contracts"] = 0,
        ["OmpOS.Parser"] = 1, ["OmpOS.Harness.Abstractions"] = 1, ["OmpOS.Sandbox.Abstractions"] = 1,
        ["OmpOS.Harness.Acp"] = 2, ["OmpOS.Harness.Rpc"] = 2, ["OmpOS.Harness.InMemory"] = 2, ["OmpOS.Sessions"] = 2,
        ["OmpOS.GenUi"] = 2, ["OmpOS.Sandbox"] = 2, ["OmpOS.Workflows"] = 2, ["OmpOS.UI"] = 2,
        ["OmpOS.Api"] = 3, ["OmpOS.Workflows.Worker"] = 3,
        ["OmpOS.Web"] = 4, ["OmpOS.App"] = 4,
    };
    // Explicit per-project allow-list for same-layer edges (D-02): Sessions→GenUi, UI→GenUi.
    public static readonly IReadOnlySet<(string From, string To)> SameLayerAllowed = new HashSet<(string, string)> { ("OmpOS.Sessions","OmpOS.GenUi"), ("OmpOS.UI","OmpOS.GenUi") };
}

// Test 1 — project graph (catches edges ArchUnitNET cannot see): walk src/ services/ apps/ for *.csproj,
// read <ProjectReference Include> with System.Xml.Linq, resolve the target project name from the file name,
// assert Layers[to] < Layers[from] || SameLayerAllowed.Contains((from,to)); assert no non-test project references OmpOS.Api;
// assert OmpOS.Web never references OmpOS.Harness.Acp / OmpOS.Harness.Rpc. Locate the repo root by walking up from
// AppContext.BaseDirectory until OmpOS.slnx is found.

// Test 2 — ArchUnitNET type-level rules (meaningful once code exists; passes today on marker types):
private static readonly Architecture Arch = new ArchLoader().LoadAssemblies(/* every OmpOS.* assembly from the map via typeof(Marker).Assembly */).Build();
[Fact] public void LowerLayersDoNotDependOnHigherLayers()
{
    foreach (var (from, fromLayer) in LayerMap.Layers)
        foreach (var (to, toLayer) in LayerMap.Layers.Where(p => p.Value > fromLayer))
            Types().That().ResideInAssembly(from).Should().NotDependOnAny(Types().That().ResideInAssembly(to))
                .Because($"{from} (L{fromLayer}) must not depend on {to} (L{toLayer})").Check(Arch);
}
// Test 3 — negative proof: a rule that MUST fail (tests depend on Contracts) → HasNoViolations == false and Check throws FailedArchRuleException.
// Test 4 — negative proof for the csproj-graph checker: feed it a synthetic in-memory edge (Contracts → Api) and assert it reports a violation.
```
`ResideInAssembly(string)` matches the assembly name (docs show `ResideInAssembly("ExampleAssembly")`), so the loop can be driven from the string map. `[CITED: ArchUnitNET guide]`

### FS-10 Docs skeleton content sources (D-07) `[VERIFIED: source files exist in .planning/]`
- `README.md`: two paragraphs from `.planning/PROJECT.md`; prerequisites (bash, curl, git; podman + podman-compose optional); `./bootstrap.sh`; repo map table (D-01); "What is verified where" (Linux CI: everything in `OmpOS.Linux.slnf`; macOS CI: `apps/OmpOS.App` from Phase 4; never built locally on Linux).
- `CONTRIBUTING.md`: `Refs SPE-xxxxx` trailer; `dotnet format OmpOS.Linux.slnf`; relock with `dotnet restore OmpOS.Linux.slnf --force-evaluate` (note that `CI=true` in the environment enables locked mode locally); adding a project = `dotnet sln OmpOS.slnx add …`, append to `OmpOS.Linux.slnf`, add to `LayerMap`, add a `ProjectReference` from `OmpOS.ArchitectureTests`; TODO policy.
- `docs/open-questions.md`: seed the 14 items from SUMMARY.md "Gaps to Address" plus spec Q1–Q8, owner `ompos-maintainers`; add from this research: Temporal volume path/ownership under rootless Podman, busybox `nc -z` availability, MAUI workload on `macos-15`, gitleaks org licence key.
- `docs/runbooks/local-dev.md`: bootstrap; `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`; `podman-compose --profile workflows up`; troubleshooting NU1004 (`--force-evaluate`), NU1510 (remove framework package refs), `error IDE0005`/`WHITESPACE` (`dotnet format`), "podman-compose: failed to check if podman is installed" (install podman).
- `docs/runbooks/sandbox.md`: Podman 5.x / cgroups v2 / `systemctl --user start podman.socket` (from PITFALLS.md §14), Ubuntu 24.04 apt = 4.9.3.

## Architecture Patterns

### System Architecture Diagram (Phase 1 build/verification flow)
```
clone ──► bootstrap.sh ──► global.json ──► (missing?) dotnet-install.sh --jsonfile ──► $DOTNET_ROOT
                │
                ├─► dotnet restore OmpOS.Linux.slnf --locked-mode ──► Directory.Packages.props + packages.lock.json (NU1004 on drift; NU1903 on vuln)
                ├─► dotnet build -c Release -warnaserror ──► Directory.Build.props (nullable, latest-all, EnforceCodeStyleInBuild, BannedSymbols, GlobalPackageReference analyzers)
                ├─► dotnet test --no-build ──► MTP exes: OmpOS.Contracts.Tests (JSON round-trips, unknown kinds, Seq comparer, ContractVersion)
                │                               OmpOS.ArchitectureTests (csproj graph + ArchUnitNET + negative proofs)
                └─► dotnet format --verify-no-changes ──► .editorconfig (single rule source)
CI (ubuntu-latest) = same four steps + vuln JSON gate + gitleaks v3 + podman-compose --profile workflows config
CI (macos-15)      = guarded MAUI build (Phase 4)
```

### Recommended Project Structure
As locked in D-01. Inside `OmpOS.Contracts` (discretion): `Sessions/` (`Session`, `Message`, `SessionStatus`), `Intents/` (`IntentEnvelope`, `IntentKind`, `Diagnostic`), `Ui/` (`UiBlock` + one file per variant, `UiBlockConverter`), `Streaming/` (`StreamEvent`, payloads, `StreamEventSeqComparer`), `Sandboxes/`, `Workflows/`, `Errors/` (`OmpOSError`, `ErrorCode`), `Serialization/OmpOsJsonContext.cs`, `ContractVersion.cs`. Tests mirror folders.

### Pattern: placeholder project
```csharp
// src/OmpOS.Parser/AssemblyMarker.cs
namespace OmpOS.Parser;
/// <summary>Marker type for the OmpOS.Parser assembly (implementation arrives in Phase 2).</summary>
public static class AssemblyMarker;
```
No `TODO`; the marker is what makes ArchUnitNET's object set non-empty (VF-3). `[VERIFIED: probe src/Empty]`

### Anti-Patterns to Avoid
- **`dotnet new xunit`** — scaffolds xunit 2.9.3/VSTest; incompatible with D-04. `[VERIFIED]`
- **Project-wide `<NoWarn>`** for CA/IDE/MA categories — use scoped `.editorconfig` sections (`[tests/**.cs]`). (PITFALLS 11)
- **`[JsonPolymorphic]` alone for `UiBlock`/`StreamEvent`** — unknown kinds throw (VF-5).
- **Trusting ArchUnitNET alone with empty placeholders** — unused `ProjectReference`s are invisible (VF-3).
- **`podman-compose config` without `--profile workflows`** — validates `services: {}` (VF-7/P-8).
- **`dotnet list package --vulnerable` as a bare step** — exit 0 on findings (P-7).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Layer rules on types | Reflection walker over `GetReferencedAssemblies()` | ArchUnitNET 0.13.4 (`NotDependOnAny`, `BeFreeOfCycles`) | Handles generics, attributes, member-level deps; verified failure messages |
| Layer rules on project edges | MSBuild evaluation via `Microsoft.Build` | 30-line `System.Xml.Linq` reader over `ProjectReference Include` | No MSBuild locator/assembly-loading pain; paths are literal in this repo |
| SDK install | Custom tarball download | `dotnet-install.sh --jsonfile global.json --install-dir` | Handles arch/OS/already-installed |
| Coverage/TRX | Custom loggers | `coverlet.MTP` + `Microsoft.Testing.Extensions.TrxReport` | Verified artifact output under MTP |
| Formatting | CSharpier or custom | `dotnet format` + `.editorconfig` | Locked (D-04); verified agreement with build |
| Banned APIs | Roslyn analyzer | `BannedSymbols.txt` + BannedApiAnalyzers | Verified entries; per-project scoping via conditional `AdditionalFiles` |
| Secret scan | grep | `gitleaks/gitleaks-action@v3` | Locked; v2 no longer runs |

## Runtime State Inventory

Greenfield phase — no rename/refactor. **Nothing to migrate** in stored data, live service config, OS-registered state, secrets, or build artifacts (verified: repo contains only `.planning/` and `AGENTS.md`). One environment fact doubles as state: the shell exports **`CI=true`** on this dev box (`env | grep ^CI=` → `CI=true`; origin not in `/etc/environment` or `~/.bashrc`/`~/.profile`), so `'$(CI)' == 'true'` conditions are live during local execution. `[VERIFIED]`

## Common Pitfalls

### P-1 `latest-all` enables rules that fail realistic code under TWAE
**What goes wrong:** first build fails with CA1062 (null guards), CA1303 (string literals), CA1707 (underscored test names), CA1716 (keyword namespace), MA0048 (file name ≠ type name), CS1591 (missing XML docs on public test types). **Avoid:** §FS-4 overrides; `ArgumentNullException.ThrowIfNull` in public API; one type per file in Contracts. **Warning sign:** the executor reaches for `<NoWarn>`. `[VERIFIED: probe]`

### P-2 Unused usings pass silently
IDE0005 is not reported by build or `dotnet format --verify-no-changes` unless `dotnet_diagnostic.IDE0005.severity = warning` **and** `GenerateDocumentationFile=true`. Set both. `[VERIFIED: probe]`

### P-3 Template `.editorconfig` defaults fight `.gitattributes`
`insert_final_newline = false` and `dotnet_separate_import_directive_groups = true` are the SDK template defaults; both produce `dotnet format` errors on normally-written files. Override (§FS-4). `[VERIFIED: probe]`

### P-4 Framework packages referenced explicitly → NU1510 error
On net10.0, `PackageReference` to `System.Text.Json` (and other pruned framework packages) is a warning promoted to error by TWAE. Keep Contracts package-free. `[VERIFIED: probe]`

### P-5 `CI=true` is set locally → locked restore everywhere
Every dependency edit needs `dotnet restore OmpOS.Linux.slnf --force-evaluate` before `--locked-mode` passes (NU1004 otherwise). Document in CONTRIBUTING; the executor must regenerate lock files after adding each project. `[VERIFIED: probe]`

### P-6 `dotnet format` exit code
`--verify-no-changes` exits **2** on drift (not 1). `set -e` handles it; scripts that test `== 1` do not. `[VERIFIED: probe]`

### P-7 `dotnet list package --vulnerable` exits 0 on findings
Parse `--format json` (§FS-5) or depend on restore's NU1903-as-error. `[VERIFIED: probe /tmp/ompos-vuln]`

### P-8 `podman-compose config` is weak and needs `podman`
Without the `podman` binary: `CRITICAL: It seems that you either do not have podman installed` (exit 1) — so it cannot run on this dev box; ubuntu-24.04 runners have Podman 4.9.3. Without `--profile workflows` the output is `services: {}` (exit 0). A semantically invalid service (`ports: notalist`) still exits 0; only YAML syntax errors exit 1. Gate CI on `config --services | grep -qx temporal` in addition to `config -q`. `[VERIFIED: /tmp/ompos-compose with podman-compose 1.6.0]`

### P-9 Unknown JSON kinds throw with `[JsonPolymorphic]`
See VF-5; the converter in §FS-8 is the fix. The Contracts test "unknown `kind` deserializes to `UnknownBlock` and never throws" must exist (D-01 specifics) and would fail against the attribute-only design. `[VERIFIED: probe]`

### P-10 Architecture test that is green but blind
Marker-only projects + unused references pass ArchUnitNET; empty object sets fail it. Use the csproj-graph test + negative proofs (§FS-9). `[VERIFIED: probe]`

### P-11 Stale action majors
`gitleaks/gitleaks-action@v2` (Node 20) is removed from hosted runners since 2026-09-16; use `@v3`; `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`. `[CITED: gitleaks README; VERIFIED: releases API]`

### P-12 Temporal volume ownership
`/data` does not exist in the image and the process runs as uid 1000 → prefer `/home/temporal` (Temporal's own compose) or add `:U`. `[CITED: Dockerfile; consequence ASSUMED]`

### P-13 `temporalio/temporal` has no shell-based healthcheck guarantee
Use the CLI itself (`temporal operator cluster health --address 127.0.0.1:7233` → `SERVING`, exit 0/1). `[VERIFIED: binary 1.9.1]`

## Project Constraints (from AGENTS.md)

- C# everywhere; one UI codebase (RCL shared by WASM + MAUI). Phase 1: all projects `net10.0`, UI as Razor Class Library placeholder.
- **podman/podman-compose only; Docker tooling must not be emitted** → `bootstrap.sh` never calls `docker`; `compose.yaml` has no Docker-only extensions; CI uses `pipx install podman-compose`.
- **No invented vendor facts** → every version/flag in this document is tagged; `[ASSUMED]` items go to `docs/open-questions.md` with owner `ompos-maintainers`.
- **Strictness**: nullable, warnings-as-errors, analyzers, deterministic, CPM + committed lock files, formatter in CI, no TODO without owner + open-question link → §FS-2/3/4; placeholders contain no TODOs.
- **Secrets**: nothing committed; `.env.example` documents variables; gitleaks in CI.
- **Hosting split / dev-box limits**: MAUI excluded from `OmpOS.Linux.slnf`, built on macOS CI; README's "verified where" table.
- **GSD workflow enforcement**: repo edits go through `/gsd-execute-phase`.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK 10.0.402 | everything | ✓ (`/root/.dotnet`, needs `DOTNET_ROOT`/`PATH` export) | 10.0.402 | `dotnet-install.sh --jsonfile global.json` |
| System dotnet | — | ✓ but wrong version | 8.0.131 | ignore |
| nuget.org | restore | ✓ | — | — |
| git | repo | ✓ | — | — |
| python3 (+venv) | CI vuln JSON parse, local podman-compose | ✓ | 3.x | `jq` |
| podman | `podman-compose config` locally | ✗ | — | validate in CI only (ubuntu-24.04 ships 4.9.3); local YAML sanity via `python3 -c yaml` |
| podman-compose | compose validation | ✗ locally (installable: `pip install podman-compose==1.6.0` works) | 1.6.0 | CI |
| Docker | — | present but **banned** | — | never used |
| temporal CLI binary | healthcheck verification | ✓ (downloaded to `/tmp/tcli`, not a repo dependency) | 1.9.1 | — |
| macOS runner / Xcode / `maui` workload | `shells` job | ✗ locally | — | guarded job; Phase 4 |
| gitleaks licence | org repos in CI | unknown | — | `GITLEAKS_LICENSE` secret; personal repos need none |

**Missing dependencies with no fallback:** none for Phase 1 deliverables (podman absence only prevents *local* compose validation; CI covers it).
**Missing dependencies with fallback:** podman/podman-compose (CI), macOS (CI, Phase 4).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.2 on Microsoft.Testing.Platform 2.5.x (`global.json` `test.runner`), Shouldly 4.3.0, TngTech.ArchUnitNET.xUnitV3 0.13.4, coverlet.MTP 10.1.0, TrxReport 2.5.1 |
| Config file | `global.json` (runner) + `Directory.Build.props` test PropertyGroup — none exists yet → Wave 0 |
| Quick run command | `dotnet test --project tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj -c Release` (or `dotnet run --project … -- --filter-class 'OmpOS.Contracts.Tests.UiBlockJsonTests'`) |
| Full suite command | `./bootstrap.sh` (= locked restore → TWAE build → `dotnet test OmpOS.Linux.slnf -c Release --no-build` → `dotnet format --verify-no-changes`) |

Every command and expected output below was exercised on the probe solution with the same packages. `[VERIFIED: probe]`

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | Expected output | File Exists? |
|--------|----------|-----------|-------------------|-----------------|-------------|
| FOUND-01 | Clean clone + `./bootstrap.sh` → exit 0; any failing step → non-zero | smoke (script) | `git clone <repo> /tmp/clean && cd /tmp/clean && ./bootstrap.sh; echo $?` | last line `bootstrap: OK …`, `0`. Negative: `dotnet format OmpOS.Linux.slnf --verify-no-changes` after inserting a stray blank line → exit `2`; `bootstrap.sh` exit `2` | ❌ Wave 0 (`bootstrap.sh`) |
| FOUND-01 | SDK resolution from `global.json` | smoke | `dotnet --version` (from repo root) | `10.0.402` | ❌ Wave 0 (`global.json`) |
| FOUND-02 | Props inherited: TWAE, nullable, `latest-all`, deterministic, CIB | build + negative | `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` → `Build succeeded.`; negative: add `string s = null;` to a placeholder → `error CS8600` (nullable) and a `Console.WriteLine("x")` → `error CA1303` only if CA1303 is left on (we disable it; use `Task.Wait()` → `error RS0030`/`VSTHRD002` instead) | exit 0 / exit 1 with the quoted codes | ❌ Wave 0 (`Directory.Build.props`) |
| FOUND-02 | `ContinuousIntegrationBuild` under `CI=true` | build inspection | `CI=true dotnet build … -getProperty:ContinuousIntegrationBuild` (MSBuild `-getProperty`) | `true` `[ASSUMED: -getProperty flag]` | ❌ |
| FOUND-03 | Lock files for every project; locked restore | restore + negative | `dotnet restore OmpOS.Linux.slnf --locked-mode` → exit 0; `find src services tests -name packages.lock.json | wc -l` == project count; negative: bump a `PackageVersion` → `error NU1004: … lock file is inconsistent …` exit 1 | as quoted | ❌ Wave 0 (`Directory.Packages.props`) |
| FOUND-04 | `.editorconfig` single source; verify passes | format | `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` | exit 0, no `error WHITESPACE/FINALNEWLINE/IDE*` lines | ❌ Wave 0 (`.editorconfig`) |
| FOUND-05 | CI workflow runs all gates; macOS job guarded | CI run + local lint | push → both jobs green; locally `python3 -c "import yaml;yaml.safe_load(open('.github/workflows/ci.yml'))"`; vuln gate unit-tested by running the inline Python on a fixture JSON with a vulnerability → exit 1 | job logs show `Test run summary: Passed!`, `No vulnerable packages`, gitleaks `No leaks found`, `temporal` from `config --services` | ❌ Wave 0 (`ci.yml`) |
| FOUND-06 | Contracts compile with STJ source-gen; reflection disabled; unknown kinds never throw; Seq comparer; ContractVersion | unit (`OmpOS.Contracts.Tests`) | `dotnet test --project tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj -c Release` | `Test run summary: Passed!`; tests: round-trip every `UiBlock`/`StreamEvent` variant via `OmpOsJsonContext.Default.*`; `{"kind":"zzz",…}` → `UnknownBlock`/`UnknownEvent` with `OriginalKind=="zzz"` and byte-identical re-serialization; missing `kind` → Unknown; `JsonSerializer.Serialize(new { a = 1 })` throws `InvalidOperationException` ("Reflection-based serialization has been disabled"); `StreamEventSeqComparer` orders by `Seq`; `ContractVersion.Current` matches `^\d+\.\d+\.\d+$` and header name is `X-OmpOS-Contract-Version` | ❌ Wave 0 |
| FOUND-07 | Upward layer reference or non-test → `OmpOS.Api` fails the build | unit (`OmpOS.ArchitectureTests`) + negative proofs | `dotnet test --project tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj -c Release` | Passed; includes `CsprojGraph_RejectsSyntheticUpwardEdge` (fails on in-memory `Contracts→Api` edge) and `ArchUnit_NegativeRuleFires` (`FailedArchRuleException`). Manual negative: add `<ProjectReference Include="../OmpOS.Api/OmpOS.Api.csproj"/>` to `OmpOS.Parser` → csproj-graph test fails with the edge named | ❌ Wave 0 |
| FOUND-08 | `compose.yaml` validates; no Docker-only tooling | CI step + grep | CI: `podman-compose --profile workflows -f deploy/compose.yaml config -q` exit 0 and `config --services` prints `temporal`; repo-wide `grep -rniE "docker(-compose)?\b" --include=*.sh --include=*.yaml --include=*.yml --include=*.md .` returns only the `docker.io/` image prefix and prose stating Docker is unsupported (assert in an ArchitectureTests `NoDockerTooling` test over `bootstrap.sh`, `ci.yml`, `compose.yaml`) | as stated | ❌ Wave 0 |
| FOUND-09 | Docs exist with content | unit (file presence + non-trivial size) | `OmpOS.ArchitectureTests` `DocsSkeletonExists`: each D-07 path exists and > 400 bytes; `docs/open-questions.md` contains the header row `ID | Question | Why it matters | Default taken | Owner | Status` and ≥ 20 rows | Passed | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build OmpOS.Linux.slnf -c Release -warnaserror && dotnet test --project tests/<touched>.Tests/… -c Release --no-build` (≈ 5–15 s on the probe)
- **Per wave merge:** `./bootstrap.sh`
- **Phase gate:** `./bootstrap.sh` from a fresh `git clone` in `/tmp` + CI green on both jobs before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `BannedSymbols.txt` (+ `BannedSymbols.NoProcess.txt`), `OmpOS.slnx`, `OmpOS.Linux.slnf` — framework config
- [ ] `tests/OmpOS.Contracts.Tests/OmpOS.Contracts.Tests.csproj` + `UiBlockJsonTests.cs`, `StreamEventJsonTests.cs`, `ReflectionDisabledTests.cs`, `SeqComparerTests.cs`, `ContractVersionTests.cs` — FOUND-06
- [ ] `tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj` + `LayerMap.cs`, `ProjectGraphTests.cs`, `ArchUnitLayerTests.cs`, `RepoHygieneTests.cs` (no Docker tooling, docs skeleton) — FOUND-07/08/09
- [ ] `bootstrap.sh`, `.github/workflows/ci.yml`, `deploy/compose.yaml` — FOUND-01/05/08
- [ ] Framework install: none (restore pulls xunit.v3/Shouldly/ArchUnitNET/coverlet.MTP/TrxReport from `Directory.Packages.props`)

## Security Domain

### Applicable ASVS Categories (level 1)
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (Phase 2+ single-dev principal) | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | yes (contract deserialization) | STJ source-gen with `required` members; unknown kinds captured, never executed; `JsonDocument` bounded by STJ `MaxDepth` default 64 `[ASSUMED]` |
| V6 Cryptography | no | — |
| V10 Malicious code / supply chain | yes | NuGet audit (`NuGetAuditMode=all`, `NuGetAuditLevel=low` → NU1903 fails restore), lock files + `--locked-mode`, CPM transitive pinning, `GlobalPackageReference` analyzers, pinned action majors, gitleaks v3 |
| V14 Configuration | yes | `.env.example` only; secrets never committed; CI `permissions: contents: read`; `GITLEAKS_LICENSE`/`GITHUB_TOKEN` via secrets |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Dependency confusion / vulnerable transitive | Tampering | lock files + audit at restore + JSON-parsed vuln gate (P-7) |
| Secret committed to git | Information disclosure | gitleaks-action@v3 with `fetch-depth: 0`; `.gitignore` includes `.env` |
| Reflection-based deserialization of arbitrary types | Tampering | `JsonSerializerIsReflectionEnabledByDefault=false` (verified to throw) |
| Unknown JSON payload crashing a client | DoS | `UnknownBlock`/`UnknownEvent` converter (verified) |
| Process spawning from UI/WASM code | Elevation | `BannedSymbols.NoProcess.txt` outside `OmpOS.Harness.*`/`OmpOS.Sandbox` + architecture test (Web ↛ Harness.Acp/Rpc) |
| Docker socket exposure | Elevation | Docker banned; podman rootless only |

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| xunit 2.x + Microsoft.NET.Test.Sdk + VSTest | xunit.v3 4.x exe projects on MTP v2, `global.json` `test.runner` | xunit.v3 4.0.0 (2026-08), SDK 10 | `dotnet new xunit` template is obsolete for this repo; `coverlet.collector` inert |
| `.sln` text format | `.slnx` XML + `.slnf` filters | SDK 9/10 | `dotnet new sln --format slnx`, `dotnet new slnf --parent-solution` |
| `[JsonPolymorphic]` + `FallBackToNearestAncestor` for "unknown kinds" | Custom converter mapping unknown discriminators to a typed fallback | STJ 7+ semantics (unchanged in 10) | Attribute never handled read-side unknowns |
| `gitleaks-action@v2`, `checkout@v4`, `setup-dotnet@v4/5` | `@v3`, `@v7`, `@v6` (Node 24) | 2026-05 → 2026-09 Node 20 removal | v2 fails on hosted runners |
| `temporalio/auto-setup` | `temporalio/temporal:1.9.1 server start-dev` | 2025–2026 | Single-container dev server with SQLite file |

**Deprecated/outdated:** `coverlet.collector` under MTP; `xunit.runner.visualstudio` for MTP-only runs; `version:` key in compose files; `macos-14` runner label.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Busybox `nc -z localhost 7233` works inside `temporalio/temporal` (alpine) | VF-7 | Fallback healthcheck fails — primary CLI healthcheck is verified, so low |
| A2 | A named volume at a non-existent `/data` is root-owned under rootless Podman and uid 1000 cannot write | VF-7 / P-12 | If wrong, D-05's `/data/temporal.db` works as written; recommendation `/home/temporal` is safe either way |
| A3 | `cache-dependency-path: '**/packages.lock.json'` glob is honoured by setup-dotnet | FS-5 | Cache miss / action error — fallback: list the files explicitly or drop `cache: true` |
| A4 | `hashFiles('apps/OmpOS.App/OmpOS.App.csproj') != ''` is a valid "file exists" guard | FS-5 | macOS job fails until Phase 4 — fallback: `if: ${{ github.event_name == 'workflow_dispatch' }}` or a shell test |
| A5 | `dotnet workload install maui` succeeds on `macos-15` with default Xcode 16.4 for MAUI 10 | VF-6 | macOS job red in Phase 4; Phase 1 only needs the skeleton |
| A6 | Fully-qualified `docker.io/temporalio/temporal` avoids rootless Podman short-name prompts | FS-6 | Harmless if unnecessary |
| A7 | MSBuild `-getProperty:ContinuousIntegrationBuild` is available for the FOUND-02 CIB probe | Validation | Use `-v:diag | grep` instead |
| A8 | STJ `JsonException` message for a missing `required` member | VF-5 | Assert exception type only, not message |
| A9 | STJ default `MaxDepth` 64 bounds `JsonDocument.ParseValue` in the converter | Security | Add explicit `JsonSerializerOptions.MaxDepth` if needed |
| A10 | Versions in STACK.md for packages not re-verified today (Markdig, DiffPlex, OpenApi, Temporalio, OpenTelemetry, bunit, Verify.XunitV3) | Standard Stack | Only matters when a later phase references them; executor restores and adjusts per D-04 |

## Open Questions

1. **Does the Linear/GitHub org need a gitleaks licence?**
   - What we know: README says a free key is required for organization-owned repos, not personal ones.
   - What's unclear: whether `swcstudio`'s GitHub repo is org-owned.
   - Recommendation: add `GITLEAKS_LICENSE` as a repo secret if org-owned; the step fails fast with a clear message otherwise. Log in `docs/open-questions.md`.
2. **Keep MA0048 (file name = type name) on, or disable?**
   - What we know: it fires under TWAE; Contracts has ~30 types.
   - Recommendation (discretion): keep it on and write one record per file — it is the convention the rest of the analyzer set assumes.
3. **`/data` vs `/home/temporal` volume path** — see A2; recommend `/home/temporal/temporal.db` and record the deviation from D-05's literal path in ADR-0001/runbook.
4. **Where does `CI=true` come from on the dev box?** Not in shell rc files; probably the agent harness. Executors must assume locked mode locally and relock with `--force-evaluate`.

## Sources

### Primary (HIGH confidence — executed this session)
- `/tmp/ompos-probe` — SDK 10.0.402: `.slnx`/`.slnf`, `global.json` MTP mode, `Directory.Build.props`/`Directory.Packages.props`, lock files + NU1004, TWAE under `latest-all`, `dotnet format` exit codes, `dotnet test` TRX/coverlet, STJ polymorphism behaviours, custom converter, BannedSymbols RS0030, ArchUnitNET API + invisibility/empty-set findings, IDE0005/CS1591 behaviour.
- `/tmp/ompos-vuln` — NU1903/NU1510 under TWAE; `dotnet list package --vulnerable` exit code and `--format json` shape.
- `/tmp/ompos-compose` + `/tmp/pcvenv` — podman-compose 1.6.0 `config` behaviour (profile, missing podman, weak validation, `--services`, `-q`).
- `/tmp/tcli` — `temporal` CLI 1.9.1 (`Server 1.32.0, UI 2.54.1`): `server start-dev --ip 0.0.0.0 --db-filename`, `operator cluster health` exit codes, ports.
- `/tmp/dotnet-install.sh` — `--jsonfile`, `--install-dir`, already-installed `exit 0`, dry run for 10.0.402.
- nuget.org v3 flat-container/search/registration APIs; PyPI JSON; Docker Hub tags API; GitHub releases API (action tags) — 2026-10-20.

### Secondary (MEDIUM confidence — official docs read this session)
- https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test — MTP mode, `--solution/--project/--test-modules`, `--report-trx` requirement.
- https://xunit.net/docs/getting-started/v3/microsoft-testing-platform — `UseMicrosoftTestingPlatformRunner`, `TestingPlatformDotnetTestSupport`, MTP v2 default.
- https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism — `FallBackToNearestAncestor`, `IgnoreUnrecognizedTypeDiscriminators`.
- https://archunitnet.readthedocs.io/en/latest/guide/ — loader, `ResideInAssembly`, `NotDependOnAny`, cycles.
- https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md — entry syntax, `BannedSymbols.*.txt`.
- https://github.com/actions/setup-dotnet/blob/main/README.md — `global-json-file`, rollForward, `cache`, `cache-dependency-path`, NU1403 note.
- https://github.com/gitleaks/gitleaks-action/blob/master/README.md — v3 migration, Node 20 removal dates, licence requirement.
- https://github.com/actions/runner-images/blob/main/README.md (+ macos-15-arm64 / Ubuntu2404 readmes) — labels, preinstalled Podman/.NET/Xcode.
- https://docs.temporal.io/cli/server, https://docs.temporal.io/cli/command-reference/operator — `start-dev` flags, `operator cluster health`.
- https://github.com/temporalio/cli/blob/main/Dockerfile, https://github.com/temporalio/samples-server/blob/main/compose/docker-compose-dev.yml — image contents, reference compose.
- `.planning/research/STACK.md`, `PITFALLS.md`, `SUMMARY.md` — prior verified stack and pitfalls.

### Tertiary (LOW confidence)
- Assumptions A1–A10 above.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every Phase-1 package restored and exercised on 10.0.402; versions read from nuget.org today.
- Architecture (file shapes, converter, layer tests): HIGH — the exact props/converter/rule code ran; the csproj-graph reader is a straightforward addition.
- Pitfalls: HIGH for toolchain behaviours (all reproduced); MEDIUM for CI-only items (podman on runner, macOS, gitleaks licence) which could not be run here.

**Research date:** 2026-10-20
**Valid until:** 2026-11-20 for SDK/package facts (patch cadence); re-check action majors and `temporalio/temporal` tag before Phase 3.

---
*Tracking: ultrathink nodes n2 — SPE-17752 (https://linear.app/swcstudio/issue/SPE-17752) and n6 — SPE-17754 (https://linear.app/swcstudio/issue/SPE-17754).*
