# Lens 1: Correctness and error handling, plus test quality

Phase 25.1, read-only. Repository `/root/src/repos/ompos`, commit 59e9dd1, branch `feat/v0.9.2-hardening`.
QUALITY_BAR items in scope: **Correctness**, **Unbounded work**. Test quality is folded into Correctness (25.1-CONTEXT.md).
Scope: tracked files from `git ls-files` (searched with `git grep`) under `src/`, `apps/`, `tests/`. Excludes `bin/`, `obj/`, and `apps/OmpOS.Web/wwwroot/lib/`.
No build, test, formatter, or package-manager run.

## 1. Findings

Total found: **34** (High 2, Medium 12, Low 20). None omitted. Ordered by severity.

| file:line | QUALITY_BAR item | severity | disposition | evidence (120 characters or fewer) |
|---|---|---|---|---|
| src/OmpOS.Harness.Acp/AcpStdioTransport.cs:247 | Unbounded work | High | fix | ACP stdout read by ReadLineAsync with no line cap; RPC caps 1 MiB at OmpRpcProcess.cs:114; no ACP cap test |
| apps/OmpOS.Web/Services/SseEventStream.cs:81 | Correctness | High | fix | Reconnect count never resets on progress (ConnectionOutcome tracks Terminal only); 10 drops throw on a live stream |
| src/OmpOS.EngineHost/RemoteEngineHost.cs:343 | Unbounded work | Medium | fix | Frame line read uncapped (ReadLineAsync); data StringBuilder (:337) has no frame cap; no shipped caller yet |
| src/OmpOS.EngineHost/RemoteEngineHost.cs:347 | Correctness | Medium | fix | Transport drop breaks silently; SubmitPromptAsync ends with no terminal event or error; no in-tree caller checks |
| src/OmpOS.Harness.Herdr/HerdrSessionService.cs:244 | Correctness | Medium | fix | Failed compensating TerminateAsync swallowed with no log or flag; the unrecorded remote session may keep running |
| src/OmpOS.Native.Views/Views/RunControls/ToolApprovalRowView.axaml.cs:94 | Correctness | Medium | fix | Decision fault dropped when the row is detached: PostWhileAttached (:125) returns without action or log |
| src/OmpOS.Harness.Rpc/OmpVersionProbe.cs:31 | Unbounded work | Medium | fix | omp --version output read to end, no cap; the launcher probe caps at 4096 chars (OmpLauncher/OmpVersionProbe.cs:15) |
| src/OmpOS.Sandbox/AgentOsProvider.cs:400 | Unbounded work | Medium | fix | Vendor response body read with ReadAsStringAsync and no size cap |
| src/OmpOS.Sandbox/RailwaySwarmProvider.cs:141 | Unbounded work | Medium | fix | Vendor response body read with ReadAsStringAsync and no size cap |
| tests/OmpOS.Sandbox.Tests/SandboxProviderContractTests.cs:134 | Correctness | Medium | fix | Timeout_IsHonoured asserts nothing; catches at :147 and :151 accept any outcome, so it cannot fail on timeout |
| tests/OmpOS.Sandbox.Tests/SandboxProviderContractTests.cs:114 | Correctness | Medium | fix | StatusTransitions_CoverEnum asserts no status and no enum value; its name claims coverage it does not give |
| tests/OmpOS.Native.Tests/RunControls/DetachedSettlementTests.cs:37 | Correctness | Medium | fix | Fixed 200 ms sleep, then a negative StatusText check; a fault posted after 200 ms passes unseen |
| tests/OmpOS.Web.Tests/SseEventStreamTests.cs:118 | Correctness | Medium | fix | Bound test uses five zero-progress failures, so it cannot tell a cumulative reconnect budget from a consecutive one |
| src/OmpOS.UI/Components/Blocks/CodeBlockView.razor:29 | Correctness | Medium | fix | Clipboard failure swallowed (:29-31); OnCopy still fires at :33, so the caller is told the copy succeeded |
| src/OmpOS.Coordination.Dragonfly/DragonflySessionSignals.cs:94 | Correctness | Low | accept | Per-subscriber delivery failure dropped; waiters poll at SessionService.cs:491, so a missed wake is late, not lost |
| tests/OmpOS.Coordination.Tests/InMemorySessionSignalsTests.cs:71 | Correctness | Low | accept | PublishWithoutSubscribersSucceeds has no assertion; it passes only if nothing throws |
| tests/OmpOS.Sandbox.Tests/RailwaySwarmProviderTests.cs:204 | Correctness | Low | fix | Name says it completes without network, but nothing checks that no request was sent; no assertion |
| tests/OmpOS.Sandbox.Tests/SandboxLifecycleTests.cs:184 | Correctness | Low | accept | Teardown_Idempotent_NotFoundIsSuccess has no assertion; it passes only if nothing throws |
| tests/OmpOS.Harness.ContractTests/AcpTransportContractTests.cs:46 | Correctness | Low | accept | CloseIsIdempotent has no assertion; it passes only if the second close does not throw |
| tests/OmpOS.EngineHost.Tests/EngineHostTests.cs:324 | Correctness | Low | accept | CancelWithoutPromptCompletes has no assertion; it passes only if neither cancel throws |
| tests/OmpOS.Api.IntegrationTests/SseWatcherWakeTests.cs:44 | Correctness | Low | fix | Fixed 250 ms sleep before the append; replay of a late subscriber can pass without the wake path running |
| tests/OmpOS.Harness.Plugins.Tests/SystemHookProcessRunnerTests.cs:94 | Correctness | Low | accept | Negative File.Exists check after a fixed 4 s sleep; a pass shows only that no marker appeared in that window |
| src/OmpOS.Harness.Workspace/SetupProcess.cs:130 | Correctness | Low | fix | Win32Exception on kill copies the "exited between check and kill" comment; a kill that could not signal is misdescribed |
| src/OmpOS.Sandbox/SandboxLifecycle.cs:85 | Correctness | Low | accept | Non-cancellation provider failures become ProvisionTimeoutException; documented at :69; no caller branches on the type |
| src/OmpOS.Harness.Plugins/ProcessTree.cs:179 | Correctness | Low | accept | Win32Exception on signal is swallowed; a descendant may survive. Documented as not owned or already gone |
| src/OmpOS.Harness.Herdr/HerdrSessionStore.cs:287 | Correctness | Low | accept | Temp-file delete failure swallowed by design (comment :279-280); the store never reads the leftover path |
| src/OmpOS.Harness.Acp/AcpAgentSession.cs:237 | Correctness | Low | accept | Disposal catch-all with a comment; the pump records the loss (comment :239). Documented |
| src/OmpOS.Sandbox/ProcessRunner.cs:531 | Unbounded work | Low | accept | Freeze loop bounded by KillWaitBudget for unfrozen members; Thread.Sleep(1) at :541 blocks the kill path |
| tests/OmpOS.Native.Tests/Editor/HighlightingTests.cs:96 | Correctness | Low | accept | Thread.Sleep on the UI test thread inside a TokenWaitLimit-bounded poll; bounded, but blocks the dispatcher |
| tests/OmpOS.Sandbox.Tests/ProcessRunnerCancelDuringSpawnRegressionTests.cs:54 | Correctness | Low | accept | Fixed 9 s wait, then empty-directory check, after 800 forked subshells; slow, and a pass shows only no marker |
| tests/OmpOS.Harness.Rpc.Tests/RpcLiveSmokeTests.cs:29 | Correctness | Low | accept | Live omp test is skip-gated on omp being on PATH; a host without omp reports skipped, not run |
| src/OmpOS.Harness.Worktrees/GitProcess.cs:76 | Unbounded work | Low | defer | git stdout and stderr read to end with no cap; output size scales with repo state. Local tool output |
| apps/OmpOS.Web/Services/ApiSessionClient.cs:108 | Unbounded work | Low | accept | Error-body read has no cap; the Justification at :95 records it as best-effort on the error path only |
| src/OmpOS.Native.Views/Engine/EngineRunInput.cs:16 | Unbounded work | Low | accept | _held has no cap of its own; admission is bounded by RunInputScheduler.MaxQueuedMessages (RunInputScheduler.cs:187) |

