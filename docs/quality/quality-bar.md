---
description: "Record of the 2026-10-22 quality-bar audit: gate checks and findings at that commit."
---

# Quality bar (audit 2026-10-22)

Status: Record (historical audit of 2026-10-22; superseded by the parity audit of 2026-10-28)

Last verified: 2026-10-28 (the status only; the body is a dated record and was not re-verified)

Applies to: v0.15.0

Repository HEAD `d965ba6`, working tree as found. Other work was uncommitted in the tree during this audit (`git status` listed tracked edits to `Directory.Packages.props`, `apps/OmpOS.Native/*`, `src/OmpOS.Harness.Terminal/*` and others, plus untracked peer work). This audit changed only: `docs/open-questions.md` (132 rows appended, 0 deleted; `git diff --numstat` gives `132 0`), `docs/quality/`, `scripts/web-smoke/` and `docs/runbooks/evidence/quality-audit-2026-10-22/`.

No build, test run or `dotnet format` was run as a gate by this audit. The only `dotnet` commands were the `dotnet publish` of `apps/OmpOS.Web` and `services/OmpOS.Api` for WEB-01, each under `timeout 300` with `--no-restore`.

## 1. Gate checks

| # | Check | Yes or no | Proof (file:line) | Re-check command |
|---|---|---|---|---|
| 1 | Analyzers on; warnings as errors | **yes** | `Directory.Build.props:7` `TreatWarningsAsErrors` true; `:8` `EnableNETAnalyzers` true; `:9` `AnalysisLevel` latest-all. Gate build with `-warnaserror`: `bootstrap.sh:39`, `.github/workflows/ci.yml:42`. Caveat: 132 suppressions and 4 `.editorconfig` severity overrides lower what the analyzers report (section 2). | `grep -n "TreatWarningsAsErrors\|AnalysisLevel" Directory.Build.props` (static). Full check: `dotnet build OmpOS.Linux.slnf -c Release --no-restore -warnaserror` (not run here). |
| 2 | Formatter verified in bootstrap | **yes** | `bootstrap.sh:52` `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore`; also `.github/workflows/ci.yml:64`. | `dotnet format OmpOS.Linux.slnf --verify-no-changes --no-restore` (not run here). |
| 3 | Architecture tests run in the gate | **yes** | `OmpOS.Linux.slnf:36` includes `tests/OmpOS.ArchitectureTests`. `bootstrap.sh:51` and `ci.yml:53` run `dotnet test OmpOS.Linux.slnf` (bootstrap filters only `Category=Live`). Repo-hygiene checks: `tests/OmpOS.ArchitectureTests/RepoHygieneTests.cs:75-112` (open-questions shape, ID uniqueness) and `:114-123` (TODO markers link to an owned open question). | `dotnet test tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj -c Release --no-restore` (not run here). |
| 4 | Secret scan in CI | **yes (CI and bootstrap; corrected 2026-10-27)** | `ci.yml:22-23` pins gitleaks 8.30.1 and its SHA-256; `ci.yml:83-88` downloads it, checks the digest (`:86`) and runs `gitleaks git --redact --exit-code 1`. Since 2026-10-26 `bootstrap.sh` also runs it through `scripts/ci/secret-scan.sh`, shared with CI (`GateParityTests`). Organisation-repo licence is a separate open item (OQ-18). | Gitleaks 8.30.1 as CI installs it: `gitleaks git --redact --exit-code 1 --log-level warn .` (not run here). |
| 5 | Banned symbols | **yes** | `Directory.Build.props:38` adds `BannedSymbols.txt` (Newtonsoft; `Task.Result`; `Task.Wait()`; `Process.Start(string)`), `:39` adds `BannedSymbols.NoProcess.txt` (`System.Diagnostics.Process`) to every project except `OmpOS.Harness.*`, `OmpOS.Sandbox` and `*Tests`. Analyzer wiring: `Directory.Packages.props:69` `GlobalPackageReference` `Microsoft.CodeAnalysis.BannedApiAnalyzers` 5.6.0, present in every `packages.lock.json` (for example `src/OmpOS.Parser/packages.lock.json:11`). | `grep -n "BannedApiAnalyzers" Directory.Packages.props` (static). Full check: a build as in row 1; a banned use fails with the analyzer's banned-API diagnostic (RS0030). |
| 6 | Locked restore | **yes (gate scope)** | `bootstrap.sh:38` and `ci.yml:41` run `dotnet restore OmpOS.Linux.slnf --locked-mode`. `Directory.Build.props:16` `RestorePackagesWithLockFile` true; `:25` `RestoreLockedMode` true when `CI=true`. All 54 projects in `OmpOS.Linux.slnf` have a `packages.lock.json`. **Exceptions:** `apps/OmpOS.App/OmpOS.App.csproj` and `tools/gweb/named/OmpOS.Gweb.Named.csproj` have none. Neither is in the filter. `apps/OmpOS.App` is tracked (13 files). `ci.yml:145` runs the `shells` job whenever that csproj exists, and `ci.yml:148` restores it with `--locked-mode`. [INFERENCE, not run: that restore should fail for want of a lock file.] | `dotnet restore OmpOS.Linux.slnf --locked-mode` (not run here). Static: `find . -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*' -exec sh -c 'test -f "$(dirname "$1")/packages.lock.json" \|\| echo "$1"' _ {} \;` lists the two exceptions. |

