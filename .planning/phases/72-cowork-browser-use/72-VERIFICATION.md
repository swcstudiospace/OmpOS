---
phase: 72-cowork-browser-use
verified: 2026-11-21
status: gaps_found
score: "3/5 runnable criteria pass as executed; live CDP fails on this host; production wiring absent"
covered_files:
  - .planning/ROADMAP.md (Phase 72, lines 1681-1720)
  - .planning/phases/72-cowork-browser-use/72-01..72-11-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-01..72-08-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-09-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-09-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-11-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-11-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-12-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-12-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-13-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-13-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-14-PLAN.md
  - .planning/phases/72-cowork-browser-use/72-14-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-10-PLAN.md (superseded, not executed)
  - .planning/phases/72-cowork-browser-use/72-SOTA-SUMMARY.md
  - .planning/phases/72-cowork-browser-use/72-VERIFICATION.md
  - apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs
  - apps/Autonogrammer.Native/Agents/AgentHead.cs
  - tests/Autonogrammer.Harness.BrowserUse.Tests/Live/PipeConnectLiveTests.cs
  - tests/Autonogrammer.Harness.BrowserUse.Tests/Live/HeadfulSmokeLiveTests.cs
  - tests/Autonogrammer.Harness.BrowserUse.Tests/Site/SiteTests.cs
  - tests/Autonogrammer.Harness.BrowserUse.Tests/Corpus/CorpusPagesInventoryTests.cs
  - tests/Autonogrammer.Harness.BrowserUse.Tests/Vision/ScreenshotLadderTests.cs
  - tests/Autonogrammer.Native.Tests/Browser/BrowserUseCompositionTests.cs
covered_digest: "v1:sha256:manual-verif-72-20261110"
behavior_unverified: 3
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: 1/5
gaps_closed: []
gaps_remaining:
  - "Production AgentHead call to Wire (AgentHead.cs:110,230) still omits Configure (Enabled stays false) and Pages (ClosedPages); no browser_* tools offered in desktop Cowork head"
  - "Live browser tests fail on this host: PuppeteerSharp 'Failed to create connection' (2 failed, 0 passed) with the ROADMAP env gates; headful smoke under the ROADMAP command (no CHROME_BIN) runs zero tests, 1 skipped"
  - "Production egress proxy and browser session are not wired (72-12 unexecuted; requires 75-11 chain)"
  - "72-13 required browser audit and 72-14 Native composition unexecuted (72-13 blocked on 75 chain; 72-14 blocked on D-12/D-13 BlockList-vs-Strict incompatibility)"
  - "Native/UI full suites now executed clean (Native 2353 passed + 5 skipped; UI 882/882); full-disk corruption of the first Native run recorded here and superseded by the clean rerun"
