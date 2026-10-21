---
phase: "4"
slug: "surfaces"
status: partial
nyquist_compliant: false
wave_0_complete: true
created: "2026-10-21"
---

# Phase 4 — Validation Strategy

Scope: the 9 requirements that ROADMAP.md (Phase 4 `Requirements:` line) and the REQUIREMENTS.md traceability table assign to Phase 4: UI-01..04, WEB-01..02, DESK-01, MOB-01, SHELL-01.

## Compliance

- `nyquist_compliant: false`. 3 of 9 requirements are COVERED (UI-01, UI-02, UI-04). 5 are PARTIAL and 1 is a GAP (SHELL-01). Each uncovered clause is listed in the Gap Register below.
- A requirement is COVERED only when every clause of its text has an automated command and each named test exists in `tests/`.
- Test names were confirmed by grep of `tests/**/*.cs`. Commands were **not executed** in this audit. A filter that matches zero tests must be treated as a failure, not a pass.
- The MAUI head (`apps/OmpOS.App`) is excluded from `OmpOS.Linux.slnf` and is never built on Linux. Its clauses cannot be covered by a Linux test run.

## Requirement Coverage

| Req | Status | Automated command | Confirmed test names (grep-verified) |
|---|---|---|---|
| UI-01 | COVERED | `dotnet test tests/OmpOS.UI.Tests --filter "FullyQualifiedName~UiBlockRendererTests\|FullyQualifiedName~MarkdownSanitizationTests\|FullyQualifiedName~ConversationListTests\|FullyQualifiedName~PromptAcceptedFoldTests.UserMessageStaysBeforeAssistantOutput\|FullyQualifiedName~AccessibilityTests.EnterSendsShiftEnterDoesNot"` | `UiBlockRendererTests`: `RendersMarkdownText`, `RendersCodeBlockWithCopyControl`, `RendersToolCallCardWithStatus`, `RendersPermissionOptionsVerbatimReadOnly`, `PermissionBlockShowsNoOutcomeWithoutPayloadOutcome`, `RendersPlanPanel`, `RendersDiffPlaceholder`, `RendersErrorBlock`, `RendersA2uiSubset`, `RemoteA2uiImageRendersFallbackLink`, `RendersUnknownCollapsed`; `MarkdownSanitizationTests`: `BoldRendersStrong`, `ScriptSourceIsEscaped`, `LinkHrefAllowlist`, `FencedCodeRendersPreAndCode`, `HttpsImageRendersFallbackLink`, `DataPngImageRendersImg`, `DataSvgImageRendersNoImg`; `ConversationListTests`: `RendersTitlesWithSelection`, `SelectingReportsIdentifier`, `EmptyListRendersEmptyPrompt`; `PromptAcceptedFoldTests.UserMessageStaysBeforeAssistantOutput`; `AccessibilityTests.EnterSendsShiftEnterDoesNot` (composer send/newline) |
| UI-02 | COVERED | `dotnet test tests/OmpOS.UI.Tests --filter "FullyQualifiedName~StreamingStateTests\|FullyQualifiedName~ApprovalStateTests"` | `StreamingStateTests`: `TextDeltaStreamsWithStopAffordance` (incremental deltas and stop), `RetryableErrorPreservesPartialOutputWithRetry`, `FatalErrorPreservesPartialOutputWithoutRetry`, `CompletionMovesToSuccess`, `HistoryLoadResolvesEmptyOrSuccess` (loading history, empty, success), `TransportStatesKeepOutputVisible` (reconnecting, offline), `ScrollPauseFlagToggles`, `AnnouncementsThrottleToStateChanges`, `AffordancesRenderInRightStates`, `JumpToLatestAppearsWhenScrollPaused`, `PermissionEventNeverEntersAwaitingApproval`; `ApprovalStateTests`: `MarkAwaitingApprovalSetsStateAndAnnouncement`, `AwaitingApprovalRendersNoticeWithoutActions`, `TransportStatesRenderNotices` |
| UI-03 | PARTIAL | `dotnet test tests/OmpOS.UI.Tests --filter "FullyQualifiedName~AccessibilityTests\|FullyQualifiedName~StreamingStateTests.AnnouncementsThrottleToStateChanges"` | `AccessibilityTests`: `EnterSendsShiftEnterDoesNot` (Enter send, Shift+Enter newline), `EscapeStopsWhileStreaming` (Esc stop), `IconButtonsHaveAccessibleNames`, `LiveRegionIsPresent` (`aria-live="polite"`), `BundledCssHasFocusAndMotionRules` (substring checks for `:focus-visible`, `prefers-reduced-motion`, `44px`); `StreamingStateTests.AnnouncementsThrottleToStateChanges`. Gap: see Gap Register |
| UI-04 | COVERED | `dotnet test tests/OmpOS.UI.Tests --filter "FullyQualifiedName~ReplayTreeTests\|FullyQualifiedName~PromptAcceptedFoldTests.PromptAcceptedFoldIsPure"` | `ReplayTreeTests.FoldTwiceProducesIdenticalTrees`, `RefoldRendersIdenticalMarkup` (identical block trees and markup), `MalformedPayloadShapesNeverThrow`, `UnrecognizedKindIsKeptAsUnknownBlock` (unknown events never blank or crash); `PromptAcceptedFoldTests.PromptAcceptedFoldIsPure` |
| WEB-01 | PARTIAL | `dotnet test tests/OmpOS.Web.Tests --filter "FullyQualifiedName~ApiBaseUrlResolverTests\|FullyQualifiedName~SseEventStreamTests\|FullyQualifiedName~SseParserTests\|FullyQualifiedName~ApiSessionClientTests\|FullyQualifiedName~SessionListEventsTests\|FullyQualifiedName~PublishSmokeTests"` | `ApiBaseUrlResolverTests`: `QueryOverrideWinsOverConfiguration`, `ConfigurationIsUsedWithoutOverride`, `OtherQueryParametersAreIgnored`, `MissingBaseUrlThrowsNamingTheKey`, `GarbageOverrideThrows`, `NonLoopbackHttpThrows`, `LoopbackHttpIsAccepted`; `SseEventStreamTests`: `FirstEventYieldsBeforeStreamCompletes` (response streaming), `InitialSinceQueryIsSent`, `ReconnectCarriesLastEventIdAndResumesWithoutGaps`, `DuplicateSeqIsDropped`, `TerminalEventStopsWithoutReconnect`, `CommentLinesIgnoredAcrossSplitChunks`, `ReconnectBoundThrowsAfterMaxAttempts`, `TerminalPayloadWithoutEventFieldStops`; `SseParserTests`: `FramesSplitAcrossChunksParseExactlyOnce`, `CommentLinesAreIgnored`, `BlankLineWithNoPendingFieldsDispatchesNothing`, `MultilineDataJoinsAndTrailingEventFlushes`, `FieldSpacingAndUnknownFields`; `ApiSessionClientTests`: `CreateSessionPostsOwnerAndReturnsSession`, `ListSessionsReturnsAll`, `GetSessionReturnsNullOnNotFound`, `SubmitPromptPostsToPromptsEndpoint`, `ApiErrorsThrowApiClientException`; `SessionListEventsTests.ListSessionsDecodesTextDeltaEvents`; `PublishSmokeTests.PublishProducesServableWwwroot`. Gap: browser smoke run is manual (see Gap Register) |
| WEB-02 | PARTIAL | `dotnet test tests/OmpOS.Web.Tests --filter "FullyQualifiedName~VercelConfigTests\|FullyQualifiedName~PublishSmokeTests"` | `VercelConfigTests.VercelConfigHasRewritesAndHeadersWithoutRoutes` (rewrites, CSP and security headers, no `routes` key, `outputDirectory`), `VercelConfigBuildUsesPinnedSdkAndWebProject` (`installCommand` references `global.json`; `buildCommand` runs `./.dotnet/dotnet publish`); `PublishSmokeTests.PublishProducesServableWwwroot`. Gap: live Vercel deployment is not automated (see Gap Register) |
| DESK-01 | PARTIAL | `dotnet test tests/OmpOS.EngineHost.Tests --filter "FullyQualifiedName~EngineHostTests.InProcessHostReportsInProcessKindByDefault\|FullyQualifiedName~EngineHostTests.InProcessHappyPathStreamsAdapterScript\|FullyQualifiedName~EngineHostTests.DescriptorsAreHonest\|FullyQualifiedName~EngineHostTests.NoOpWindowStateStoreLoadsNullAndAcceptsSaves\|FullyQualifiedName~EngineHostTests.NoOpSingleInstanceGuardAlwaysAcquires\|FullyQualifiedName~EngineHostTests.NoOpSessionRecoveryListsNothing"` | `EngineHostTests.InProcessHostReportsInProcessKindByDefault`, `InProcessHappyPathStreamsAdapterScript`, `DescriptorsAreHonest`, `NoOpWindowStateStoreLoadsNullAndAcceptsSaves`, `NoOpSingleInstanceGuardAlwaysAcquires`, `NoOpSessionRecoveryListsNothing`. Gap: see Gap Register |
| MOB-01 | PARTIAL | `dotnet test tests/OmpOS.EngineHost.Tests --filter "FullyQualifiedName~EngineHostTests.RemoteHostReportsRemoteKindByDefault\|FullyQualifiedName~EngineHostTests.RemoteResumeReplaysMissedEventsExactlyOnce\|FullyQualifiedName~EngineHostTests.DescriptorsAreHonest"` | `EngineHostTests.RemoteHostReportsRemoteKindByDefault`, `RemoteResumeReplaysMissedEventsExactlyOnce` (sequence resume without duplicates), `DescriptorsAreHonest`. Gap: see Gap Register |
| SHELL-01 | GAP | None. No existing test or test-project command asserts this clause. | No test references `OmpOS.Linux.slnf`, and no test asserts that `apps/OmpOS.App` is excluded from it. `tests/OmpOS.ArchitectureTests/ArchUnitLayerTests.cs:69-73` only tolerates the App assembly being absent. See Gap Register |

