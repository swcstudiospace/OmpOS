---
phase: 72-cowork-browser-use
verified: 2026-11-03
status: gaps_found
method: static only (read, grep, glob, phase summaries). No builds, tests, Chromium or Xvfb launches, live omp turns or git writes were run.
covered_files:
  - .planning/ROADMAP.md (Phase 72, lines 1478-1490)
  - .planning/phases/72-cowork-browser-use/72-01..72-08-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-01..72-08-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-PLAN-CHECK.md
---

# Phase 72 verification: Cowork Browser Use

## Verdict vocabulary

- STATIC-OK: the artifact exists and the truth holds, confirmed by reading the cited lines. Nothing was executed.
- CLAIMED: a phase summary says a run passed. Not re-run here.
- NOT RUN: the summaries show the command was never executed, or it could not build.
- HUMAN NEEDED: needs a live browser, a headful Xvfb run, or an owner-approved omp turn.
- GAP: a confirmed defect, or a deliverable missing against a criterion or truth.

## ROADMAP success criteria

| # | Criterion (ROADMAP 72) | Verdict | Evidence | Notes |
|---|---|---|---|---|
| 1 | BrowserUse offline tests pass (`--filter-not-trait Category=Live`) | NOT RUN (full set); subsets CLAIMED | 72-01-SUMMARY.md:152 (15 passed, 1 live skipped, with `BuildProjectReferences=false`); 72-02-SUMMARY.md:47-68 and 72-03-SUMMARY.md:58 (unit rows, pass, combined filters 21 passed); 72-07-SUMMARY.md:16-17 (targeted offline run "not green", no result produced) | The full offline set was never reported green. |
| 2 | Live browser tests pass with headless Chromium on this host | GAP | Four of five live classes end in an unconditional `Assert.Skip`, so they report Skipped even with every env gate set: tests/Autonogrammer.Harness.BrowserUse.Tests/Corpus/HostileCorpusLiveTests.cs:17 and :20; Site/SiteLiveTests.cs:17 and :20; Live/ScreenshotLadderLiveTests.cs:17 and :20; Live/HeadfulSmokeLiveTests.cs:18 and :21. The one gated spike with real logic is Live/PipeConnectLiveTests.cs:21-27, and it was not launched (72-01-SUMMARY.md:32, :153). | A skip is not a pass. Product code also never launches Chromium (see gap 2 below). |
| 3 | Headful smoke passes under private Xvfb | GAP | Live/HeadfulSmokeLiveTests.cs:18 and :21 (unconditional `Assert.Skip`: "this execution must not launch Chromium or Xvfb"); 72-07-SUMMARY.md:24 (headful smoke not run under `xvfb-run`) | Same defect as criterion 2. |
| 4 | Heads, architecture graph, notices and decision records pass; build warning-free | NOT RUN | 72-05-SUMMARY.md:49, :54 (Native headless tests blocked by ComputerUse.Windows and .MacOS compile failures); 72-07-SUMMARY.md:17 (sibling compile failures); 72-08-SUMMARY.md:21-27 (BrowserUseIsolationTests written, run not reported); 72-PLAN-CHECK.md (no run) | UI BrowserPaneTests (5 passed, 72-05-SUMMARY.md:49) is the only head-side run reported. |
| 5 | Live omp turn with gateway, image result and `browser.enabled: false` is recorded as owner-approved or stated as not run | MET (stated-not-run branch) | 72-08-SUMMARY.md:33 and :57 ("the live omp turn was not run; OD-07 was not approved"); docs/runbooks/browser-use.md present in the docs tree | The criterion allows "stays stated as not run"; the runbook states it. An owner-approved run is still outstanding if wanted (HUMAN NEEDED). |

## Plan truths (must_haves)