## 2. Suppressions and skip markers

Decisions recorded by Phase 25.9 on 2026-10-26. The 2026-10-22 audit inventory is `docs/runbooks/evidence/quality-audit-2026-10-22/suppressions-full.tsv` (file, line, kind, rule, justification in source, source line; method: exhaustive line scan, counts in `suppressions-summary.txt`). Line numbers in that evidence are as of 2026-10-22. The SUP rows of `docs/open-questions.md` are keyed by file and rule, never by line, so edits above a suppression do not invalidate its row.

### Counts and scope

Scope of the suppression counts: first-party `.cs`, `.razor`, `.axaml` files and `.editorconfig`, under the repository root, excluding `plugins/`, `bin`, `obj`, `TestResults`, `.planning`, `docs`, `TestData`, `output`, `node_modules` and `.nuget`. This is also the scope `tests/OmpOS.ArchitectureTests/SuppressionScanner.cs` scans, and `SuppressionLedgerTests` fails the gate when a suppression in it has no kept row or a kept row has no suppression. Scope of the skip counts: the same directories, `.cs` and `.razor` files; the markers are `Assert.Skip`, `Assert.SkipUnless`, `[Fact(Skip=...)]` and the `Category=Live` trait. Skip counts are not gate-enforced.

| Count | At audit 2026-10-22 | Kept at 2026-10-26 | Scope and note |
|---|---|---|---|
| `#pragma warning disable` | 18 | **17** | Gate-enforced. `CA1031` 12, `VSTHRD003` 4, `CA1515` 1. One `CA1031` pragma was fixed (SUP-050). |
| `[SuppressMessage]` | 81 | **64** | Gate-enforced. `CA1031` 26, `CA2000` 19, `CA1515` 9, `CA5394` 5, `CA2100` 2, `CA1711` 2, `CA1034` 1. Source held 91 on 2026-10-26 before the fixes: the 81 recorded plus 10 added after the audit. 27 were fixed (19 recorded rows and 8 added after the audit without a row); the other 2 added sites (`CA2000`) are kept. |
| `.editorconfig` severity `none` | 4 | **4** | Gate-enforced. `CA1303` for all `*.{cs,vb}`; `CA1707`, `CS1591`, `CA1062` for `tests/**.cs`. All four kept with a recorded justification (SUP-001 to SUP-004); `.editorconfig` is unchanged. |
| Skip markers (`Assert.Skip`, `Assert.SkipUnless`, `Fact(Skip=...)`) | 28 | **49** | Not gate-enforced. 23 `Assert.Skip`, 23 `Assert.SkipUnless`, 3 `Fact(Skip=...)`. 12 markers of the 51 skip-type lines are in `tests/OmpOS.Harness.AgentLoop.Tests` and `tests/OmpOS.Native.Tests`, which other work was editing on 2026-10-26; re-count them before relying on the total. |
| `Category=Live` trait | 1 | **2** | Not gate-enforced. `RpcLiveCaptureTests` and the new `LiveOmpHandshakeTests`; `bootstrap.sh` excludes the trait from the gate run. |
| `NoWarn`, `#nullable disable`, downgraded warnings | 0 | **0** | Same scope plus `.csproj`, `.props` and `.targets`; none found on 2026-10-26. |
| **Total** | **132** | **136** | 136 = 17 + 64 + 4 + 49 + 2. |

