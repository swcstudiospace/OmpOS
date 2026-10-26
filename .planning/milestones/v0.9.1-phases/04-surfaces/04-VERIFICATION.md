---
phase: 04-surfaces
verified: 2026-10-21T00:00:00Z
status: human_needed
score: 7/9 requirements VERIFIED, 2 PARTIAL (DESK-01 and MOB-01: inspection-only, macOS binaries pending under H-1; no code missing)
mode: inspection-only for MAUI binaries (Linux cannot build them by design)
human_validation: accepted as tracked follow-ups by owner decision 8 (docs/handoff.md, 2026-10-21): macOS MAUI build (CI shells job), live Vercel deploy with production origin, screen-reader pass
---

# Phase 4 (Surfaces) Verification Report

**Phase goal** (`.planning/ROADMAP.md` Phase 4): a prompt typed into the web, desktop or
mobile surface renders as live, accessible Generative UI blocks streamed from the backend —
the full vertical slice runs in a real browser against the in-memory adapter with no omp,
Temporal, Railway or cloud credential.

**Method:** goal-backward; every status below cites a file path with a line number and a
test class or method name confirmed by grep in this session. No source, test, project,
config, or docs file was edited; no build, test, restore, format, publish, bootstrap, or
git-write command was run. Nothing is marked VERIFIED on the strength of a name alone.
Anything not confirmed by grep/read is marked PARTIAL/UNVERIFIED with the reason.

## Requirement table (one row per Phase 4 requirement ID)