## 2. Scans

### 2.1 Pattern hit counts

Counts are from `git grep` over tracked files. Build output under `obj/` is excluded. An unscoped grep counted generated `obj/` files and was discarded.

| Pattern | Scope | Hits | Result |
|---|---|---|---|
| `catch\s*(\(\|\{)` | src/ 207, apps/ 19 (includes .razor), tests/ 39 | **265** | 56 swallowed, 88 filtered, 121 surfaced (2.2) |
| `async void` | src/, apps/, tests/ | **0** | None |
| `\.Result\b\|\.Wait\(\)\|GetAwaiter\(\)\.GetResult\(` | src/, apps/, tests/ | **17** | 0 blocking calls on a Task. All 17 are record or property `.Result` (2.3) |
| `ContinueWith\(` | src/, apps/, tests/ | **15** | All 15 observe the fault (2.4) |
| `Thread\.Sleep\|Task\.Delay\(` | tests/ | **61** | 13 poll loops with a deadline, 7 blocking stubs, 37 fixed sleeps under 1 s, 4 fixed sleeps of 1 s or more (2.5) |
| `Skip\s*=\|\[Ignore\|Assert\.Skip\|Assert\.Inconclusive` (plus `SkipUnless` on the same lines) | tests/ | **32** | 26 platform or tool gates, 5 live-omp or environment gates, 1 not categorised (2.6) |
| `while \(true\)\|for \(;;\)` | src/, apps/ | **13** | All 13 have an exit condition (2.7) |

