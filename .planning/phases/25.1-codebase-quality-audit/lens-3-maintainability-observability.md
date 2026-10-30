# Lens 3: Maintainability and observability

Repository `/root/src/repos/autonogrammer`, commit `59e9dd1`, branch `feat/v0.9.2-hardening`. Read-only. No build, test, formatter, or package manager was run.

Audit scope: tracked files from `git ls-files` under `src/`, `apps/`, `tests/`, `scripts/`, `.github/`, and `bootstrap.sh`, excluding `apps/Autonogrammer.Web/wwwroot/lib` and `bin/` or `obj/` output. That is 1,362 files, of which 1,129 are `.cs`. Counts below come from `git grep -c` / `git grep -n` with the same pathspec (`git grep` is git's own grep, not the shell tool), so they are exact for this scope.

## 1. Findings

Total rows: 20. Severity counts: High 0, Medium 4, Low 16.

| file:line | QUALITY_BAR item | severity | disposition | evidence (120 characters or fewer) |
|---|---|---|---|---|
| src/Autonogrammer.Coordination.Dragonfly/DragonflySessionSignals.cs:94 | Observability | Medium | fix | Handler failures dropped with no log; class has no logger. InMemorySessionSignals.cs:42 collects and rethrows. |
| src/Autonogrammer.Native.Views/MainView.axaml.cs:704 | Observability | Medium | fix | Close faults only reach Trace.TraceError. No listener in src/apps; tests add one (MainViewCloseFailureTests:34). |
| src/Autonogrammer.Harness.Acp/AcpAgentSession.cs:503 | Observability | Medium | defer | Harness, EngineHost, Workflows, Coordination: 0 ILogger uses. The pump fault at :503 is never logged. |
| src/Autonogrammer.Harness.Acp/AcpAgentSession.cs:1426 | Maintainability | Medium | defer | Largest in-scope .cs file: one sealed class of 1426 lines; nested PendingCall :1408, PromptTurn :1414. |
| src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs:209 | Observability | Low | fix | `_ = ex;` drops the stderr drain failure; the comment says diagnostics only, but nothing is recorded. Line 252 accepted. |
| apps/Autonogrammer.Web/Pages/Index.razor:89 | Observability | Low | fix | Four catch sites (:89, :105, :159, :184) show ex.Message and log nothing. The stream catch at :288 logs. |
| src/Autonogrammer.EngineHost/RemoteEngineHost.cs:345 | Observability | Low | accept | Transport drop ends the stream by `break` with no log. Comment at :347 says ReconnectAsync resumes from cursor. |
| src/Autonogrammer.Sandbox/ProcessRunner.cs:37 | Maintainability | Low | defer | 846 lines in one sealed class file; nested BoundedLineReader at :144. Size only; no defect claimed. |
| src/Autonogrammer.Sessions/SessionService.cs:50 | Maintainability | Low | defer | 804 lines in one sealed class; nested SessionLease at :789. Size only; no defect claimed. |
| src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs:19 | Maintainability | Low | defer | 802 lines in one sealed class covering provisioning, teardown and file staging. Size only; no defect claimed. |
| src/Autonogrammer.Harness.Rpc/OmpRpcSession.cs:30 | Maintainability | Low | defer | 784 lines in one sealed class; transport, pump and dispatch in one file. Size only; no defect claimed. |
| src/Autonogrammer.Native.Views/MainView.axaml.cs:48 | Maintainability | Low | defer | 761-line partial class; close, search, tool and send wiring in one file. Size only; no defect claimed. |
| tests/Autonogrammer.Harness.Acp.Tests/AcpAgentSessionTests.cs:14 | Maintainability | Low | defer | 977 lines; largest in-scope test file. Size only. |
| tests/Autonogrammer.EngineHost.Tests/EngineHostTests.cs:18 | Maintainability | Low | defer | 881 lines; second-largest in-scope test file. Size only. |
| tests/Autonogrammer.Sandbox.Tests/ProcessRunnerDrainTests.cs:29 | Maintainability | Low | accept | 32 skip sites: 27 gate on POSIX tools, Linux, podman; 5 on live omp or env. CI has sh/head/tr (quality-bar doc). |
| src/Autonogrammer.Harness.Acp/AcpAgentSession.cs:191 | Maintainability | Low | accept | 81 SuppressMessage plus 9 pragma disables. Each has a Justification or same-line reason; every pragma restored. |
| src/Autonogrammer.Harness.Rpc/OmpRpcClient.cs:86 | Observability | Low | accept | Compensating dispose swallowed by design; comment at :88 says the start failure stays authoritative. No log. |
| apps/Autonogrammer.Web/Services/SseEventStream.cs:349 | Observability | Low | accept | Best-effort error-body read returns null with no log; suppression at :335 justifies it. ApiSessionClient.cs:114 same. |
| src/Autonogrammer.Harness.OmpLauncher/OmpVersionProbe.cs:97 | Observability | Low | accept | Timeout or IO failure swallowed silently; comment at :99 says the timeout result stands. |
| src/Autonogrammer.Harness.Acp/AcpAgentSession.cs:20 | Observability | Low | defer | 0 ActivitySource hits in src/ and apps/. Spans exist only in services/Autonogrammer.Api/Telemetry, outside this scope. |

Notes on the findings:

- The Medium observability rows are the ones that matter. Each is a failure path whose only signal is a silent catch, a Trace sink with no listener, or a missing logger.
- M3 (`AcpAgentSession.cs:503`) covers DirectSessionRunner.cs:65 too. That catch sets `state.FailureDetail = ex.Message` and does not log. The row cites the Acp site only because the 120-character evidence cap leaves no room for a second file.
- Rows marked "accept" are catch-all handlers or silent returns that carry a written reason in source. They are listed so the owner can see them, not because they are defects.
- No High finding. No unlinked TODO marker (VERIFY-02 rule). No sensitive value in a log call. No commented-out code. No mutable public static field.

## 2. Scans

Every pattern was run over the in-scope pathspec. A hit count of 0 means the pattern matched nothing in scope.

**S1. `TODO|FIXME|HACK|XXX`** (case-sensitive, not whole-word; a superset of the VERIFY-02 rule). Raw hits: **13 lines in 5 files.** VERIFY-02 rule: a whole-word uppercase token must share its line with an OQ-NN or Q-UPPER ID that has an owner in `docs/open-questions.md`.

| Hit(s) | Line carries OQ/Q link? | Classification |
|---|---|---|
| `scripts/test-services.sh:84` (1) | no | Fine. `mktemp` template `XXXXXX`. VERIFY-02's `\b(?:XXX)\b` does not match inside `XXXXXX`, so it is not a marker. |
| `tests/Autonogrammer.ArchitectureTests/RepoHygieneTests.cs:131,150,169` (3) | `:169` names OQ-01 as a string | Fine. Scanner positive and negative controls. Path is in `SelfExcludedPaths`. |
| `tests/Autonogrammer.ArchitectureTests/TodoMarkerScanner.cs:6,11,46` (3) | no | Fine. The scanner's own doc and regex. Path is self-excluded. |
| `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-live-20261021T171351Z.ndjson:2,4,6,7` (4) | no | Fine. `TestData` path segment is excluded by VERIFY-02. |
| `tests/Autonogrammer.Harness.Rpc.Tests/TestData/frames/rpc-out.ndjson:3,5` (2) | no | Fine. `TestData` path segment is excluded. |

Result: **0 first-party markers that need an OQ link.** No real `TODO`, `FIXME`, `HACK`, or `XXX` marker exists in scope. [INFERENCE] The VERIFY-02 test would report 0 offenders, derived by reading the rule. The test was not run.

**S2. `#pragma warning disable|SuppressMessage|NoWarn|GlobalSuppressions|\[Suppress`.** Raw hits: **91 lines.**
- 81 attribute openers (`SuppressMessage(` or `[Suppress`). Per-file opener counts equal per-file `Justification = ` counts. No file has a mismatch.
- 9 `#pragma warning disable` lines, each with a same-line reason comment. Every file with a disable also has a matching `restore`.
- 1 `NoWarn` match: the test method name `SuccessfulPartialTeardown_LogsNoWarning` (`tests/Autonogrammer.Sandbox.Tests/PodmanCleanupFailureRegressionTests.cs:65`). Fine: a name, not a suppression.
- 0 `GlobalSuppressions` and 0 `NoWarn` attributes or project properties.

Result: every suppression carries a justification in source. The rules are CA1031 (catch-all), CA2000, CA2007, MA0004, CA1515, CA1034, CA1711 and VSTHRD003. The quality-bar document (docs/quality/quality-bar.md, section 2) records the same suppressions with rows SUP-001 to SUP-132.

**S3. `Console\.(Write|WriteLine|Error)` in `src/` and `apps/`.** **0 hits** (exit 1). Logging in product code uses `ILogger` or `LoggerMessage`. Files with `ILogger`/`LoggerMessage` in src and apps: 5 (`apps/Autonogrammer.Web/Pages/Index.razor`, `src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs`, `OpenShellProvider.cs`, `SandboxLifecycle.cs`, `src/Autonogrammer.SessionRuns/SessionRunActivities.cs`).

Answer to "logger or Trace?": the repo's logger is `ILogger`. `System.Diagnostics.Trace` appears only at two call sites, `src/Autonogrammer.Native.Views/MainView.axaml.cs:704` and `:715`. No production code configures a Trace listener; only `tests/Autonogrammer.Native.Tests/MainViewCloseFailureTests.cs:34,61` adds one. That is finding M2.

**S4. Commented-out code: `^\s*//\s*(var |await |return |if \(|foreach |public |private |using )`.** **0 hits.** Broader check `^\s*//.*;\s*$` in scope: 28 hits, all prose. Examples: `tests/Autonogrammer.Harness.Providers.Tests/VerifiedProviderRows.cs:60` (provenance note), `src/Autonogrammer.Parser/PromptInput.cs:11` (doc comment). None is commented-out code.

**S5. Mutable public static fields** (heuristic: `public static` line, no `readonly`/`const`, no parentheses, ends in `;`). **8 hits, all expression-bodied `=>` read-only properties**, not fields: `src/Autonogrammer.Contracts/Ui/UserMessageBlock.cs:12`, `src/Autonogrammer.Harness.Abstractions/HarnessStateMachine.cs:15`, `src/Autonogrammer.Native.Views/Views/Blocks/BlockTokens.cs:13`, `tests/Autonogrammer.Harness.Rpc.Tests/RpcLiveSmokeTests.cs:15`, `tests/Autonogrammer.Native.Tests/Completion/CompletionHarness.cs:21`, `tests/Autonogrammer.Native.Tests/Export/ExportHarness.cs:17`, `tests/Autonogrammer.Native.Tests/ThemeContrastTests.cs:113,116`. Public static properties with `set` or `init`: **0.** Get-only public static properties: 27, and the three returning collections (`src/Autonogrammer.GenUi/A2uiSurfaceCatalog.cs:44`, `src/Autonogrammer.Harness.OmpLauncher/OmpProcessStarter.cs:16`, `tests/Autonogrammer.Parity.Support/ParityScenarios.cs:159`) are `IReadOnlyList<>`. Result: **0 mutable public static fields.**

**S6. Sensitive values in log calls: `(?i)(log|logger|trace)\.[a-z]+\(.*(password|token|secret|apikey)`.** **1 hit:** `tests/Autonogrammer.Harness.Agent.Tests/FakeOmpAgentAdapter.cs:98`, `_log.FollowAsync(afterSequence, cancellationToken)`. Fine: not a log call. The match is `Token` inside `cancellationToken`.

Broader check (`LoggerMessage` or logger-named call with `token|secret|password|apikey|credential`): in-scope hits are `src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs:238,242,247` and the definition at `:43`. These pass `secretName`. The template at `:47` is `"...credential secret {SecretName}..."`, which logs a secret's **name**, not its value. Fine. Result: **0 sensitive values logged.**

**S7. Catch-all census** (not in the required list, recorded for context): `catch (Exception` or `catch {` in `src/` and `apps/`: **53 sites.** Sites with no log, no rethrow and no recorded diagnostic:
- `src/Autonogrammer.Coordination.Dragonfly/DragonflySessionSignals.cs:94` (M1)
- `src/Autonogrammer.Harness.Acp/AcpStdioTransport.cs:209` (L1), `:252` (accepted)
- `src/Autonogrammer.Harness.Acp/AcpAgentSession.cs:237`, `:503` (M3)
- `src/Autonogrammer.Harness.Rpc/OmpRpcClient.cs:86`, `OmpRpcSession.cs:339`, `OmpRpcProcess.cs:246`
- `src/Autonogrammer.Harness.OmpLauncher/OmpVersionProbe.cs:97`
- `src/Autonogrammer.EngineHost/RemoteEngineHost.cs:345`
- `src/Autonogrammer.Native.Views/Views/Hooks/HooksModel.cs:125,186`, `WorkspaceSearchModel.cs:59` (failure shown to the user as `exception.Message`; no log)
- `apps/Autonogrammer.Web/Services/ApiSessionClient.cs:114`, `SseEventStream.cs:349`
- `apps/Autonogrammer.Web/Pages/Index.razor:89,105,159,184` (L2)

All other catch-all sites log, record the failure on state, or rethrow.

**S8. Skip markers** (plan item; not in the requested pattern list): `Assert\.Skip|SkipUnless|\[Fact\(Skip|Skip ?=|\[Theory\(Skip`. **34 raw hits.** Two are false positives: `scripts/web-smoke/smoke.mjs:417` (`skipLinkFocusedOnTab`) and `src/Autonogrammer.Harness.Workspace/WorkspaceSearch.cs:148` (`AttributesToSkip`). **32 real skip sites:** 27 gate on POSIX tools, Linux, `/bin/printf`, or `podman`/`podman-compose`; 5 gate on a live `omp` on PATH or an environment variable (`AcpLiveSmokeTests.cs:25`, `RpcLiveCaptureTests.cs:46`, `RpcLiveSmokeTests.cs:29,56`, `PostgresTwoProcessAppendTests.cs:43`). Representative row L11.

**S9. Largest `.cs` files, in scope** (`git ls-files -- 'src/**/*.cs' 'tests/**/*.cs' 'apps/**/*.cs' 'scripts/**/*.cs'` piped to `wc -l`; 1,129 files):

| Rank | Lines | File |
|---|---|---|
| 1 | 1426 | src/Autonogrammer.Harness.Acp/AcpAgentSession.cs |
| 2 | 977 | tests/Autonogrammer.Harness.Acp.Tests/AcpAgentSessionTests.cs |
| 3 | 881 | tests/Autonogrammer.EngineHost.Tests/EngineHostTests.cs |
| 4 | 846 | src/Autonogrammer.Sandbox/ProcessRunner.cs |
| 5 | 804 | src/Autonogrammer.Sessions/SessionService.cs |
| 6 | 802 | src/Autonogrammer.Sandbox/LocalPodmanComposeProvider.cs |
| 7 | 784 | src/Autonogrammer.Harness.Rpc/OmpRpcSession.cs |
| 8 | 761 | src/Autonogrammer.Native.Views/MainView.axaml.cs |
| 9 | 625 | src/Autonogrammer.Sessions.Postgres/PostgresSessionRepository.cs |
| 10 | 614 | src/Autonogrammer.EngineHost/RemoteEngineHost.cs |
| 11 | 611 | tests/Autonogrammer.Sessions.Tests/SessionServiceTests.cs |
| 12 | 540 | tests/Autonogrammer.Native.Tests/Send/EngineSendPathTests.cs |
| 13 | 534 | tests/Autonogrammer.Native.Tests/Shell/ShellWave7Tests.cs |
| 14 | 531 | tests/Autonogrammer.Api.IntegrationTests/DirectRunPermissionRoundTripTests.cs |
| 15 | 519 | tests/Autonogrammer.Native.Tests/Terminal/TerminalPanelViewTests.cs |

Outside scope, not in the table: `services/Autonogrammer.Api/Endpoints/SessionEndpoints.cs` is 546 lines (it would rank 12th in an unscoped `*.cs` listing).

**S10. Logger and trace census** (context for S3 and M3): `ILogger` or `LoggerMessage` appears in 0 files under `src/Autonogrammer.Harness.*`, `src/Autonogrammer.EngineHost`, `src/Autonogrammer.Workflows`, `src/Autonogrammer.Coordination*`, `src/Autonogrammer.Native.Views`, and `apps/Autonogrammer.Native`. `ActivitySource` appears 0 times in `src/` and `apps/`.

## 3. Gaps

- **Not run:** build, tests, the VERIFY-02 scanner (`TodoMarkerScanner`), analyzers, and formatters. Per the phase rules. The VERIFY-02 result (0 offenders) is derived by reading the rule and the 13 hits, not by execution.
- **Dead code and unused members:** need a build with analyzers or a whole-program reference count. Not claimed here. This is the gap the phase context already records.
- **Analyzer maintainability metrics** (MA rules, CA1502 cyclomatic complexity, CA1506 coupling): not computed. Size findings use line counts only.
- **Production visibility of Trace output (M2):** the claim that no listener exists is from a static search of `src/` and `apps/` (no `Trace.Listeners`, `AddTraceListener`, or console listener). Whether `DefaultTraceListener` output reaches an operator in a release run is [INFERENCE] and was not observed.
- **Whether CI skips hide coverage (L11):** the claim that CI has `sh`, `head`, and `tr` comes from `docs/quality/quality-bar.md` and was not re-checked against a CI log.
- **Reconciliation gap:** `docs/quality/quality-bar.md` section 2 records 18 `#pragma warning disable` and 28 skip markers. In scope this lens counts 9 pragmas and 32 skip sites. The difference is not explained: that document counted outside this scope (`docs/`, `services/`, `tools/`), and its totals reflect the tree as of `d965ba6`, not `59e9dd1`. Not reconciled.
- **Out of scope but relevant:** `.editorconfig` (its four `severity = none` overrides, cited by the quality-bar document as CA1303, CA1707, CS1591, CA1062) and `Directory.Build.props` were not read. `services/Autonogrammer.Api/Telemetry/ApiTelemetry.cs` holds the only tracing in the repo. `tools/gweb/*` was not read.
- **Owned by other lenses:** exception-message disclosure to the UI (`Index.razor:91` and similar) belongs to lens 2. The mixed logger-versus-Trace convention beyond the two call sites belongs to lens 4 (pattern fidelity).
- **Heuristic limits:** the commented-out-code and mutable-static checks are line-pattern heuristics. A commented-out block that does not begin with one of the listed keywords would be missed. Mutable state held in a private static field is outside the lens's public-static rule.

## 4. Method

I started from the phase plan's list for this lens (markers, suppressions, skips, commented-out code, mutable statics, logging of values) and the requested pattern set. I read the VERIFY-02 scanner first, because it defines what counts as an unlinked marker, and I read `CONTRIBUTING.md`'s TODO policy to confirm the required form. I built the in-scope file list with git pathspecs, so the exclusions for `wwwroot/lib`, `bin`, and `obj` are enforced by git and not by eye. Every pattern count came from `git grep` over that same pathspec. I then read the code around each hit that could be a finding: every suppression-pragma pair, every catch-all handler that lacks an obvious log call, each Trace call site, and each file that appeared in the size ranking. I chose the largest files by line count from the in-scope `.cs` set, and I did not sample beyond the scan results. I used the `_grep` tool only to orient across large result sets, not for any reported count.
