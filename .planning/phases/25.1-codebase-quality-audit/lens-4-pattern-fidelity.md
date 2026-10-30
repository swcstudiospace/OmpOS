# Lens 4: Pattern fidelity

Repository `/root/src/repos/autonogrammer` at commit `59e9dd1` (branch `feat/v0.9.2-hardening`). Read-only. Scope: tracked files under `src/` and `apps/`, excluding `apps/Autonogrammer.Web/wwwroot/lib`. `tests/` and `services/` appear only where a scan says so.

QUALITY_BAR item: **Pattern fidelity** (plan text: mixed conventions for logging, time, HTTP clients, sync and async style, duplicated helpers). `docs/quality/quality-bar.md` defines no pattern rule, and `AGENTS.md:216-218` says "Conventions not yet established". So "drift" below means an undocumented split, judged from uniformity within a project and from the absence of any written rule. It does not mean a breach of a stated rule.

## 1. Findings

Total: 14 rows, under the cap of 40. High 0, Medium 1, Low 13. Disposition counts: fix 6, accept 4, defer 4.

| file:line | QUALITY_BAR item | severity | disposition | evidence (120 characters or fewer) |
|---|---|---|---|---|
| `src/Autonogrammer.Native.Views/MainView.axaml.cs:704,715` | Pattern fidelity: logging mechanism | Medium | fix | `Trace.TraceError` is the only report of close faults; no Trace listener in shipped code, tests only |
| `src/Autonogrammer.Workflows/IWorkflowContext.cs:13` | Pattern fidelity: time source | Low | fix | Second clock seam beside TimeProvider; `UtcNow` has no reader in src or tests; its ADR-0003 reason is superseded |
| `src/Autonogrammer.Native.Views/MainView.axaml.cs:376,419` | Pattern fidelity: time source | Low | defer | `DateTimeOffset.UtcNow` in head code-behind; session services take an injected TimeProvider (SessionService.cs:85) |
| `apps/Autonogrammer.Web/Services/SseEventStream.cs:313` | Pattern fidelity: time source | Low | accept | Receive-time stamp in static ConvertFrame; wire data is parsed as payload only (:293, :327), so no server time |
| `src/Autonogrammer.Harness.Workspace/WorkspaceSearch.cs:50` | Pattern fidelity: sync and async style | Low | fix | Task.Run offloads a synchronous Search with no stated reason; HookRunner, PluginHost, HerdrSessionStore comment theirs |
| `src/Autonogrammer.EngineHost/RemoteEngineHost.cs:61` | Pattern fidelity: HTTP clients | Low | defer | Four HttpClient-taking types (also CompletionClient:27, AgentOsProvider:53, RailwaySwarmProvider:67) never constructed |
| `src/Autonogrammer.SessionRuns/SessionRunActivities.cs:59` | Pattern fidelity: convention naming | Low | fix | One TimeProvider field, three names: `_time` here, `_clock` in 3 files, `_timeProvider` in 4 files |
| `tests/Autonogrammer.Api.IntegrationTests/FixedTimeProvider.cs:6` | Pattern fidelity: duplicated helpers | Low | fix | Same one-instant clock as tests/Autonogrammer.Sessions.Tests/FixedTimeProvider.cs:4; bodies match |
| `tests/Autonogrammer.Coordination.Tests/ManualTimeProvider.cs:4` | Pattern fidelity: duplicated helpers | Low | fix | Same Advance clock as tests/Autonogrammer.Sandbox.Tests/FakeTimeProvider.cs:6; class name also in Herdr.Tests:7 |
| `src/Autonogrammer.Harness.Workspace/WorkspaceConventions.cs:1` | Pattern fidelity: namespace matches folder | Low | defer | `namespace Autonogrammer.Workspace;` under folder Autonogrammer.Harness.Workspace; 26 of 26 files drop the Harness prefix |
| `src/Autonogrammer.Harness.Worktrees/WorktreeRunId.cs:3` | Pattern fidelity: namespace matches folder | Low | defer | `namespace Autonogrammer.Worktrees;` under Autonogrammer.Harness.Worktrees; 11 of 11 files drop the Harness prefix |
| `src/Autonogrammer.Native.Views/Views/Blocks/ToolCallBlockView.axaml.cs:5` | Pattern fidelity: namespace matches folder | Low | accept | `Views/` absent from the namespace in all 60 files under Views/; uniform, so accept only if documented |
| `src/Autonogrammer.Harness.Rpc/RpcProtocol/RpcPromptError.cs:1` | Pattern fidelity: namespace matches folder | Low | accept | `RpcProtocol/` absent from the namespace; all 18 files in the folder declare Autonogrammer.Harness.Rpc |
| `src/Autonogrammer.Sandbox.Abstractions/Models/SandboxCapability.cs:3` | Pattern fidelity: namespace matches folder | Low | accept | `Models/` absent from the namespace; all 17 files in the folder declare Autonogrammer.Sandbox.Abstractions |