Supporting scan, not in the spec: `ReadAsStringAsync\|ReadAsStreamAsync\|ReadToEndAsync\|ReadBoundedAsync\|BoundedText` over src/ and apps/. Hits relevant to the findings were read. Capped readers: OmpLauncher OmpVersionProbe.cs:52-53 (4096 chars), SystemProcessRunner.cs:40-41 (MaxOutputCharacters), ProcessRunner (1 MiB). Uncapped readers are rows 3, 7, 8, 9, 32, 33. One further uncapped read, RemoteEngineHost.cs:600 (session-create JSON body from our own API), is not a finding.

### 2.2 Catch classification (265 pattern hits)

Rule, applied to each block body: **swallowed** = body empty or comment-only (regex-verified: 56). **filtered** = a `when` filter or an expected typed exception (cancellation, disposed, exited-process race, JS disconnected) with a benign body (return, continue, break, assignment, or dispose). **surfaced** = everything else: rethrow, wrap, log, status or transcript text, or a typed result. The filtered/surfaced split (88/121) is a body heuristic. Spot-checked bodies are cited in the findings.

Swallowed, all 56, grouped by stated reason:

- **A. Kill, exit, or stream-close races** (comment states the process or pipe had already gone): ProcessRunner.cs:489, 493, 497, 802, 806, 810, 814, 828, 832; ProcessTree.cs:39, 171, 175, 179 (row 25); OmpRpcProcess.cs:166, 227, 246; OmpSession.cs:53; OmpVersionProbe.cs:72 (Rpc) and :97 (Launcher, after kill, timeout result stands); SystemProcessRunner.cs:90; PtyCore.cs:179; PtyProcessRunner.cs:142; GitProcess.cs:129; AcpStdioTransport.cs:310, 343; SetupProcess.cs:126, 130 (row 23). 27 in total.
- **B. Best-effort disposal, documented**: AcpAgentSession.cs:224, 237 (row 27); OmpRpcSession.cs:339; OmpRpcClient.cs:86 (start failure stays authoritative); RemoteEngineHost.cs:174 (stream finished concurrently). 5 in total.
- **C. Cancellation, timeout, or stop, documented as not an error**: Index.razor:254 (user Stop); InlineCompletionBox.axaml.cs:153 (newer edit replaces request); InMemoryOmpSession.cs:311 (timeout is a denial); AcpAgentSession.cs:724 (timeout or cancel is a denial, fails closed); AcpStdioTransport.cs:298 (diagnostics only), :325 (grace period, then kill). 6 in total.
- **D. Expected absence or non-JSON fallback**: HerdrSessionStore.cs:43, 47 (no store file yet); UiFolder.cs:234 and HarnessEventProjection.cs:293 (payload not JSON, so no verbatim options by design). 4 in total.
- **E. Intentional and documented, with a stated reason**: AcpAgentSession.cs:1278 (connection already gone; the pump fails the turn); OmpRpcSession.cs:702 (no run to abort); WorkspaceCatalog.cs:128 (loader reports the path); HerdrSessionStore.cs:287, 290 (temp file left behind, row 26); CodeBlockView.razor:29 (see row 14, a flagged case in its effect, not in its intent); DragonflySessionSignals.cs:94 (row 15); SessionView.razor:149 (circuit gone); LiveCaptureReplay.cs:121 (expected cancel); OmpLiveVariantContractTests.cs:94 (expected close). 10 in total.
- **F. Not accepted as written**: HerdrSessionService.cs:244, 248 (row 5). Compensation failure is swallowed and no orphan is recorded. 2 in total.
- **G. Test code, not accepted as written**: SandboxProviderContractTests.cs:147, 151 (row 10). 2 in total.