| ID | Status | Evidence (file:line + test, all grep-confirmed) |
|----|--------|--------------------------------------------------|
| UI-01 | VERIFIED | Renderer sources exist and are substantive: `src/OmpOS.UI/Services/MarkdownRenderer.cs:20` (`MarkdownRenderer`, pipeline `:22`, allowlist remarks `:12-18`); `src/OmpOS.UI/Components/Blocks/MarkdownBlockView.razor:4` (sanitized HTML via `MarkdownRenderer.ToHtml`); `CodeBlockView.razor:8` (copy button `aria-label="Copy code"`) and `:10` (`pre`/`code`); `UserMessageView.razor:3-4` (user turn, `data-role="user"`); `src/OmpOS.GenUi/UserMessageBlock.cs:11` (`UserMessageBlock : UiBlock`) and `:17` (verbatim `Text`). Tests (classes confirmed by grep): `PromptAcceptedFoldTests` (`tests/OmpOS.UI.Tests/PromptAcceptedFoldTests.cs:14`), `MarkdownSanitizationTests` (`:10-11`), `SurfaceSafetyTests` (`SurfaceSafetyTests.cs:12`), `ConversationListTests` (`ConversationListTests.cs:9`), `UiBlockRendererTests` (`UiBlockRendererTests.cs:11`, one assertion per variant). |
| UI-02 | VERIFIED (with honest limitation, see Gaps G-1) | All states render: `SessionViewState.cs` defines `AwaitingApproval` (`src/OmpOS.UI/SessionViewState.cs:24`); `SessionView.razor:25` renders the approval notice, `:47-49` the jump-to-latest button, `:63-65` the `AutoScrollPaused` parameter; `SessionViewModel.cs:29` (`CanStop`), `:85`/`:88` (`PauseAutoScroll`/`ResumeAutoScroll`). Tests: `StreamingStateTests` (`tests/OmpOS.UI.Tests/StreamingStateTests.cs:15`), `ScrollPauseFlagToggles` (`:120`), `JumpToLatestAppearsWhenScrollPaused` (`:166-173`); `ApprovalStateTests` (`ApprovalStateTests.cs:11`), `MarkAwaitingApprovalSetsStateAndAnnouncement` (`:15`), `AwaitingApprovalRendersNoticeWithoutActions` (`:28`). LIMITATION (not a code defect): the approval UI is reachable only via `MarkAwaitingApproval`, which `apps/OmpOS.Web/Pages/Index.razor:129-130` calls only when the server `session.Status == SessionStatus.AwaitingToolApproval` — and no harness path ever sets that status (G-1). |
| UI-03 | VERIFIED (bUnit and axe-core run recorded 2026-10-21; see Re-verification) | bUnit evidence confirmed: `AccessibilityTests` (`tests/OmpOS.UI.Tests/AccessibilityTests.cs:11`); `EnterSendsShiftEnterDoesNot` (`:17`), `EscapeStopsWhileStreaming` (`:31`), `IconButtonsHaveAccessibleNames` (`:45`), `LiveRegionIsPresent` (`:65`), `BundledCssHasFocusAndMotionRules` (`:79-81`, reads the built CSS). Axe-core: 4.14.0 (WCAG 2.0 and 2.1, levels A and AA) run recorded in `docs/runbooks/local-dev.md` (section Accessibility run (UI-03)): zero violations in the empty and active states; two `incomplete` items on the icon-only send control (measured 18.26:1). H-2 closed. |
| UI-04 | VERIFIED | Pure fold confirmed: `SessionViewModel.cs:46` folds every event through `UiFolder.Fold(Tree, evt)`; replay-equality fixture `ReplayTreeTests` (`tests/OmpOS.UI.Tests/ReplayTreeTests.cs:16`, "fixed 23-event fixture" `:15`); fold-from-`prompt_accepted` covered by `PromptAcceptedFoldTests` (`PromptAcceptedFoldTests.cs:14`); unknown-wire-kind degradation is structural (`UserMessageBlock.cs:8-9` — folded trees only, deserialization degrades to `UnknownBlock`). |
| WEB-01 | VERIFIED | Runbook section `docs/runbooks/local-dev.md:42` ("Vertical slice: web head against the API (WEB-01)"), browser smoke executed 2026-10-21 (`:68`), steps + probe prompt and observed counts (`:70-79`: `strong` 1, `img` 0, `#app script` 0, no unhandled-error bar, no console errors after load). Screenshot exists: `docs/runbooks/evidence/web-vertical-slice-smoke.webp` (confirmed by glob listing). Release web head on `127.0.0.1:5180` against API on `127.0.0.1:5187` with the InMemory adapter, per the runbook (`:53-62`). |
| WEB-02 | VERIFIED (with honest correction: deterministic checks, NOT property-based) | `vercel.json:1-15` confirmed by read: `framework: null` (`:2`), dotnet-install via `global.json` (`:3`), `dotnet publish apps/OmpOS.Web` (`:4`), `outputDirectory output/wwwroot` (`:5`), SPA rewrite (`:10-15`), security headers (`:16-20`). Tests: `VercelConfigTests` (`tests/OmpOS.Web.Tests/VercelConfigTests.cs:8`) with `VercelConfigHasRewritesAndHeadersWithoutRoutes` (`:15`, CSP parse + `img-src` same-origin/data-only loop + wildcard/`http:`/`https:` rejection via `BroadSourceDirectives` `:11`) and `VercelConfigBuildUsesPinnedSdkAndWebProject` (`:63-72`). CORRECTION: the suite contains exactly two `[Fact]`s and **no `[Theory]`, no FsCheck/Hedgehog/property test** (grep for `Theory\|Property\|FsCheck\|Hedgehog` in the file: no matches). CSP coverage is deterministic assertion loops, not property-based testing. The requirement itself does not demand property-based tests, so WEB-02 stands VERIFIED; the "property-based" label must not be repeated. |
| DESK-01 | PARTIAL (inspection-only; binaries human-deferred, H-1) | Host logic present and tested: `IEngineHost` seam (`src/OmpOS.EngineHost/IEngineHost.cs:10`); `InProcessEngineHost.ForAcp` stdio path (`InProcessEngineHost.cs:51`); desktop registration `MauiProgram.cs:42` (`ForAcp(new OmpAcpClient(), ...)`), non-desktop branch excluded by platform check (`:37`); `EngineHostTests` 10 `[Fact]`s (`tests/OmpOS.EngineHost.Tests/EngineHostTests.cs:19,27,38,63,91,102,116,145,156,167` — incl. `NoOpWindowStateStoreLoadsNullAndAcceptsSaves` `:145`, `NoOpSingleInstanceGuardAlwaysAcquires` `:156`, `NoOpSessionRecoveryListsNothing` `:167`). App-side evidence is **inspection-only**: `apps/OmpOS.App` targets `net10.0-android;net10.0-ios;net10.0-maccatalyst` (`OmpOS.App.csproj:8`) and its own comment states it never restores/builds on Linux (`:3-6`); the reflection override is inferred, not built (`:21-22`, tracked as Q-WEB-REFLECTION). Binaries were never produced on Linux — by design — and await the macOS CI job (H-1). |
| MOB-01 | PARTIAL (inspection-only; binaries human-deferred, H-1) | Remote-only mobile path confirmed in code: `MauiProgram.cs:37-39` (Android/iOS → `new RemoteEngineHost(...)`, never a spawn); `RemoteEngineHost` (`src/OmpOS.EngineHost/RemoteEngineHost.cs:20`) with resume header (`LastEventIdHeader` `:23`); `App.xaml.cs:26` wires `window.Resumed`, and `OnWindowResumed` resumes **only** the remote host, returning early for in-process (`:30-36`); `RemoteResumeReplaysMissedEventsExactlyOnce` (`EngineHostTests.cs:64`) covers sequence resume. Same inspection-only caveat as DESK-01: no mobile binary was or can be built on Linux; macOS CI pending (H-1). |
| SHELL-01 | VERIFIED | Exclusion confirmed: `OmpOS.Linux.slnf` includes `src/OmpOS.EngineHost` (`:6`) and `tests/OmpOS.EngineHost.Tests` (`:34`) and contains **no `App` entry** (grep `App` over the file: no match). macOS job confirmed: `ci.yml` `shells` job on `macos-15` (`:74-75`) with MAUI workload install + restore + `-warnaserror` build of `apps/OmpOS.App` (`:84-88`). README states where each head was verified (`README.md:52-59` "What is verified where": Linux CI = everything in the filter; macOS = MAUI head only, human-deferred; MAUI structure = inspection-only on Linux) and the head map (`:34-35`, `:44`, `:81-83`). |