## Manual-Only Verifications

| Req | Reason |
|---|---|
| WEB-01 (browser smoke) | The clause "a browser smoke run completes the vertical slice, with the first block visible before stream end" is not automated. Evidence: `docs/runbooks/evidence/web-vertical-slice-smoke.webp` (exists, 12.2K) and the section `## Vertical slice: web head against the API (WEB-01)` in `docs/runbooks/local-dev.md` (line 42). Those are manual records, not a test run. |
| UI-03 (contrast and axe) | WCAG AA contrast and an axe-core run require human review or tooling that is not present in the repository (see Gap Register). |
| DESK-01, MOB-01, SHELL-01 (MAUI head) | Built and verified only in the macOS CI `shells` job, which is human-deferred (`README.md:58`; first run pending in `docs/handoff.md`). Not runnable on Linux. |
| WEB-02 (live deploy) | Deployment to Vercel is an external step. Its build-image details are an open question in VERIFY-02. |

## Gap Register (requirement clauses without an automated test; orchestrator to decide)

| Req | Missing | Evidence |
|---|---|---|
| UI-03 | WCAG AA contrast tokens are not tested. No test asserts a contrast ratio. | `grep -i "contrast\|wcag" tests` returns no matches. |
| UI-03 | No axe-core run exists. Neither a package nor a test references axe. | `grep -i "axe"` over `*.cs`, `*.json`, `*.csproj`, `*.ts` and `*.js` (excluding `obj/` and `bin/`) returns no matches. |
| UI-03 | `BundledCssHasFocusAndMotionRules` checks only that the CSS contains the strings `:focus-visible`, `prefers-reduced-motion` and `44px`. It does not verify that the focus ring, motion rule or 44px target applies to any control. | `tests/OmpOS.UI.Tests/AccessibilityTests.cs:77-86`. |
| WEB-01 | The end-to-end browser smoke (prompt → parser → session → in-memory adapter → SSE → rendered blocks, first block before stream end) has no automated run. Only manual evidence exists. | `docs/runbooks/evidence/web-vertical-slice-smoke.webp`; `docs/runbooks/local-dev.md:42`. |
| WEB-02 | Live deployment to Vercel (install, publish, static output served, headers observed in response) is not automated. Only the `vercel.json` shape and a local publish are tested. | `tests/OmpOS.Web.Tests/VercelConfigTests.cs`, `PublishSmokeTests.cs`. |
| DESK-01 | No test asserts that the desktop registration spawns `omp acp` over stdio. `EngineHostTests` states in its header that no test spawns `omp`. The desktop branch wires `InProcessEngineHost.ForAcp(new OmpAcpClient(), new AcpClientOptions())`, which is not exercised by any test. | `apps/OmpOS.App/MauiProgram.cs:42`; `tests/OmpOS.EngineHost.Tests/EngineHostTests.cs` header. |
| DESK-01 | The Windows and MacCatalyst no-op seams (`apps/OmpOS.App/Platforms/*/Platform/MauiProgram.*.cs`) are not compiled or tested on Linux. The alternative remote-backend path for desktop has no App-level test. | `apps/OmpOS.App/Platforms/Windows/Platform/MauiProgram.Windows.cs:11-13`; `apps/OmpOS.App/Platforms/MacCatalyst/Platform/MauiProgram.MacCatalyst.cs:11-13`. |
| MOB-01 | The `Window.Resumed` handler that resumes the SSE stream by sequence is not tested. | `apps/OmpOS.App/App.xaml.cs:26, 30`. |
| MOB-01 | The mobile branch (Android or iOS → `RemoteEngineHost`) is not tested. No test asserts that mobile never spawns a process. | `apps/OmpOS.App/MauiProgram.cs:37-39`. |
| SHELL-01 | No automated check asserts that `OmpOS.Linux.slnf` excludes `apps/OmpOS.App`, or that the root README states which heads are verified where. Current evidence is by inspection only: the `OmpOS.Linux.slnf` project list has no `apps/OmpOS.App` entry, `.github/workflows/ci.yml` has a macOS `shells` job, and `README.md:52-59` has a "What is verified where" table. The macOS job is human-deferred. | `OmpOS.Linux.slnf`; `.github/workflows/ci.yml`; `README.md:52-59`. |

## Notes

- Commands here were not run (no `dotnet test` in this audit). Before this file can move to `validated`, run each command and confirm each reports a non-zero match count and the named tests pass.
- Test counts are not asserted by any command in this file. Counts in the prior sign-off (UI 91, Web 29, EngineHost 10) were not re-verified here.
- `nyquist_compliant` stays `false` until the five PARTIAL requirements and the SHELL-01 GAP in the Gap Register are closed or the orchestrator accepts them explicitly. Items that depend on macOS or on a human browser run cannot be closed by a Linux test run alone.