Total swallowed: 27 + 5 + 6 + 4 + 10 + 2 + 2 = **56**.

Surfaced sample, checked by body: ComposerView.axaml.cs:207 (refusal returned as a faulted task, shown on the status line). SessionRunActivities.cs:285-292 (SettleSkipped or SettleFailed logs). DirectSessionRunner.cs:60-69 and 241 (state set to Failed or Cancelled, FailureDetail set). TerminalGate.cs:177-188 (FailedToRun). MainView.axaml.cs:312-318 (fault shown under its turn). SandboxLifecycle.cs:85 (row 24, a wrap).

Bare `catch` lines (no `(` or `{` on the same line; not matched by the pattern; 12 found): InProcessEngineHost.cs:162, HerdrSessionService.cs:227, McpClientConnector.cs:68, RpcHarnessSession.cs:134, OpenShellProvider.cs:192, 218, PostgresSessionLock.cs:70, PostgresSessionRepository.cs:235, 261, FixtureHttpProcess.cs:53, FixtureRegistry.cs:23, PodmanPostgresContainer.cs:64. Each body records to a status list or disposes and rethrows. I read 10 in full. McpClientConnector.cs:68 and RpcHarnessSession.cs:134 continue past the lines I read; their first lines are cleanup, and they are not swallowed.

### 2.3 Sync over async (17 hits)

None blocks on a Task. The 17 hits are `.Result` on records or properties: AcpAgentSession.cs:1391 (`AcpFrames.Inbound` property); InlineCompletionBox.axaml.cs:84, 187, 219 (`ShownSuggestion` record property); PromptParser.cs:119 (synchronous method returning a recognizer result, not a Task); AcpFrameReplayTests.cs:96; RecordedRpcCapture.cs:95, 100, 110; DiffReviewViewTests.cs:43, 59, 75, 92, 107, 111, 127, 160 (static `DiffHarness.Result(window)`). No `.Wait()` and no `GetAwaiter().GetResult()` in tracked src, apps, or tests.

### 2.4 ContinueWith (15 hits)

Each continuation observes the fault, either by reading `.Exception` or through a sibling continuation with `OnlyOnFaulted`:

- TaskFault.cs:20: observes every end state and returns the exception.
- PtyTerminalSession.cs:47 (OnlyOnFaulted reads it). :139 does not read it, but the :47 handler observes the same task.
- ProcessRunner.cs:840: OnlyOnFaulted, returns `t.Exception`.
- MainView.axaml.cs:267, 703, 714: OnlyOnFaulted, reads `Exception`.
- ComposerView.axaml.cs:223 (OnlyOnFaulted, reads). :235 has no read, and :223 observes the fault.
- InlineCompletionBox.axaml.cs:302 (Observe). TranscriptExportView.axaml.cs:106. TerminalPanelView.axaml.cs:144: all OnlyOnFaulted and read `Exception`.
- ToolApprovalRowView.axaml.cs:94: reads `finished.Exception`, but drops it when the row is detached (row 6).
- FakeOmpAgentAdapter.cs:85: SettleTurn reads `pump.Exception` (line 162).
- WebSocketOmpTransportStubContractTests.cs:86: reads `task.Exception`.

### 2.5 Sleeps in tests (61 hits)

- **13 poll loops**, each with a deadline or a cancellation token. Fine.
- **7 blocking stubs**: `Task.Delay(Timeout.Infinite, ct)` in stub transports and hooks (for example RunWorktreeIsolationTests.cs:286, 296; HookRunnerTests.cs:267). Fine.
- **37 fixed sleeps under 1 s**. Used as synchronisation. Flagged where the outcome depends on the timing: SseWatcherWakeTests.cs:44 (row 21); SseResumeTests.cs:108 (same pattern, replay masks the wake); DetachedSettlementTests.cs:37 (row 12); SessionRepositoryContractTests.cs:282 and :302 (negative `IsCompleted` after 50 ms; a slow lock cannot make it fail).
- **4 fixed sleeps of 1 s or more**, all negative or window checks: DragonflyCoordinationTests.cs:90 (1.5 s, then a window check); SystemHookProcessRunnerTests.cs:94 (4 s, row 22); ProcessRunnerCancelDuringSpawnRegressionTests.cs:54 (9 s, row 30); ProcessRunnerCancellationRegressionTests.cs:49 (4 s, then `File.Exists(marker).ShouldBeFalse()`; same pattern as row 22, a surviving child would have written by then). Accepted with the reasons in the rows.
- `Thread.Sleep` in tests: HighlightingTests.cs:96 (row 29).

### 2.6 Skip markers (32 hits)

26 platform or tool gates: POSIX shell or Linux checks, `sh`, `head`, `tr`, `printf`, `/bin/printf`, and podman. docs/quality/quality-bar.md notes that CI has `sh`, `head`, and `tr`, so these run in CI and skip only on hosts without the tools. Accepted.
5 live-omp or environment gates: AcpLiveSmokeTests.cs:25; RpcLiveSmokeTests.cs:29 and :56 (row 31); RpcLiveCaptureTests.cs:46 (env `OMPOS_RPC_LIVE_CAPTURE`); PostgresTwoProcessAppendTests.cs:43 (env connection string). Accepted; these are opt-in.
1 not categorised by the heuristic. Not reviewed further.

### 2.7 Unbounded loops, src/ and apps/ (13 hits)