Reconciliation of the SUP rows: 132 rows at the audit (SUP-001 to SUP-132), plus 24 rows added by Phase 25.9 for sites that appeared after 2026-10-22 and were kept (SUP-133 to SUP-156: 2 `CA2000` suppressions and 22 skip-type markers) gives 156 rows. 20 rows are fixed, so 136 stay kept. Eight more suppressions were added after 2026-10-22 without a row (class-level `CA2007` and `MA0004` in `AcpLaunchPlumbingTests`, `AcpMcpServerSessionTests`, `AcpPumpFaultLoggingTests`, `AcpPermissionResolverTests` and `RemoteStreamDropTests`); they were fixed in the same pass, so they have no row.

Added 2026-10-27: eight kept rows, SUP-157 to SUP-164 (`VSTHRD003` in `apps/OmpOS.Native/Agents/AgentHead.cs` and `tests/OmpOS.Harness.OmpLauncher.Tests/Live/LiveTerminalSink.cs`; `CA2000` in five ACP and EngineHost test files; `CA1859` in `AcpSessionOptionTests.cs`), came after this table from the Phase 48 and 49 work. They are in `docs/open-questions.md` (164 SUP rows: 20 fixed, 144 kept) and are not rows of the table or part of the counts above.

Reconciled 2026-10-28 (milestone v0.15.0, final records): the gate-enforced suppressions in the tree (`#pragma warning disable`, `[SuppressMessage]`, `.editorconfig` severity `none`: 91 sites, compared by file and rule with the kept `Is the suppression` rows) match the kept rows exactly; v0.15.0 added no suppression and removed none beyond SUP-005 (`apps/OmpOS.Web/Pages/Index.razor`, Phase 53) and SUP-073 (`WebSocketOmpTransportStub` tests, Phase 60), which are marked fixed. The 22 fixed rows are SUP-005, 050, 052, 055, 056, 058, 059, 062, 063, 065 to 068, 073, 074, 095 and 124 to 127, 129 and 130; the earlier line "164 SUP rows: 20 fixed, 144 kept" predates SUP-005 and SUP-073. The skip markers were not gate-enforced and had fallen behind: 72 `Assert.Skip`, `Assert.SkipUnless` and `Assert.SkipWhen` sites in 49 test files had no row, so SUP-165 to SUP-236 record them (symlink and POSIX-shell guards, Unix-mode guards, a privileged-user guard, and the live opt-ins `OMPOS_LIVE_*`). `docs/open-questions.md` now holds 236 SUP rows: 22 fixed, 214 kept. The skip rows are not read by `SuppressionLedgerTests` (it reads only `Is the suppression` rows); this paragraph and the table above are not re-counted for them.

Re-check, gate: `dotnet test tests/OmpOS.ArchitectureTests/OmpOS.ArchitectureTests.csproj -c Release --no-restore --filter-class OmpOS.ArchitectureTests.SuppressionLedgerTests` (not run by the author of this section). Re-check, skips: `grep -rnE "Assert\.Skip|Skip *= *\"|Trait\(\"Category\", *\"Live\"" --include=*.cs src tests apps services tools`.

### Findings