## 2. Scans

All counts come from `git grep -c` or `git grep -n` over the scope named (`-- src apps ':!apps/Autonogrammer.Web/wwwroot/lib'` unless stated). The grep tool read the 30-file namespace sample and the code around each hit.

### 2.1 Time

| Pattern | Scope | Hits | Classification |
|---|---|---|---|
| `DateTime\.(Now\|UtcNow)` | src, apps | 0 | none |
| `DateTimeOffset\.(Now\|UtcNow)` | src, apps | 5 | 4 code: MainView.axaml.cs:376, :419 (row 3); DirectWorkflowContext.cs:17 (row 2); SseEventStream.cs:313 (row 4). 1 doc cref: DirectWorkflowContext.cs:7, not a call. |
| `TimeProvider` (requested comparison) | src | 24 occurrences in 10 files | The convention. All 10 files take a constructor-injected TimeProvider. Herdr's optional default is documented (see 2.8). |
| `TimeProvider` | apps | 0 | apps use no injected clock (row 4 is the only wall-clock read there) |
| `class [A-Za-z]*TimeProvider\b` (extension) | tests | 7 classes, 7 projects | Duplicates: rows 8 and 9. Naming: Fixed, Manual and Fake for the same concept. |

### 2.2 HTTP clients

| Pattern | Scope | Hits | Classification |
|---|---|---|---|
| `new HttpClient\(` | src, apps | 0 | none |
| `new HttpClient\b` (object-initializer form, extension) | src, apps | 2 | apps/Autonogrammer.Web/Program.cs:17, :24. Fine: standalone Blazor WebAssembly host; one `BaseAddress` per scoped consumer inside `AddScoped`. |
| `IHttpClientFactory\|AddHttpClient` | src, apps, whole tracked repo | 0 | none. No factory is used anywhere. |
| `\bHttpClient\b` | src, apps | 7 files | apps/Autonogrammer.Web 3 (ApiSessionClient, SseEventStream, Program); src/Autonogrammer.EngineHost 1; src/Autonogrammer.Harness.Completion 1; src/Autonogrammer.Sandbox 2. The four library types take the client by constructor; no construction site exists (row 6). |

### 2.3 Task.Run

| Pattern | Scope | Hits | Classification |
|---|---|---|---|
| `Task\.Run\(` | src | 4 in 4 files | HerdrSessionStore.cs:262: fine, reason at :261 (no async flush to disk in .NET). HookRunner.cs:129: fine, reason at :128 (sync throw becomes a faulted task). PluginHost.cs:160: fine, reason at :159 (same). WorkspaceSearch.cs:50: row 5, no reason. |
| `Task\.Run\(` | apps | 0 | none |

Out of scope, for context: services/ and tests/ hold 8 more `Task.Run(` hits.

### 2.4 Logging mechanism, per top-level folder

| Folder | `\bTrace\.` | `ILogger` | `\bConsole\.` call sites | `LoggerMessage` |
|---|---|---|---|---|
| src/Autonogrammer.Native.Views | 2 | 0 | 0 | 0 |
| src/Autonogrammer.Sandbox | 0 | 14 | 0 | 7 |
| src/Autonogrammer.SessionRuns | 0 | 5 | 0 | 2 |
| apps/Autonogrammer.Web | 0 | 2 | 0 | 1 |
| every other src/ and apps/ folder | 0 | 0 | 0 | 0 |

- `\bConsole\.` has 12 raw hits, all `Microsoft.Windows.Console.ConPTY` in `packages.lock.json` (apps/Autonogrammer.Android 6, apps/Autonogrammer.Native 2, src/Autonogrammer.Harness.Terminal 2, src/Autonogrammer.Native.Views 2). False positives. Zero console calls in source.
- `LoggerMessage\|ILoggerFactory\|Debug\.Write\|Serilog\|ActivitySource\|Activity\.Current\|\bActivity\b`: 14 hits, of which 10 are logging (Sandbox 7, SessionRuns 2, Web 1). The other 4 are Android (MainActivity.cs:10 `[Activity(` attribute and 3 AndroidX lock lines), not logging.
- `Trace\.Listeners` in src, apps: 0. The only listener is `tests/Autonogrammer.Native.Tests/MainViewCloseFailureTests.cs:34` and `:61`.
- Native.Views does not reference `Microsoft.Extensions.Logging`. Its csproj lists Avalonia, AvaloniaEdit, TextMate, Terminal, Markdig and TextMateSharp (`src/Autonogrammer.Native.Views/Autonogrammer.Native.Views.csproj:9-14`). Only Sandbox and SessionRuns reference `Logging.Abstractions`.