## Success-criteria table (ROADMAP.md Phase 4, criteria 1–5)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Conversation list, session view, composer, every `UiBlock` variant, unknown-collapsed fallback; same-log-same-tree replay; unknown never blanks/crashes | VERIFIED | UI-01 + UI-04 rows above. |
| 2 | Incremental deltas, stop/cancel, partial-output + retry, scroll-pause + jump-to-latest, all UI states explicit | VERIFIED (with G-1 limitation) | UI-02 row; `SessionView.razor:47-49`, `StreamingStateTests:120,166`. |
| 3 | Keyboard operation, focus rings, labelled buttons, WCAG AA tokens, throttled `aria-live`, reduced-motion, ≥44px targets; bUnit + axe-core | VERIFIED | UI-03 row: bUnit confirmed; axe-core 4.14.0 run recorded in `docs/runbooks/local-dev.md` (see Re-verification). |
| 4 | Web head env-driven config, SSE with resume, browser vertical slice prompt→rendered blocks, first block before stream end; `vercel.json` static deploy, no server component | VERIFIED | WEB-01 + WEB-02 rows; runbook `:44-46` (no hardcoded host), `:78-79` (first-block/terminal observations). |
| 5 | MAUI head shares `OmpOS.UI`; desktop in-process `IEngineHost` stdio (or remote backend) with persistence/single-instance/recovery stubs; mobile remote-only + `Resumed` sequence resume; excluded from Linux filter; macOS CI; README states verification scope | PARTIAL (binaries deferred by design) | DESK-01 + MOB-01 + SHELL-01 rows; H-1. |

## Gaps (honest limitations; neither is missing Phase 4 code)

- **G-1 — Awaiting-approval state is rendered but not end-to-end reachable.** `MarkAwaitingApproval` (`src/OmpOS.UI/State/SessionViewModel.cs:76`) has exactly two production call-sites gated on server status (`Index.razor:129-130`); permission *events* are deliberately excluded (`SessionViewModel.cs:47-49`, `Index.razor:129-131` comment). `SessionService.RequestToolApprovalAsync` (`src/OmpOS.Sessions/SessionService.cs:286`, sets `AwaitingToolApproval` at `:305`) has **no callers outside `tests/OmpOS.Sessions.Tests/SessionServiceTests.cs`** (grep `RequestToolApprovalAsync|ResolveToolApprovalAsync` over `src;services;apps`: only `SessionService.cs` + tests). The harness maps `PermissionRequestEvent` to an informational `Unknown` payload (`services/OmpOS.Api/Harness/HarnessEventMapper.cs:30`), and the API exposes exactly four routes — create/list/get session, submit prompt (`SessionEndpoints.cs:39,45,46,48`) — **no approval/resolution network route**. This is the documented deny-by-policy posture, owned by `Q-HARN-PERMISSION` (`docs/open-questions.md:41`: "Interactive approval is impossible until the harness exposes a pending-permission resolution path"). UI-02 is VERIFIED as explicit rendering; reachability awaits the harness path, which is out of Phase 4 scope.
- **G-2 — No axe-core run evidenced (UI-03). Closed 2026-10-21 by the recorded run (see Re-verification).** Before that run, a repo-wide grep for axe identifiers over `docs` and `tests` was empty. The bUnit half of the criterion is VERIFIED; a screen-reader check remains a human step outside the requirement text.