- No repository document forbids suppressions. AGENTS.md, CONTRIBUTING.md and PROJECT.md are silent on them. The rule "no pragma, NoWarn, SuppressMessage, Skip or disabled tests" was a coding rule the orchestrator set for the wave units, and the audit wrongly recorded it as a repo rule. Each occurrence was therefore a review question; Phase 25.9 answered every one.
- Fixed (20 recorded rows). 18 class-level `CA2007` and `MA0004` suppressions in test classes were removed without changing an `await`: every test project is an executable (`OutputType` `Exe` for `*Tests` projects in `Directory.Build.props`), where neither rule reports, and 196 other test files await without `ConfigureAwait` and carry no suppression. `GuardTests` now uses `Assert.ThrowsAny<Exception>` instead of a try/catch under a `CA1031` pragma, the same form `StartupConfigValidationTests` uses. `CapturingLogger<T>.LogEntry` is now `internal`, which removed its `CA1515` suppression. [INFERENCE, not built: the removals compile and stay warning-free; the orchestrator must build.]
- Kept (136). `CA1031` catch-alls sit at failure boundaries whose contract is "never throw" or "preserve the cause" (background loops, fail-closed permission paths, best-effort cleanup, readiness probes). `CA1515` suppressions are public types xUnit discovery or fixture generics require. `CA2000` suppressions mark disposal ownership handed to the object under test. `CA5394` is seeded or injected `Random`; `CA2100` is a SQL identifier the helper generates; `VSTHRD003` is a test-owned gate task. Skips fire only where the tool, OS, privilege or live opt-in is absent. Each SUP row carries its own justification text.
- `.editorconfig` overrides: none removed. `CA1303=none` (all C# files) stays because the product ships English-only text and no localization requirement exists; `CA1707`, `CS1591` and `CA1062` stay for `tests/**.cs` only, because test names use `Subject_Behaviour`, test projects publish no API and test helpers take trusted arguments. Removing them would need a build to measure the findings; none was run. The justification lives in SUP-001 to SUP-004 and in the table below.
- Skip markers hide tests on hosts without the tool. CI has `sh`, `head` and `tr`, so they run there; a host without them reports skipped, not failed.

### Decision table

One row per SUP id. Full text of each decision is the SUP row in `docs/open-questions.md`.

| SUP | Kind | Rule or marker | File | Decision | Basis |
|---|---|---|---|---|---|
| SUP-001 | .editorconfig | `CA1303` | `.editorconfig` | kept | Broad override; no localization requirement. |
| SUP-002 | .editorconfig | `CA1707` | `.editorconfig` | kept | Test naming convention; scoped to tests. |
| SUP-003 | .editorconfig | `CS1591` | `.editorconfig` | kept | Test projects publish no API; scoped to tests. |
| SUP-004 | .editorconfig | `CA1062` | `.editorconfig` | kept | Test helpers take trusted arguments; scoped to tests. |
| SUP-005 | SuppressMessage | `CA1031` | `apps/OmpOS.Web/Pages/Index.razor` | fixed | Removed: the page no longer starts the stream; the workbench shows faults in its error state (Phase 53). |
| SUP-006 | SuppressMessage | `CA1031` | `apps/OmpOS.Web/Services/ApiSessionClient.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-007 | SuppressMessage | `CA1711` | `apps/OmpOS.Web/Services/SseEventStream.cs` | kept | Naming or nesting contract recorded in the SUP row. |
| SUP-008 | SuppressMessage | `CA1031` | `apps/OmpOS.Web/Services/SseEventStream.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-009 | pragma | `CA1031` | `services/OmpOS.Api/Coordination/CoordinationStartup.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-010 | SuppressMessage | `CA1031` | `services/OmpOS.Api/Harness/SessionPermissionResolver.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-011 | pragma | `CA1031` | `services/OmpOS.Api/Health/SessionRepositoryCheck.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-012 | pragma | `CA1031` | `services/OmpOS.Api/Idempotency/IdempotencyCompletion.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-013 | pragma | `CA1031` | `services/OmpOS.Api/Services/PromptRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-014 | pragma | `CA1031` | `services/OmpOS.Api/Services/PromptRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-015 | pragma | `CA1031` | `services/OmpOS.Api/Sse/SessionChangePublisher.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-016 | pragma | `CA1031` | `services/OmpOS.Api/Sse/SessionSignalSubscriber.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-017 | pragma | `CA1031` | `services/OmpOS.Api/Workflows/LeaseRenewal.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-018 | pragma | `CA1031` | `services/OmpOS.Api/Workflows/SessionRunReaper.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-019 | SuppressMessage | `CA1031` | `src/OmpOS.Coordination.Dragonfly/DragonflySessionSignals.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-020 | SuppressMessage | `CA1031` | `src/OmpOS.Coordination/InMemorySessionSignals.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-021 | pragma | `CA1515` | `src/OmpOS.Harness.Abstractions/IHarnessSessionFactory.cs` | kept | Public test seam. |
| SUP-022 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Acp/AcpAgentSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-023 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Acp/AcpAgentSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-024 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Acp/AcpAgentSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-025 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Acp/AcpAgentSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-026 | SuppressMessage | `CA1034` | `src/OmpOS.Harness.Acp/AcpProtocol/AcpFrames.cs` | kept | Naming or nesting contract recorded in the SUP row. |
| SUP-027 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Rpc/OmpRpcClient.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-028 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Rpc/OmpRpcSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-029 | SuppressMessage | `CA1031` | `src/OmpOS.Harness.Rpc/OmpRpcSession.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-030 | SuppressMessage | `CA1711` | `src/OmpOS.Harness.Rpc/RestoredQueue.cs` | kept | Naming or nesting contract recorded in the SUP row. |
| SUP-031 | SuppressMessage | `CA1031` | `src/OmpOS.Parser/PromptParser.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-032 | SuppressMessage | `CA1031` | `src/OmpOS.Parser/PromptParser.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-033 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-034 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-035 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/LocalPodmanComposeProvider.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-036 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/OpenShellProvider.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-037 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/OpenShellProvider.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-038 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/ProcessRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-039 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/SandboxLifecycle.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-040 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/SandboxLifecycle.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-041 | SuppressMessage | `CA1031` | `src/OmpOS.Sandbox/SandboxLifecycle.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-042 | SuppressMessage | `CA5394` | `src/OmpOS.Sandbox/SandboxLifecycle.cs` | kept | Seeded or injected Random; not a security decision. |
| SUP-043 | SuppressMessage | `CA5394` | `src/OmpOS.Sandbox/SandboxLifecycle.cs` | kept | Seeded or injected Random; not a security decision. |
| SUP-044 | pragma | `CA1031` | `src/OmpOS.SessionRuns/SessionRunActivities.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-045 | pragma | `CA1031` | `src/OmpOS.SessionRuns/SessionRunActivities.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-046 | SuppressMessage | `CA1031` | `src/OmpOS.Workflows/DirectSessionRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-047 | SuppressMessage | `CA1031` | `src/OmpOS.Workflows/DirectSessionRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-048 | pragma | `CA1031` | `src/OmpOS.Workflows/DirectSessionRunner.cs` | kept | Failure boundary: a catch-all is the contract. |
| SUP-049 | SuppressMessage | `CA2000` | `tests/OmpOS.Api.IntegrationTests/DirectRunPermissionRoundTripTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-050 | pragma | `CA1031` | `tests/OmpOS.Api.IntegrationTests/GuardTests.cs` | fixed | Removed: catch replaced by Assert.ThrowsAny. |
| SUP-051 | SuppressMessage | `CA1515` | `tests/OmpOS.Api.IntegrationTests/ProcessPathSerialization.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-052 | SuppressMessage | `CA2007` | `tests/OmpOS.EngineHost.Tests/EngineHostTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-053 | pragma | `VSTHRD003` | `tests/OmpOS.EngineHost.Tests/EngineHostTests.cs` | kept | Test-owned gate or release task. |
| SUP-054 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpAgentSessionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-055 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.Acp.Tests/AcpAgentSessionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-056 | SuppressMessage | `MA0004` | `tests/OmpOS.Harness.Acp.Tests/AcpAgentSessionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-057 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpFrameReplayTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-058 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.Acp.Tests/AcpFrameReplayTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-059 | SuppressMessage | `MA0004` | `tests/OmpOS.Harness.Acp.Tests/AcpFrameReplayTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-060 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpLiveSmokeTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-061 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpParkedPermissionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-062 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.Acp.Tests/AcpParkedPermissionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-063 | SuppressMessage | `MA0004` | `tests/OmpOS.Harness.Acp.Tests/AcpParkedPermissionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-064 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpPermissionDecisionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-065 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.Acp.Tests/AcpPermissionDecisionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-066 | SuppressMessage | `MA0004` | `tests/OmpOS.Harness.Acp.Tests/AcpPermissionDecisionTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-067 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.Acp.Tests/AcpStdioTransportTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-068 | SuppressMessage | `MA0004` | `tests/OmpOS.Harness.Acp.Tests/AcpStdioTransportTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-069 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpStdioTransportTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-070 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpStdioTransportTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-071 | SuppressMessage | `CA1515` | `tests/OmpOS.Harness.ContractTests/AsyncStreamHelpers.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-072 | SuppressMessage | `CA1515` | `tests/OmpOS.Harness.ContractTests/LoopbackAcpTransport.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-073 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.InMemory.Tests/WebSocketOmpTransportStubTests.cs` | fixed | Removed: the file was deleted with the stub (Phase 60). |
| SUP-074 | SuppressMessage | `CA2007` | `tests/OmpOS.Harness.InMemory.Tests/WebSocketOmpTransportStubTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-075 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Rpc.Tests/OmpPinMismatchTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-076 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Rpc.Tests/OmpPinMismatchTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-077 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcAbortAndRestoreQueueTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-078 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcAbortAndRestoreQueueTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-079 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcClientStartTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-080 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcClientStartTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-081 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcCommandCorrelationTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-082 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcHarnessSessionFactoryTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-083 | skip | `Category=Live trait` | `tests/OmpOS.Harness.Rpc.Tests/RpcLiveCaptureTests.cs` | kept | Live opt-in or trait filter. |
| SUP-084 | skip | `Fact(Skip=...)` | `tests/OmpOS.Harness.Rpc.Tests/RpcLiveCaptureTests.cs` | kept | Live opt-in or trait filter. |
| SUP-085 | skip | `Fact(Skip=...)` | `tests/OmpOS.Harness.Rpc.Tests/RpcLiveSmokeTests.cs` | kept | Live opt-in or trait filter. |
| SUP-086 | skip | `Fact(Skip=...)` | `tests/OmpOS.Harness.Rpc.Tests/RpcLiveSmokeTests.cs` | kept | Live opt-in or trait filter. |
| SUP-087 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcPromptCompletionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-088 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcPromptCompletionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-089 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcPromptStreamTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-090 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcPumpFailureTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-091 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Rpc.Tests/RpcStderrFloodTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-092 | SuppressMessage | `CA5394` | `tests/OmpOS.Parser.Tests/RecognizerOrderingTests.cs` | kept | Seeded or injected Random; not a security decision. |
| SUP-093 | SuppressMessage | `CA5394` | `tests/OmpOS.Parser.Tests/RecognizerOrderingTests.cs` | kept | Seeded or injected Random; not a security decision. |
| SUP-094 | pragma | `VSTHRD003` | `tests/OmpOS.Presentation.Tests/ComposerViewModelTests.cs` | kept | Test-owned gate or release task. |
| SUP-095 | SuppressMessage | `CA1515` | `tests/OmpOS.Sandbox.Tests/CapturingLogger.cs` | fixed | Removed: type made internal. |
| SUP-096 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-097 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancellationRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-098 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancellationRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-099 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerDrainTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-100 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerDrainTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-101 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerDrainTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-102 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerLongLineRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-103 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerLongLineRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-104 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerLongLineRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-105 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerLongLineRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-106 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-107 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-108 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-109 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-110 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-111 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerStreamingTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-112 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerStreamingTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-113 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProductionProcessRunnerTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-114 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProductionProcessRunnerTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-115 | SuppressMessage | `CA1515` | `tests/OmpOS.Sessions.Postgres.Tests/PostgresServerFixture.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-116 | SuppressMessage | `CA1515` | `tests/OmpOS.Sessions.Postgres.Tests/PostgresServerGroup.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-117 | SuppressMessage | `CA2100` | `tests/OmpOS.Sessions.Postgres.Tests/PostgresTestDatabase.cs` | kept | SQL identifier generated by the helper, not input. |
| SUP-118 | SuppressMessage | `CA2100` | `tests/OmpOS.Sessions.Postgres.Tests/PostgresTestSchema.cs` | kept | SQL identifier generated by the helper, not input. |
| SUP-119 | skip | `Assert.Skip` | `tests/OmpOS.Sessions.Postgres.Tests/PostgresTwoProcessAppendTests.cs` | kept | Live opt-in or trait filter. |
| SUP-120 | pragma | `VSTHRD003` | `tests/OmpOS.UI.Tests/ComposerDraftTests.cs` | kept | Test-owned gate or release task. |
| SUP-121 | SuppressMessage | `CA1515` | `tests/OmpOS.UI.Tests/UiTestContext.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-122 | pragma | `VSTHRD003` | `tests/OmpOS.Web.Tests/GatedSseContent.cs` | kept | Test-owned gate or release task. |
| SUP-123 | SuppressMessage | `CA1515` | `tests/OmpOS.Workflows.Tests/DirectRunnerFixture.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-124 | SuppressMessage | `CA2007` | `tests/OmpOS.Workflows.Tests/DirectRunnerRetryPolicyTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-125 | SuppressMessage | `MA0004` | `tests/OmpOS.Workflows.Tests/DirectRunnerRetryPolicyTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-126 | SuppressMessage | `CA2007` | `tests/OmpOS.Workflows.Tests/DirectRunnerTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-127 | SuppressMessage | `MA0004` | `tests/OmpOS.Workflows.Tests/DirectRunnerTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-128 | SuppressMessage | `CA1515` | `tests/OmpOS.Workflows.Tests/ISessionRunWorkflowFixture.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-129 | SuppressMessage | `CA2007` | `tests/OmpOS.Workflows.Tests/SessionRunWorkflowContractTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-130 | SuppressMessage | `MA0004` | `tests/OmpOS.Workflows.Tests/SessionRunWorkflowContractTests.cs` | fixed | Removed: suppression was a no-op in an Exe test project. |
| SUP-131 | SuppressMessage | `CA1515` | `tests/OmpOS.Workflows.Tests/TestSessionRunActivities.cs` | kept | Public type required by xUnit discovery or fixture generics. |
| SUP-132 | SuppressMessage | `CA5394` | `tests/OmpOS.Workflows.Tests/TestWorkflowContext.cs` | kept | Seeded or injected Random; not a security decision. |
| SUP-133 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpMcpServerSessionTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-134 | SuppressMessage | `CA2000` | `tests/OmpOS.Harness.Acp.Tests/AcpPumpFaultLoggingTests.cs` | kept | Disposal ownership transfers to the object under test. |
| SUP-135 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpLaunchPlumbingTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-136 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpLaunchPlumbingTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-137 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpLaunchPlumbingTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-138 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Acp.Tests/AcpStdioTransportTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-139 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Launch/ConfigOverlayWriterTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-140 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Launch/ConfigOverlayWriterTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-141 | skip | `Category=Live trait` | `tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs` | kept | Live opt-in or trait filter. |
| SUP-142 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs` | kept | Live opt-in or trait filter. |
| SUP-143 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-144 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs` | kept | Live opt-in or trait filter. |
| SUP-145 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Live/LiveOmpHandshakeTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-146 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Plugins/OmpPluginResolverTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-147 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Plugins/OmpPluginResolverTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-148 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Sessions/AgentSessionRegistryTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-149 | skip | `Assert.Skip` | `tests/OmpOS.Harness.AgentLoop.Tests/Steering/FileSteeringChannelTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-150 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Providers.Tests/ProviderRegistryTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-151 | skip | `Assert.Skip` | `tests/OmpOS.Harness.Rpc.Tests/OmpVersionProbeOutputCapTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-152 | skip | `Assert.Skip` | `tests/OmpOS.Native.Tests/AgentLoop/EngineProcessTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-153 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancelDuringSpawnRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-154 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancelDuringSpawnRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-155 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancelDuringSpawnRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |
| SUP-156 | skip | `Assert.SkipUnless` | `tests/OmpOS.Sandbox.Tests/ProcessRunnerCancelDuringSpawnRegressionTests.cs` | kept | Environment precondition (tool, OS or privilege). |

## 3. WEB-01: browser smoke

**Result: PASS**, on commit `d965ba6`, 2026-10-22, against the working tree as found. Driver: `scripts/web-smoke/smoke.mjs`. Wrapper: `scripts/web-smoke/run-smoke.sh`. Result JSON: `web-smoke-d965ba6-2026-10-22.json`. Driver log: `web-smoke-d965ba6-2026-10-22.log`.

Chromium: `chromium`, `chromium-browser` and `chrome` are not on PATH. `google-chrome` is `/usr/bin/google-chrome`, version `Google Chrome 153.0.8010.36`, and was used. A Playwright build also exists at `~/.cache/ms-playwright/chromium-1248/chrome-linux64/chrome`, not used. Set `CHROME_BIN` to choose.

| Assertion (passing run) | Observed |
|---|---|
| First render clean | rendered; error bar hidden; 0 exceptions; 0 console errors; 0 reflection-disabled hits |
| Response completed | "Response complete." announced after the probe prompt |
| Probe markup | `strong` 1; `img` 0; `script` 0; no "Unsupported content"; user turn shown as escaped text |
| No error bar or console errors after prompt | error bar hidden; `consoleErrorsAfterPrompt` empty |
| Reload renders the same history | conversation items 2; opened index 1; `strong` 1; `img` 0; `script` 0; "Response complete." |
| axe-core 4.14.0 (WCAG 2.0/2.1 A and AA), empty state | 0 violations; 0 incomplete |
| axe-core 4.14.0, after streamed response | 0 violations; 0 incomplete |
| Send-button contrast | 18.26:1 (`rgb(0,0,0)` on `rgb(239,239,239)`), both states |
| Skip link | one Tab from body focuses `a.ompos-skip-link`; target `#ompos-conversation` exists |

Screenshots: `web-first-render`, `web-axe-empty`, `web-axe-after-response`, `web-skip-link-focused` (`.png`), and `web-vertical-slice-d965ba6-2026-10-22.webp`. axe-core SHA-256: `axe-core-4.14.0-d965ba6-2026-10-22.sha256`.

Run history. The smoke ran six times. Only attempt 6 is the result of record. Attempt logs are kept with `-attemptN` names.

| Attempt | Outcome | Cause | Change made |
|---|---|---|---|
| 1 | API publish failed; no browser | MSBuild internal error `Invalid node id specified: 3` | `-nodeReuse:false` on both publishes |
| 2 | API exited at start | `Api:Auth:Authority` and `Audience` required outside Development (OD-03); the published API defaults to Production | `ASPNETCORE_ENVIRONMENT=Development` |
| 3 | API exited at start | `Sessions:Store is required` | `Sessions__Store=InMemory`, `Coordination__Store=InMemory` |
| 4 | Browser ran; first render, axe and skip link passed; response never completed | every endpoint requires a bearer token by default; `/sessions` returned 401 ("You are offline.") | `Api__Auth__AllowDevelopmentPrincipal=true` (validator refuses it outside Development) |
| 5 | Response completed; reload assertion failed | driver defect: `Index.razor` selects the first listed conversation, which can be an empty session created on first load | driver opens each listed conversation until one renders its history |
| 6 | All 9 assertions true; exit 0 | — | result of record |

Gaps between `docs/runbooks/local-dev.md` and the running tree, for the owner to fix in the runbook (not edited here, outside this audit's paths):

- Its serve command omits `ASPNETCORE_ENVIRONMENT=Development`, `Sessions__Store`, `Coordination__Store` and `Api__Auth__AllowDevelopmentPrincipal`. Without them the API does not start, or the chat returns 401.
- It says the button is "Start new conversation". The tree has `aria-label="New conversation"` (visible text "+ New", `apps/OmpOS.Web/Pages/Index.razor:20`). The driver uses the tree's label.
- Its screenshot path is `docs/runbooks/evidence/web-vertical-slice-smoke.webp`. This run wrote `docs/runbooks/evidence/quality-audit-2026-10-22/web-vertical-slice-d965ba6-2026-10-22.webp`.

Limits: a local Development run with the explicit dev principal and in-memory stores. It does not exercise the production sign-in path (OD-03), and it does not cover the MAUI or native heads, or screen readers.

## 4. HO-03: podman-compose config

**Result: record still matches; config renders for the backend profile.** Full output: `ho-03-compose-config-d965ba6-2026-10-22.md`.

| Command (read-only) | Exit | Output |
|---|---|---|
| `podman-compose -f deploy/compose.yaml config` | **1** | `ValueError: required variable OMPOS_POSTGRES_PASSWORD is missing a value` |
| same, with the two password variables set to placeholders | **0** | `services: {}` and `WARNING: No services defined` |
| `podman-compose -f deploy/compose.yaml --profile backend config` (the test's argument order, placeholders set) | **0** | services `postgres`, `dragonfly`, `greptimedb`; no `temporal` |

Finding: without `--profile`, the compose file exposes no services, so the task's bare command exits 0 and validates nothing. The test and `ci.yml:92-102` pass `--profile backend`.

Comparison with `ho-03-podman-compose-config-2026-10-21.md`: the test name is unchanged; the file is `tests/OmpOS.Sandbox.Tests/LocalPodmanComposeProviderContractTests.cs`; the `[Fact]` is at line 133 and the closing brace at line 165, matching the record's "lines 133 to 165"; podman 4.9.3 and podman-compose 1.6.0 match. The 2026-10-21 pass itself was not re-run (no test run in this audit).

## 5. Findings for owners

- Decided in Phase 25.9 (2026-10-26), section 2: 156 suppression and skip rows, 20 fixed at the cause, 136 kept with a recorded justification. `SuppressionLedgerTests` keeps the ledger in step with the tree.
- `ci.yml` `shells` job will run for `apps/OmpOS.App`, which has no lock file (row 6). [INFERENCE, not run]
- `apps/OmpOS.App` still exists although `local-dev.md` says it retires at v0.10.0 Phase 25.
- `tools/gweb/stock` and `tools/gweb/named` are outside the solution filter and CI. `named` has no lock file.
- Runbook drift for WEB-01 (section 3).

## Evidence folder

`docs/runbooks/evidence/quality-audit-2026-10-22/`: `suppressions-full.tsv`, `suppressions-summary.txt`, `suppressions-id-map.tsv`; `ho-03-compose-config-d965ba6-2026-10-22.md`; `web-smoke-d965ba6-2026-10-22.{json,log}`, the screenshots and `.webp`, `axe-core-4.14.0-d965ba6-2026-10-22.sha256`, `chrome-*`, `api-*`, `web-*` and `api-publish-*` logs; and `*-attemptN.*` files for attempts 1 to 5.