| file:line | Exit condition | Verdict |
|---|---|---|
| apps/OmpOS.Web/Services/SseEventStream.cs:67 | Terminal event, or reconnects reach MaxReconnects (default 10); a null option means unbounded | Bounded by default. Counter is cumulative (row 2) |
| src/OmpOS.EngineHost/RemoteEngineHost.cs:207 | Lease free, or cancellation through the waiter's token | Bounded by caller token |
| src/OmpOS.EngineHost/RemoteEngineHost.cs:338 | Null line, or transport exception breaks | Bounded per connection; frame size uncapped (row 3) |
| src/OmpOS.Harness.Acp/AcpStdioTransport.cs:106 | EOF (null line); exit raised | Bounded; line size uncapped (row 1) |
| src/OmpOS.Harness.Agent/OmpEventLog.cs:126 | Log completed and drained, or cancellation | Bounded; waits are cancellable |
| src/OmpOS.Harness.InMemory/InMemoryOmpSession.cs:72 | Cancelled, errored, or no pause segment | Bounded by Produce (test double in src) |
| src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:85 | Stream completed; a partial frame over 1 MiB throws (:114-116) | Bounded |
| src/OmpOS.Harness.Terminal/PtyCore.cs:139 | Read returns 0, IOException, or ObjectDisposedException | Bounded |
| src/OmpOS.Native.Views/MainView.axaml.cs:306 | No held message, stop, or fault. Admission capped at 16 (RunInputScheduler.cs:23, 187) | Bounded. Follow-ups dispatch one per turn end (RunInputScheduler.cs:140-157), taken by TakeNext before the next admission |
| src/OmpOS.Sandbox/ProcessRunner.cs:99 | EOF, spec.Timeout, or cancellation | Bounded |
| src/OmpOS.Sandbox/ProcessRunner.cs:165 | EOF, or a line completes | Bounded by EOF and cancellation. Per-line truncation flag exists; reader not read end to end (section 3) |
| src/OmpOS.Sandbox/ProcessRunner.cs:531 | Each unfrozen member bounded by KillWaitBudget; once all are stopped, no new forks | Bounded (row 28) |
| src/OmpOS.Sessions/SessionService.cs:475 | Decision recorded, record missing (throws), or lapse token (:446-452) | Bounded by the lapse timer |

Phase 25 claim checked: the 16-message cap at RunInputScheduler.cs:23 and :187 is in place. `EngineRunInput._held` (row 34) is not capped by itself, but admission is capped in front of it.

### 2.8 Test method scan (all test files with test attributes)

Scanned: 307 test files with `[Fact]`, `[Theory]`, `[Test]`, or `[TestMethod]` attributes. Parsed: 1839 test methods, by brace matching. Assertion patterns: `Should.*`, `.Should*(`, `Assert.*`, `Fail(`, `Throws*`, `ThrowsAsync`, `.ShouldBe*`. Helper calls (`*Assert*`, `Expect*`, `Verify*`, `Check*`, `Require*`, `Settle*`, `Wait*For*`) were checked separately.

Methods with no assertion call found: 15 flagged, reviewed:

- **9 real, no direct assertion**: rows 10, 11, 16, 17, 18, 19, 20, plus two with no row: `tests/OmpOS.Harness.InMemory.Tests/WebSocketOmpTransportStubTests.cs:34` (CloseOnUnconnectedStubSucceeds, passes only by not throwing; documented) and `tests/OmpOS.Sandbox.Tests/SandboxProviderContractTests.cs:95` (Teardown_IsIdempotent, passes only by not throwing; accepted).
- **3 not a finding**: `tests/OmpOS.Harness.Automations.Tests/AutomationGuardTests.cs:78, 84, 90` call `RefusesWhenStoreFaultsAsync`, which asserts `Outcome`, `Guard`, and `tally.Count` (AutomationGuardTests.cs:128-134).
- **2 not a finding**: `tests/OmpOS.UI.Tests/SessionScrollTests.cs:18, 33` assert through bUnit `module.VerifyInvoke`.
- **1 false positive**: `tests/OmpOS.Native.Tests/Editor/HighlightingTests.cs:19`, a `Main` string inside an `[InlineData]`.

Sampled test files (at least 25). Method bodies read: tests/OmpOS.Sandbox.Tests/SandboxProviderContractTests.cs; SandboxLifecycleTests.cs; RailwaySwarmProviderTests.cs; ProcessRunnerCancelDuringSpawnRegressionTests.cs; ProcessRunnerCancellationRegressionTests.cs; tests/OmpOS.Coordination.Tests/InMemorySessionSignalsTests.cs; tests/OmpOS.EngineHost.Tests/EngineHostTests.cs; tests/OmpOS.Harness.Automations.Tests/AutomationGuardTests.cs; tests/OmpOS.Harness.ContractTests/AcpTransportContractTests.cs; OmpLiveVariantContractTests.cs; tests/OmpOS.Harness.InMemory.Tests/WebSocketOmpTransportStubTests.cs; tests/OmpOS.Native.Tests/Editor/HighlightingTests.cs; tests/OmpOS.Native.Tests/RunControls/DetachedSettlementTests.cs; tests/OmpOS.UI.Tests/SessionScrollTests.cs; tests/OmpOS.Web.Tests/SseEventStreamTests.cs; tests/OmpOS.Harness.Plugins.Tests/SystemHookProcessRunnerTests.cs; tests/OmpOS.Sessions.Tests/SessionRepositoryContractTests.cs (lock tests).