## Human verification (required before `passed`)

### H-1. macOS MAUI build (DESK-01/MOB-01 binaries)

**Test:** On macOS with the MAUI workload, run the `ci.yml` `shells`-job steps (`dotnet workload install maui`, restore + `-warnaserror` Release build of `apps/OmpOS.App`) and confirm the desktop (MacCatalyst) and mobile (iOS/Android) heads compile; spot-check desktop launches against the remote backend and mobile registers the remote-only host.
**Expected:** Warning-free build; desktop head streams via in-process host or remote backend; mobile head never spawns a process and resumes the SSE stream on `Window.Resumed`.
**Why human:** Linux cannot build MAUI targets by design (`OmpOS.App.csproj:3-6`, README `:63-68`); only macOS with workloads installed can produce the binaries. This run was pending at verification time.

### H-2. axe-core run against the published web app (UI-03) — closed 2026-10-21 (see Re-verification)

**Test:** Serve the published WASM head as in `docs/runbooks/local-dev.md:59-62`, run axe-core against the conversation/session/composer views, and record the report.
**Expected:** Zero `critical`/`serious` violations (fix if found); `moderate` recorded as open questions.
**Why human (superseded):** the axe run was done by hand on 2026-10-21 and is recorded in `docs/runbooks/local-dev.md` (see Re-verification); the bUnit assertions remain as regression guards.

---

*Verified: 2026-10-21 (read/grep/glob-only; no builds, tests, or git writes per task hard rules)*
*Verifier: VerP4 (gsd-verifier)*

## Post-verification fixes

Fixes landed after the verification above; each cites a grep-confirmed test or record:

- Skip link + accessible name (closes the axe first-run findings): `AccessibilityTests.SkipLinkIsFirstFocusableAndTargetsConversation` pins the skip link as first-focusable targeting the conversation; `NewConversationButtonTests` guards the New-button accessible name against the label-content-name-mismatch finding.
- apiBaseUrl rejection: `ApiBaseUrlResolverTests` (`MissingBaseUrlThrowsNamingTheKey`, `GarbageOverrideThrows`, `NonLoopbackHttpThrows`, `FtpSchemeOverrideIsRejected`, `JavaScriptSchemeOverrideIsRejected`, `FileSchemeOverrideIsRejected`, `PlainHttpToNonLoopbackHostIsRejected`) pins that only loopback http(s) or non-loopback https origins are accepted.
- CSP `connect-src` property: `VercelConfigTests.VercelConfigConnectSrcAllowsOnlySelfAndLoopbackHttp` pins that `connect-src` admits only `'self'` and loopback http origins.
- Axe run recorded (closes gap G-2): `docs/runbooks/local-dev.md` (Accessibility run section) records the axe-core 4.14.0 WCAG 2.0/2.1 A+AA run — 0 violations in empty and active states, two `incomplete` items on the icon-only send control (measured 18.26:1), one unexplained screenshot on record.

## Re-verification (2026-10-21, after the accessibility run)

- **UI-03: VERIFIED.** bUnit evidence in `tests/OmpOS.UI.Tests/AccessibilityTests.cs`, plus `SkipLinkIsFirstFocusableAndTargetsConversation` and `NewConversationButtonNameContainsVisibleLabel` (`tests/OmpOS.Web.Tests/NewConversationButtonTests.cs`), and an axe-core 4.14.0 run (WCAG 2.0 and 2.1, levels A and AA) against the published Release head: zero violations in the empty and the active state. Recorded in `docs/runbooks/local-dev.md` (section Accessibility run (UI-03)). Two incomplete items on the icon-only send control remain; its measured contrast is 18.26:1.
- **G-2 and H-2: closed** by the recorded run. A screen-reader check remains a human step outside the requirement text.
- **G-1 is unchanged.** Interactive approval stays deferred by the owner's decision (Q-HARN-PERMISSION).