gaps:
  - truth: "Live browser tests drive real Chromium and assert without unconditional skip"
    status: failed
    reason: "Re-verified 2026-11-21: with the ROADMAP env gates the only two live classes (PipeConnectLiveTests, HeadfulSmokeLiveTests) fail — PuppeteerSharp 'Failed to create connection' (total 2, failed 2, skipped 0). The Site/Corpus/Screenshot live classes named by 72-09 do not exist; 72-09 is unexecuted. No live behavior is proven."
    artifacts:
      - path: "tests/Autonogrammer.Harness.BrowserUse.Tests/Live/HeadfulSmokeLiveTests.cs"
        issue: "real host launch and frame assertion added; not verified"
      - path: "tests/Autonogrammer.Harness.BrowserUse.Tests/Site/SiteTests.cs"
        issue: "offline test only; no SiteLiveTests exists (72-09 unexecuted)"
      - path: "tests/Autonogrammer.Harness.BrowserUse.Tests/Corpus/CorpusPagesInventoryTests.cs"
        issue: "offline inventory only; no HostileCorpusLiveTests exists (72-09 unexecuted)"
      - path: "tests/Autonogrammer.Harness.BrowserUse.Tests/Vision/ScreenshotLadderTests.cs"
        issue: "offline ladder only; no ScreenshotLadderLiveTests exists (72-09 unexecuted)"
    missing:
      - "Implement drive bodies using BrowserSessionHost + provider + asserts for invariants and SOTA states per 72-09 plan"
  - truth: "In production desktop head, real BrowserSessionHost / ChromiumProcessLauncher / EgressGuardProxy / NavigationGate / BrowserToolProvider are constructed and registered when Enabled; real broker/audit/grant used; Enabled set true"
    status: failed
    reason: "Unchanged 2026-11-21: AgentHead wires only StateDirectory + Artifacts (no Configure, no Pages, no Shell), so the provider receives ClosedPages/ClosedPorts with Enabled default false and offerTools stays false. Composition does build a real CoworkApprovalBroker/FileCoworkGrantStore/FileCoworkToolAudit (72-08), but they are unreachable for browser tools. 72-12/13/14 unexecuted."
    artifacts:
      - path: "apps/Autonogrammer.Native/Agents/AgentHead.cs:110,230"
        issue: "Wire call lacks Configure: o => o.Enabled = true, Pages"
      - path: "apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs:237,244-247"
        issue: "offerTools = options.Enabled && ...; provider gets request.Pages ?? new ClosedPages() and new ClosedPorts() — no guarded session."
    missing:
      - "Pass Configure and real Pages/broker/audit from AgentHead/Cowork per 72-10 plan"
      - "Rewrite composition Build to accept/forward real ICoworkApprovalBroker, ICoworkToolAudit, ICoworkGrantStore from request for prod path (keep fakes only under Force/Closed for tests)"
  - truth: "EgressGuardProxy started per-session before Chromium, loopback port passed to launch plan; requests pinned/classified in prod"
    status: failed
    reason: "No session-owned egress proxy is started or passed to Chromium by the production composition."
    artifacts:
      - path: "apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs:232"
        issue: "The invented proxy construction was removed; actual session-owned egress wiring remains absent."
    missing:
      - "Wire egress port/credentials into launch and CDP in prod path; remove silent catch or log"
  - truth: "Full ROADMAP criteria 1-5 (offline green, live gated, headful gated, heads+arch green, runbook) and 72-11 SOTA a11y/security states"
    status: failed
    reason: "Re-verified 2026-11-21: criterion 1 passes (124/124); criterion 2 fails (CDP connection, 2/2 failed); criterion 3 runs zero tests under the ROADMAP command (1 skipped, CHROME_BIN unset); criterion 4 passes (Presentation 2646/2646, Native 2353+5 skipped, UI 882/882, Architecture 107/107, slnf build 124 projects 0 warnings, notices exit 0); criterion 5 passes via stated-not-run. Prod wiring absent; 72-11 SOTA/a11y matrix unexecuted."
    artifacts:
      - path: "tests/Autonogrammer.Harness.BrowserUse.Tests"
        issue: "offline run not confirmed this verif"
    missing:
      - "Confirm full commands green after wiring fixed; update runbook if live omp done; mark 72-09/10/11 [x] after SUMMARYs; fix ROADMAP checklist"
deferred: []
advisory: []
behavior_unverified_items:
  - truth: "Live tests and headful smoke actually drive Chromium and exercise state transitions (BrowserPaneState.Live, CoworkProviderState.Available) and cleanup invariants"
    test: "Set the three env gates and run the exact ROADMAP live + xvfb headful commands; trigger navigate + screenshot + stop"
    expected: "Real Chromium launches over pipe, frames captured, no planted secrets in environ, states transition, profiles cleaned on bound, no unconditional skip, assertions pass"
    why_human: "Presence of launcher code + env gate is necessary but presence checks cannot prove the CDP pipe actually connected, frames returned, process scrubbed or state machine fired; requires live Chromium (or xvfb) execution"
  - truth: "Egress guard and NavigationGate actually intercept and classify every request in a launched session"
    test: "With live gates, navigate to corpus and granted sites; inspect proxy log or classifier hits"
    expected: "All egress requests pinned to grants; classified and blocked/allowed per policy; no direct net from Chromium"
    why_human: "Static grep sees the proxy new/Start but not that the launch plan receives the port and that CDP navigation actually routes through it at runtime"
  - truth: "Real (non-denying, non-swallowing) broker, grant store and audit are used for browser tool calls from Cowork head; stage-2 approval works"
    test: "In desktop head with browser.enabled true in settings, issue a browser_navigate via omp or tool host; observe approval card and audit entry"
    expected: "Allowing path or real head broker reached; audit receives entry (not swallowed); grant recorded if stage-1"
    why_human: "Code paths exist under offerTools but the prod call sites and host ctor still hard-wire the denying/swallowing seams; only live exercise of tool dispatch from CoworkToolHost shows the seam actually used"