| Plan | Truth | Verdict | Evidence |
|---|---|---|---|
| 72-01 | Agent browser is a host-owned Chromium over a pipe; no debugging port is opened | STATIC-OK (flags) / GAP (not launched) | src/Autonogrammer.Harness.BrowserUse/Chromium/ChromiumLaunchPlan.cs:7 (no `--remote-debugging-port`, no `--no-sandbox`), :22-23 (`--headless=new`, `--remote-debugging-pipe`); but no product code constructs `ChromiumProcessLauncher` or `BrowserSessionHost` (gap 2) |
| 72-01 | Allow-listed environment, argument-list launch, sandbox on, fresh 0700 profile wiped at session end | STATIC-OK (flags) / CLAIMED (tests) | ChromiumLaunchPlan.cs:7 and :35-36 (`--proxy-server` to the guard port); 72-01-SUMMARY.md:38-53 (D1 and D2 unit rows: flags and environment tests pass) |
| 72-01 | Chromium located or acquired only through a user-confirmed checksum step; manifest ships no rows | CLAIMED | 72-01-SUMMARY.md:114 (embedded manifest has zero rows), :31 (acquirer host check); 72-06-SUMMARY.md:33-34 (acquire only from the confirm dialog) |
| 72-01 | Every limit (tabs, memory, idle, wall time) is a validated option naming the key | CLAIMED | 72-01-SUMMARY.md:55-61 (D3 validator), :78-85 (D6 session bounds) |
| 72-02 | Every browser request passes a per-session loopback egress guard that pins the validated address | GAP (not in product path) | Classifier and pinned dial exist (72-02-SUMMARY.md:87-90, RebindingTests in the summary's coverage). Production code never constructs `EgressGuardProxy` (grep of apps/ and src/ finds definitions only). 72-02-SUMMARY.md:137-138 admits Chromium wiring is [INFERENCE]. |
| 72-02 | Top-level navigations and redirects pass the navigation gate; stage 1 host gate; stage 2 provider card | GAP (not in product path) | Gate and tests exist (72-02-SUMMARY.md:90, NavigationGateTests). `new NavigationGate` appears only in tests; `ApplyGuardsAsync` at src/Autonogrammer.Harness.BrowserUse/Tools/BrowserToolProvider.cs:214 is never invoked from the desktop head. |
| 72-02 | Local dev servers reachable only through an explicit LocalOriginGrant; zero hits on ungranted redirect targets | CLAIMED (offline only) | 72-02-SUMMARY.md:30, :50-51, :66-67 (offline proofs); live zero-hit proof is not run (72-02-SUMMARY.md:128, 72-07-SUMMARY.md:22-25) |
| 72-03 | Nine browser_* tools through BrowserToolProvider on the real CoworkToolHost | STATIC-OK (names) / GAP (not registered in a running head) | Nine descriptors: browser_navigate, browser_click, browser_type, browser_scroll, browser_read, browser_screenshot, browser_wait, browser_back, browser_close (BrowserToolDescriptors, :38-42). tests/.../Tools/BrowserToolProviderTests.cs:23-25 (actuating set is browser_click and browser_type). Registration depends on `options.Enabled`, which no production path sets (gap 2). |
| 72-03 | Stage-2 broker asks that Skip cannot skip | CLAIMED | 72-03-SUMMARY.md:12-13, :54 (redirect asked in Skip); tests/.../Tools/BrowserApprovalStage2Tests.cs:20-23 |
| 72-03 | Tainted page envelope, screenshot ladder under 3 MiB and 40 shots, dispatch limits | STATIC-OK (budget) / CLAIMED (ladder) | src/Autonogrammer.Harness.BrowserUse/Vision/BrowserLimits.cs:63 (warning at 400 calls), :77-79 (`ImageBudgetExhausted`), :90-93 (cap). The ladder runs only in ScreenshotLadderLiveTests, which skips (criterion 2). |
| 72-04 | Frames reach a pane through IBrowserPaneFeed with bounded rate and size; Web and Android show RemoteViewUnavailable | CLAIMED | 72-04-SUMMARY.md:6-12, :31-32, :56 (ScreencastPumpTests 5 passed; ContractsUiFreeTests 6 passed) |
| 72-04 | Desktop take-over with pointer and keyboard; F6 leaves keyboard capture | CLAIMED | 72-04-SUMMARY.md:33-34; 72-05-SUMMARY.md:29-30 (F6 in the pane view) |
| 72-04 | Indicator outside the frame; Stop cancels in-flight calls and revokes control | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Harness.BrowserUse/Viewer/BrowserCallGate.cs:9 (`BrowserCallGate : IBrowserCallGate`); 72-04-SUMMARY.md:33-34 |
| 72-05 | Take-over and Stop controls in both heads; accessibility and theme contracts with scripted frames | CLAIMED (partial) | 72-05-SUMMARY.md:49-54 (UI BrowserPaneTests 5 passed, CssContractTests 32, StyleAccessibilityTests 17; Native BrowserPaneViewTests blocked) |
| 72-06 | Settings shows Chromium state, policy lists, PolicyMode and acquisition only through a user-confirmed checksum step | CLAIMED | 72-06-SUMMARY.md:33-35, :54-55 |
| 72-06 | Acquisition hidden unless a manifest row exists; dialog names version, host, size and licence | CLAIMED | 72-06-SUMMARY.md:34, :40 |
| 72-07 | Hostile corpus of nineteen pages; live proofs assert BlockedByPolicy, marker unforgeability, hidden-text drop, IsUntrusted, caps, taint | GAP | Corpus pages exist (72-07-SUMMARY.md:9-12, :15). The live bodies assert nothing (criterion 2 lines). 72-07-SUMMARY.md:22-23 records that they do not drive Chromium and that LocalTestSite answers "ok" to every non-redirect path. The summary says the live classes are in `Live/`; in fact `HostileCorpusLiveTests` is in `Corpus/` and `SiteLiveTests` is in `Site/`. |
| 72-08 | Desktop composes AddBrowserUse, registers provider and both dock panels, reports capability browser_use only when Enabled and AllowDispatchSessions hold, wires guidance and Stop | GAP | apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs:215 (`offerTools = options.Enabled && ...`), :304-306 (BrowserDisabled when not Enabled). `Wire` is called without `Configure` at apps/Autonogrammer.Native/Agents/AgentHead.cs:107 and :226, so Enabled is never set. Capability and guidance therefore never activate in the running head. Stop chord bound at BrowserUseComposition.cs:251 (`Ctrl+Shift+.`). |
| 72-08 | Live omp turn recorded as owner-approved or stated as not run; docs, changelog and notices regenerated | CLAIMED | 72-08-SUMMARY.md:33, :57; docs/runbooks/browser-use.md present; notices regeneration not reported |

## Gaps

1. **Live browser tests cannot pass (GAP, criteria 2 and 3).** Four of five live classes end in an unconditional `Assert.Skip` after their env-gate check (Corpus/HostileCorpusLiveTests.cs:20, Site/SiteLiveTests.cs:20, Live/ScreenshotLadderLiveTests.cs:20, Live/HeadfulSmokeLiveTests.cs:21). With the gates set they still report Skipped, which means the roadmap's "live tests pass" cannot be met by these classes. Plan 72-07 required them to assert the behaviour listed in its truths. Fix: write the live bodies to drive Chromium and assert, and gate only the launch.
2. **The browser runtime is not in the product path (GAP, plan truths 72-01, 72-02, 72-03, 72-08 and phase goal).** `BrowserSessionHost`, `ChromiumProcessLauncher`, `EgressGuardProxy`, `NavigationGate` and `BrowserToolProvider` are constructed only in tests (grep of apps/ and src/). `BrowserUseComposition.Wire` (apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs:175-252) substitutes the following:
   - `ClosedPages` for pages (:223), which throws "The browser is not available." (:363-364)
   - `DenyingBroker`, which denies every approval (:376-377)
   - `IdleCdpSession` as the CDP session, which never sends CDP (:204, :421-431)
   - `SwallowingAudit`, which discards every audit entry (:240-245, :389-391)
   - `MemoryGrantStore` and `ClosedPorts` (:225, :244)
   `AgentHead` passes neither `Configure` nor `Pages` (AgentHead.cs:107, :226). `Enabled` therefore stays false (:215, :304), no provider is registered (:217-227), and no `browser_*` tool reaches the host. The egress guard and navigation gate are never installed. This contradicts 72-08 truth 1 and the phase goal ("The Cowork agent can browse"). The composition is a set of stand-ins, not a working integration.
3. **Egress-guard wiring is an inference (GAP, plan 72-02 truth 1).** 72-02-SUMMARY.md:137-138 says the proxy credentials and Chromium wiring are [INFERENCE]. ChromiumLaunchPlan.cs:35 does pass `--proxy-server` to a loopback port, but nothing starts the proxy (gap 2).
4. **Audit is discarded in the desktop browser path (GAP).** BrowserUseComposition.cs:240-245 and :389-391. Phase 70 requires audit for tool-host calls (70-01 truth 2). Fix: wire the audit sink the head provides.
5. **Native and UI head tests blocked (criterion 4).** 72-05-SUMMARY.md:49, :54; 72-07-SUMMARY.md:17.
6. **Summary inaccuracy (non-gap).** 72-07-SUMMARY.md:12 places `SiteLiveTests` and `HostileCorpusLiveTests` under `Live/`. They are under `Site/` and `Corpus/`.

## Human verification required

- Owner-approved live run, if wanted for criterion 5: `AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait "Category=Live"` (criterion 2, after gap 1 is fixed).
- Headful: `AUTONOGRAMMER_LIVE_BROWSER=1 AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 xvfb-run -a dotnet test ... --filter-class "*HeadfulSmokeLiveTests*"` (criterion 3, after gap 1 is fixed).
- Offline set: `dotnet test --project tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait "Category=Live"` (criterion 1).
- After the sibling Computer Use projects compile: Presentation, Native, UI and ArchitectureTests (`ProjectGraphTests`, `DecisionRecordTests`, `BrowserUseIsolationTests`) and the warning-free build (criterion 4).

## Non-blocking observations

- 72-01-SUMMARY.md:152 reports a test run with `BuildProjectReferences=false`, which is not the official command.
- 72-03-SUMMARY.md:62 notes that `Viewer/BrowserCallGate` was absent at the time; it now exists (src/Autonogrammer.Harness.BrowserUse/Viewer/BrowserCallGate.cs:9).