### 2.5 Duplicated test helpers (tests/)

| Pattern | Hits | Classification |
|---|---|---|
| `class (ShellHarness\|TestAppBuilder\|FakeClock\|ManualTimeProvider\|FakeMcpToolRegistry\|RecordingSink)\b` | 6 | `ManualTimeProvider` in 2 projects: Coordination.Tests:4 and Herdr.Tests:7 (row 9; the Herdr body adds monotonic timestamps). `FakeClock`: 0. `ShellHarness` (Native.Tests/ShellHarness.cs:14), `TestAppBuilder` (Native.Tests/TestAppBuilder.cs:16), `FakeMcpToolRegistry` (Native.Tests/Mcp/FakeMcpToolRegistry.cs:12), `RecordingSink` (Harness.Terminal.Tests/RecordingSink.cs:8): one definition each. Fine. |
| `class [A-Za-z]*(CapturingLogger\|CapturingLoggerProvider\|RecordingLogger)\b` (extension) | 3 | Sandbox.Tests/CapturingLogger.cs:10 (`CapturingLogger<T>`) and Api.IntegrationTests/CapturingLoggerProvider.cs:12 with nested `CapturingLogger` at :37. Different shapes, not duplicates. Fine. |
| shared test support | n/a | `tests/Autonogrammer.Parity.Support` exists as a shared project, but no time fake uses it. |

### 2.6 Namespace versus folder

Sample: `git ls-files 'src/*.cs'` returned 638 files. Taking every 21st line (`sed -n '1~21p'`) gave 31; the last one (`src/Autonogrammer.Workflows/AssemblyMarker.cs`) was dropped to hold 30. The grep tool read `^namespace ` in each.

Result: 23 match, 7 mismatch.

| File | Namespace | Folder or project | Kind |
|---|---|---|---|
| `src/Autonogrammer.Harness.Rpc/RpcProtocol/RpcPromptError.cs:1` | Autonogrammer.Harness.Rpc | Rpc/RpcProtocol | subfolder dropped (row 13) |
| `src/Autonogrammer.Harness.Workspace/WorkspaceConventions.cs:1` | Autonogrammer.Workspace | Autonogrammer.Harness.Workspace | project prefix dropped (row 10) |
| `src/Autonogrammer.Harness.Worktrees/WorktreeRunId.cs:3` | Autonogrammer.Worktrees | Autonogrammer.Harness.Worktrees | project prefix dropped (row 11) |
| `src/Autonogrammer.Native.Views/Views/Blocks/ToolCallBlockView.axaml.cs:5` | Autonogrammer.Native.Views.Blocks | Views/Blocks | `Views/` dropped (row 12) |
| `src/Autonogrammer.Native.Views/Views/Export/TranscriptExportView.axaml.cs:8` | Autonogrammer.Native.Views.Export | Views/Export | `Views/` dropped (row 12) |
| `src/Autonogrammer.Native.Views/Views/Search/WorkspaceSearchModel.cs:3` | Autonogrammer.Native.Views.Search | Views/Search | `Views/` dropped (row 12) |
| `src/Autonogrammer.Sandbox.Abstractions/Models/SandboxCapability.cs:3` | Autonogrammer.Sandbox.Abstractions | Models | subfolder dropped (row 14) |

Whole-folder counts, confirming the pattern is uniform within each folder:

- `src/Autonogrammer.Native.Views/Views/`: 60 files. 55 declare `Autonogrammer.Native.Views.<sub>`, 5 declare `Autonogrammer.Native.Views` (the root of Views/), 0 declare `Autonogrammer.Native.Views.Views`.
- `src/Autonogrammer.Harness.Rpc/RpcProtocol/`: 18 files, 18 declare `Autonogrammer.Harness.Rpc`.
- `src/Autonogrammer.Sandbox.Abstractions/Models/`: 17 files, 17 declare `Autonogrammer.Sandbox.Abstractions`.
- `src/Autonogrammer.Harness.Workspace/`: 26 files, 26 declare `Autonogrammer.Workspace`.
- `src/Autonogrammer.Harness.Worktrees/`: 11 files, 11 declare `Autonogrammer.Worktrees`.

Rule on record: `.editorconfig:66` `dotnet_style_namespace_match_folder = true:suggestion`. Suggestion severity; not enforced by the build.

### 2.7 #region and sync and async style

| Pattern | Scope | Hits | Classification |
|---|---|---|---|
| `#region` / `#endregion` | src, apps, tests, services (non-vendored) | 0 | No convention and no use. |
| `GetAwaiter\(\)\.GetResult\(\)` | src, apps | 0 | none |
| `async void` | src, apps | 0 | none |
| `\.Result\b\|\.Wait\(\)` | src, apps | 5 | Not classified here. Name matches: AcpAgentSession.cs:1391; InlineCompletionBox.axaml.cs:84, :187, :219; PromptParser.cs:119. Lens 1 owns sync-over-async. |