coincidental_reliance_items: []
human_verification:
  - test: "Run offline: dotnet test tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-not-trait \"Category=Live\""
    expected: "All non-live tests pass (green) — EXECUTED 2026-11-21: 124/124 passed"
    why_human: "Closed by execution; retained for traceability"
  - test: "Run live (headless): AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter-trait \"Category=Live\""
    expected: "Live classes pass — EXECUTED 2026-11-21: FAILED (total 2, failed 2): PuppeteerSharp 'Failed to create connection'. Criterion 2 does not pass on this host; not claimed."
    why_human: "Needs a host where the CDP pipe connects to the downloaded/launched Chromium; also needs 72-09 live bodies (Site/Corpus/Ladder do not exist)."
  - test: "Run headful smoke: AUTONOGRAMMER_LIVE_BROWSER=1 AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 xvfb-run -a dotnet test ... --filter-class \"*HeadfulSmokeLiveTests*\" (ROADMAP command, no CHROME_BIN)"
    expected: "Headful smoke passes — EXECUTED 2026-11-21: 0 ran, 1 skipped ('Set CHROME_BIN to the Chromium binary'). Criterion 3 as written does not execute; with CHROME_BIN set it would hit the same CDP connection failure as criterion 2."
    why_human: "ROADMAP command omits CHROME_BIN which the test requires; headed behavior unproven either way (test uses --headless=new)."
  - test: "After sibling fixes, run: dotnet test tests/Autonogrammer.Presentation.Tests -c Release; tests/Autonogrammer.Native.Tests; tests/Autonogrammer.UI.Tests; tests/Autonogrammer.ArchitectureTests; dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror; python3 scripts/legal/generate-third-party-notices.py --check"
    expected: "All green, warning-free — EXECUTED 2026-11-21: Presentation 2646/2646, Native 2353+5 skipped, UI 882/882, Architecture 107/107, build 124 projects 0 warnings, notices exit 0"
    why_human: "Closed by execution; retained for traceability"
  - test: "Verify runbook: cat docs/runbooks/browser-use.md | grep -E 'live omp|OD-07|browser.enabled: false'"
    expected: "Documents the live omp or states not run"
    why_human: "Owner-approved live run with gateway+image is external; static file check + stated-not-run is per criterion"
  - test: "Desktop head smoke: start head with Cowork:BrowserUse:Enabled true (via settings), issue browser_navigate via omp or UI; observe pane live, tools offered, stop chord, no swallow audit"
    expected: "Real browser pane updates, tools in host, egress active, audit written"
    why_human: "Requires full desktop run + real Chromium + user-visible pane + tool dispatch; code paths alone do not prove integration"
---

# Phase 72: Cowork Browser Use Verification Report

**Phase Goal:** The Cowork agent can browse: a host-launched, isolated Chromium driven over a CDP pipe by PuppeteerSharp, shown as a live pane on the desktop head (the Web and Android panes render the same models and wait for a relay feed, which ADR-0036 holds back), exposed to omp as `browser_*` tools with vision, under an egress guard, two-stage approvals, a visible Stop and a hostile-page test corpus.
**Verified:** 2026-11-21 (re-verification; supersedes 2026-11-10 where they conflict)
**Status:** gaps_found
**Re-verification:** Yes — 2026-11-21 executed re-verification (see table); plans 72-09/72-11/72-12/72-13/72-14 blocked and unexecuted, 72-10 superseded
**Score:** 1/5 runnable criteria pass as executed; live CDP fails; prod wiring absent

## Goal Achievement