Flagged or sampled lines read: tests/OmpOS.Api.IntegrationTests/SseWatcherWakeTests.cs; SseResumeTests.cs; DirectRunPermissionRoundTripTests.cs; SessionRunWorkflowTests.cs; SessionOwnerScopeTests.cs; GuardTests.cs; OtlpExporterSelectionTests.cs; RunWorktreeIsolationTests.cs; ToolApprovalEndpointTests.cs; tests/OmpOS.Sessions.Tests/ToolApprovalTests.cs; tests/OmpOS.Harness.Agent.Tests/FakeOmpAgentAdapter.cs; RecordedRpcCapture.cs; tests/OmpOS.Harness.Acp.Tests/AcpAgentSessionTests.cs; tests/OmpOS.Harness.Terminal.Tests/LinuxLocalPtyBackendTests.cs; tests/OmpOS.Harness.Rpc.Tests/RpcPromptCompletionTests.cs; tests/OmpOS.Native.Tests/Diff/DiffReviewViewTests.cs.

## 3. Gaps

- **No runtime evidence.** No build, test, or analyzer ran. Claims about flakiness, vacuous passes, and timing windows come from source and are not reproduced. A test that passes today can still fail under load.
- **Test scan is structural.** The method scan uses attributes and brace matching. A body with no assertion token is flagged. A test whose assertions sit in a helper I did not read to its end could be misclassified. The three AutomationGuard cases are the only helper-based ones verified to the end. The bUnit `VerifyInvoke` cases were not read to the end.
- **Catch split is heuristic.** The filtered/surfaced split (88/121) is a body heuristic. Only the groups and rows named above are reviewed individually.
- **SSE frame parser not read.** `apps/OmpOS.Web/Services/SseEventStream.cs` after :150 (frame and line reading) was not read, so any frame-size cap there is unverified. Row 2 concerns only the reconnect counter.
- **ProcessRunner line reader not read end to end.** The per-line cap at ProcessRunner.cs:165 was checked through its truncation flag and `FinishLine`, not through the whole `BoundedLineReader`.
- **Consumers not traced.** Whether the `OnCopy` consumer shows success text (row 14) and whether a detached-row fault is logged elsewhere (row 6) were checked only at the call sites in this lens.
- **Dead code** is not in this lens (25-CONTEXT.md; Phase 25 maintainability gap).
- **QUALITY_BAR text.** The items are named in 25-SUMMARY.md and docs/quality/quality-bar.md. No standalone definition of "Unbounded work" was found, so the severity rules in the assignment were applied directly.

## 4. Method

I started from the required scans, run as `git grep` over tracked files so generated `obj/` output could not inflate counts. For catch blocks, I classified every pattern hit by its body: a regex for empty or comment-only bodies, a body heuristic for filtered versus surfaced, and a reading of every swallowed group and of each candidate that could affect correctness. For unbounded work, I read the exit condition of each of the 13 loops in src and apps, followed each uncapped reader to its source of bytes (child process, network peer, or vendor API), and checked for a cap upstream or in a test. I checked two Phase 25 claims directly: the run-input cap and the RPC 1 MiB frame cap. For test quality, I parsed all 307 test files with attributes with a brace-matching script over method bodies, read each flagged method, and read the sampled files named in section 2.8. Findings are ranked by the severity rules in the assignment and capped at 40 rows; this register has 34.