### 2.8 Mixed conventions: decision

1. **Logging: `ILogger` versus `System.Diagnostics.Trace`.** Drift. Libraries (Sandbox, SessionRuns) and the web head (Index.razor) use `ILogger` with `LoggerMessage` delegates. Native.Views uses `Trace` alone, because it has no Logging reference. No document names the split. The two `Trace` calls are the only failure report for window close, and no listener is configured in shipped code. Row 1.
2. **Time: `TimeProvider` versus direct wall clock.** `TimeProvider` is the convention: 10 src files, all constructor-injected. Three direct reads sit outside it. Native.Views (rows 3) is drift. The web receive stamp (row 4) is deliberate by necessity, because the wire carries no server time. The workflow seam (row 2) is a superseded parallel clock.
3. **Clock field names.** Drift. One parameter type under three field names (row 7).
4. **Optional clock default.** `HerdrHeartbeatMonitor(TimeProvider? timeProvider = null)`, defaulting to `TimeProvider.System` (HerdrHeartbeatMonitor.cs:26-29). Deliberate and documented in the constructor's XML comment ("The system clock when null"). Not a finding.
5. **HTTP: `new HttpClient { BaseAddress }` in `AddScoped` versus `IHttpClientFactory`.** Deliberate for the web host: a standalone Blazor WebAssembly app with one base URL per consumer. Libraries take an injected client, and no composition root exists in tracked code (row 6). Not documented; the WebAssembly reason is framework practice, not a repo decision.
6. **`Task.Run` offload.** Three of four sites carry a reason in source, so they are deliberate and documented. WorkspaceSearch.cs:50 is drift (row 5).
7. **Namespace versus folder.** Uniform per folder, and undocumented. The one rule on record is advisory (`.editorconfig:66`). The `Views/`, `RpcProtocol/` and `Models/` segments are a consistent undocumented convention (rows 12 to 14). Dropping the `Harness.` project prefix (rows 10 and 11) breaks the folder-to-assembly mapping, so it is drift.
8. **Test time fakes.** Drift. Two identical pairs (rows 8 and 9) and three names for one concept. A shared support project exists, so the precedent is there.
9. **Logger-capture helpers.** Two shapes, both justified by their project. Not duplicates. Not a finding.
10. **Sync and async.** No `GetResult`, no `async void`. Consistent. The `.Result` name matches go to lens 1.

## 3. Gaps

- **No written rule.** No tracked document sets logging, time, HTTP, namespace or helper conventions (`AGENTS.md:216-218`; `docs/` has no such decision; ADR-0003 is superseded). Each "drift" is a judgement from uniformity, not a breach of a stated rule. The owner must decide and document each convention before a fix can be checked against it.
- **Runtime listeners.** A host could add a Trace listener outside tracked files. Not checkable read-only. Row 1 assumes none [INFERENCE].
- **Dead code.** Row 6 (unconstructed HttpClient consumers) and row 2 (unread `UtcNow`) point to unused members. Not claimed as dead code here: no call-graph tool was run. The dead-code check recorded at Phase 25 is owned by lens 3.
- **Namespace full count.** The 30-file sample is one of 638 `src/` files. The per-folder counts above cover the folders named, not the whole tree. Whether `dotnet format --verify-no-changes` fails on suggestion-level IDE0130 was not run [INFERENCE: it does not].
- **Sync and async.** Only `Task.Run`, `GetAwaiter().GetResult()` and `async void` were scanned here. The five `.Result`/`.Wait()` name matches are not classified (lens 1).
- **Scope.** `tests/` time reads and `services/` were not scanned for pattern drift. Their helper and `Task.Run` counts appear only as stated.
- **Not run.** No build, analyzer, formatter or test, per the phase rules. Compilation was not used to confirm any finding.

## 4. Method

I read the QUALITY_BAR document and the phase plan to fix the item's scope (`docs/quality/quality-bar.md`; `25.1-01-PLAN.md`), then ran each requested scan over the stated scope with `git grep` counts, and extra patterns where the requested one missed a real form (the object-initializer `new HttpClient {` and the time-fake family). I read the code around every hit I classified as a finding or as a deliberate exception: the `Task.Run` comments, the `TimeProvider` constructors, the Trace and ILogger call sites, the Blazor host, the workflow seam and its ADR. For the namespace check I took a fixed stride sample of 30 `src/` files and read each declaration; then I counted each affected folder in full to see whether a mismatch was isolated or a convention. I classified each mixed convention as deliberate and documented, undocumented but uniform, or drift, and recorded the reason in section 2.8.