### Observable Truths (ROADMAP criteria + gap-closure must-haves)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Browser library passes its offline tests (`dotnet test ... --filter-not-trait "Category=Live"`) | ✅ PASSED (executed 2026-11-21) | Total 124, failed 0, skipped 0. |
| 2 | Live browser tests pass with headless Chromium on this host | ❌ FAILED (executed 2026-11-21) | `AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX=1 dotnet test ... --filter-trait "Category=Live"` → total 2, failed 2, skipped 0: `PuppeteerSharp.ProcessException: Failed to create connection` (+ Shouldly assertion). Recorded as failure, not pass. |
| 3 | Headful smoke passes under private Xvfb | ⚠️ SKIPPED (executed 2026-11-21, ROADMAP command as written) | ROADMAP command (no CHROME_BIN) under `xvfb-run -a` → 0 ran, 1 skipped ('Set CHROME_BIN to the Chromium binary'). Headed behavior unproven (test uses `--headless=new` in any case). |
| 4 | Heads, architecture graph, notices and decision records pass; build warning-free | ✅ PASSED (executed 2026-11-21) | Presentation full 2646/2646; Native full 2353 passed + 5 skipped, 0 failed (clean rerun after freeing disk; first run's 244 failures were full-disk corruption — build exit -532462766); UI full 882/882; Architecture full 107/107 (incl. ProjectGraph + DecisionRecord); slnf build 124 projects 0 warnings/errors; notices `--check` exit 0. |
| 5 | Live omp turn with gateway, image result and `browser.enabled: false` recorded or stated not run | STATIC-OK (stated-not-run) | docs/runbooks/browser-use.md present; prior verif noted "the live omp turn was not run; OD-07 was not approved". Criterion allows stated branch. |
| 6 | Live tests drive real Chromium and assert host invariants | ❌ FAILED (see criterion 2) | Site/Corpus/Screenshot live classes do not exist (72-09 unexecuted); the two existing live classes fail at CDP connection. No invariants proven live. |
| 7 | In production desktop head, real ... are constructed and registered when Enabled; Egress started; real ICowork* broker/audit/grant (not Denying/Swallowing); Enabled=true via Configure in AgentHead; provider reaches CoworkToolHost; browser_* tools offered; SOTA states reported | ❌ FAILED (unchanged 2026-11-21) | `AgentHead.cs:110,230` Wire call passes only StateDirectory + Artifacts; provider gets `ClosedPages`/`ClosedPorts`; `offerTools` false. Composition builds real broker/grant/audit but unreachable for browser tools. 72-12/13/14 unexecuted (75-chain missing; D-12/D-13 incompatibility). |

**Score (re-verification):** criteria 1, 4 and 5 pass; criteria 2, 6, 7 fail; criterion 3 skipped. Phase goal not delivered (live CDP broken on this host; production wiring absent).

### Deferred Items

None for this milestone; 72-09/10/11 are the gap-closure within phase (later SOTA follow-ups noted in placeholders).

### Advisory (New Scope, Unevidenced)

None.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `tests/Autonogrammer.Harness.BrowserUse.Tests/Live/*.cs` (updated) | Real drive and assertions | ✓ PARTIAL | Pipe and headful smoke contain live drive; site, corpus and screenshot remain placeholders; new smoke unverified |
| `apps/Autonogrammer.Native/Cowork/BrowserUseComposition.cs` | Prod path with real seams (no stand-ins) | ✗ STUB | offerTools + real objects gated behind Enabled=false; host ctor stand-ins |
| `apps/Autonogrammer.Native/Agents/AgentHead.cs` | Passes Configure: o=>o.Enabled=true (or persisted) + Pages | ✗ MISSING | Still bare Wire({StateDirectory, Artifacts}) |
| `BrowserSessionHost`, `ChromiumProcessLauncher`, `EgressGuardProxy`, `BrowserToolProvider` | Instantiated in prod when enabled | ⚠️ ORPHANED | Code present, never reached in prod path |
| `docs/runbooks/browser-use.md` | States live omp or not-run | ✓ VERIFIED | Per prior + criterion allowance |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| AgentHead.Create | BrowserUseComposition.Wire | direct new BrowserUseCompositionRequest | PARTIAL | No Configure/Pages passed |
| BrowserUseComposition (offerTools) | BrowserToolProvider + Egress + SessionHost | if (offerTools) | WIRED (but dead) | Real objects created only when Enabled |
| CoworkToolHost | providers list | ctor | PARTIAL | Real provider added but host broker/audit = denying/swallowing |
| Live tests (env=1) | BrowserSessionHost.OpenAsync | real launcher | PARTIAL | Pipe and headful smoke call the host; site, corpus and screenshot do not |
| EgressGuardProxy | Chromium launch | --proxy-server arg | INFERENCE | Code in 72-02; not confirmed passed in prod |

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| BrowserPaneViewModel | State | PaneState(options, located, Force) | Static enum based on Enabled/locate | ⚠️ HOLLOW (no live feed when not offered) |
| CoworkToolHost | providers | if(offer) add provider else empty | Empty in prod | ✗ DISCONNECTED |
| BrowserToolProvider | readiness | CoworkProviderState(Available) | Hardcoded in composition | ⚠️ STATIC |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Offline tests (non-live) | AUTONOGRAMMER_LIVE_BROWSER=0 /tmp/ag-bin/ag-dotnet test ... --filter-not-trait Category=Live | Timed out after 120s (build phase) | ? SKIP |
| Targeted live pipe (real Chromium drive) | AUTONOGRAMMER_LIVE... /tmp/ag-bin/ag-dotnet test ... --filter "*PipeConnectLiveTests.Pipe_Connects*" | Timed out after 60s | ? SKIP |
| Build harness tests | /tmp/ag-bin/ag-dotnet build tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --no-restore | Timed out after 90s | ? SKIP |
| Native composition tests (static path) | (code inspection + prior test file) | Tests exercise Force+Configure paths; prod path not | PARTIAL |

### Probe Execution

No phase-specific probes declared for 72; conventional test commands used above.

### Requirements Coverage

| Requirement | Source | Description | Status | Evidence |
|-------------|--------|-------------|--------|----------|
| AG-72-1 to AG-72-16 | 72-11 + ROADMAP | All browser use reqs | PARTIAL | Core launch/egress/tools present in harness; prod integration and live drive incomplete |
| (from gap plans) | 72-09/10 | Live drive + prod wiring | PARTIAL/FAILED | See gaps |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Site, Corpus, ScreenshotLadder live tests | 20 | placeholder drive body | ⚠️ Warning | Tests pass vacuously when opted in; behavior not exercised |
| BrowserUseComposition.cs | 232 | try { _ = egress.StartAsync(); } catch { /* optional */ } | ℹ️ Info | Silent failure on egress start |
| (none new debt markers unreferenced) | - | - | - | - |

### Human Verification Required

(See frontmatter human_verification section — 6 items including the 3 behavior-unverified truths. All require live execution or full head run.)

### Gaps Summary

Gap-closure plans 72-09/10/11 written and reference prior VERIFICATION gaps 1-6. 

- Unconditional skip gap closed (static).
- Live drive and prod wiring gaps remain open (placeholders + Enabled not set in prod + stand-in broker/audit in host ctor).
- Full behavioral confirmation (offline, live, headful, arch) blocked by runner timeouts in this verif run; rely on static + prior summaries for non-live.
- Egress/audit still not active in desktop path.

Next: execute 72-09/10 (or fold into SOTA), produce SUMMARYs, re-verify with successful runs, update ROADMAP checklist, close remaining in 72-11 or follow-up.

**Status:** gaps_found (core prod integration + full live drive bodies + run confirmation pending)

---

_Verified: 2026-11-10_
_Verifier: gsd-verify-72 (re-verification after gap-closure planning; static + limited runner attempts)_

**Policy blocker (2026-11-10):** The user chose to retain both the D-12 PuppeteerSharp BlockList and D-13 Strict Allowlist requirements. The pinned 25.15.0 API documents those options as mutually exclusive. Phase 72 remains blocked until a compatible implementation and real browser evidence establish both boundaries.

**Policy research:** The pinned [PuppeteerSharp target manager](https://github.com/hardkoded/puppeteer-sharp/blob/v25.15.0/lib/PuppeteerSharp/Cdp/ChromeTargetManager.cs) rejects a nonempty blocklist and allowlist in the same connection. Separate connections are only an unverified hypothesis, not evidence that their policies compose or fail closed across targets. Any candidate must prove ordering, attachment and failure handling, workers, redirects and non-network schemes against the actual guarded Native path before 72-14 can proceed.

**72-09 implementation blockers:** The session returned by `BrowserSessionHost` exposes version and frame capture but not a page or CDP session for navigating, reading or capturing screenshots. `BrowserReadTool` expects synthetic top-level `nodes`, not the real DOMSnapshot response; `BrowserNavigateTool` attempts to read a final URL from `Page.navigate`, which does not supply it. The production launcher does not consume `AUTONOGRAMMER_BROWSER_ALLOW_NO_SANDBOX`. Site redirect zero-hit, hostile corpus extraction and taint, screenshot ladder and budget, and actual pane/provider state transitions remain unproved. These require supported integration and real private-fixture browser verification; no live model or external site is needed.

**Launch and cancellation:** `ChromiumProcessLauncherTests` passed 1/1 against a deliberately stalled local child; cancellation stopped the handshake and terminated that child. The pinned PuppeteerSharp pipe transport also requires `Start()` to begin receiving; after adding it, the isolated real Chromium smoke passed 1/1 with a test-only explicit no-sandbox wrapper. This does not verify production sandboxing, headed behavior, egress, approval or audit.
