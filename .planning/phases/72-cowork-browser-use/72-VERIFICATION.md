---
phase: 72-cowork-browser-use
verified: 2026-11-28T05:05:00Z
status: gaps_found
score: "offline 124/124; prod wiring absent; live CDP fails"
covered_files:
  - ".planning/phases/72-cowork-browser-use/72-01-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-01-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-02-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-02-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-03-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-03-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-04-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-04-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-05-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-05-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-06-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-06-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-07-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-07-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-08-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-08-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-09-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-09-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-10-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-11-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-11-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-12-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-12-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-13-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-13-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-14-PLAN.md"
  - ".planning/phases/72-cowork-browser-use/72-14-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-CONTEXT.md"
  - ".planning/phases/72-cowork-browser-use/72-PLAN-CHECK.md"
  - ".planning/phases/72-cowork-browser-use/72-RESEARCH.md"
  - ".planning/phases/72-cowork-browser-use/72-SOTA-COWORK-BROWSER-USE.md"
  - ".planning/phases/72-cowork-browser-use/72-SOTA-SUMMARY.md"
  - ".planning/phases/72-cowork-browser-use/72-VALIDATION.md"
  - ".planning/phases/72-cowork-browser-use/72-VERIFICATION.md"
covered_digest: "v1:sha256:3a741467a5fb8cbd860d5b3fec0919ba5050b61b28b71fb1732e5662971e9e03"
behavior_unverified: 3
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "3/5 runnable criteria pass as executed; live CDP fails on this host; production wiring absent"
commits: ["994f02b0"]
gaps:
  - truth: "Prod Wire omits Configure/Pages; browser_* not offered; egress proxy absent (72-12)."
    status: unverified
    closing_step: "Pass Configure+Pages/broker/audit; wire egress; re-run heads."
  - truth: "Live CDP fails host; headful zero tests without CHROME_BIN."
    status: unverified
    closing_step: "Run live/headful with CHROME_BIN on network host; record."
  - truth: "72-13/72-14 unexecuted (D-12/D-13 BlockList-vs-Strict)."
    status: unverified
    closing_step: "Resolve D-12/D-13; execute 72-13/72-14."
human_verification:
  - "Live/headful real-browser + 72-11 SOTA states."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - offline 124/124; prod wiring absent; live CDP fails*

| Check | Command | Result |
|---|---|---|
| restore | `dotnet restore Autonogrammer.Linux.slnf --locked-mode` | exit 0 |
| build | `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` | 124 projects, 0 errors, 0 warnings, exit 0 |
| format | `dotnet format Autonogrammer.Linux.slnf --verify-no-changes` | exit 0 |
| rebrand | `python3 -m unittest scripts/rebrand/test_rebrand.py; rebrand.py verify` | 61 OK; verify OK |
| checks | `check-licences/brand/changelog/openapi-breaking/sbom/skip-markers/parity` | all exit 0 |
| arch | `ArchitectureTests -c Release --no-build` | 107/107 |
| contracts | `Contracts.Tests non-Live` | 179/179 |
| accounts | `Accounts 215/215; Secrets.Os 83/83; Desktop 47/47` | all exit 0 |
| coord | `Coordination.Tests with services` | 87/87 |
| postgres | `Sessions.Postgres.Tests with services` | 148/148 |
| dataplane | `Api *DataPlane* 70/70; *Dispatch* 127/127; *Cowork* 44/44; *Guard* 12/12` | all exit 0 |
| cowork | `Cowork 123/123; McpGateway 79/79; AgentLoop 1328/1328` | all exit 0 |
| artifacts | `Artifacts 32/32; Stores 17/17; Memory 35/35; Memory.PG 1/1` | all exit 0 |
| browser | `BrowserUse.Tests 124/124` | exit 0 |
| compute | `ComputerUse 90/90; X11 9/9` | exit 0 |
| dispatch | `Client 1/1; Desktop 80/80` | exit 0 |
| resil | `Resilience 62/62; Sessions 124/124` | exit 0 |
| heads | `Presentation 2646/2646; UI 882/882; Web 444/444; Native 2353/2353` | exit 0 |
| a11y | `node --test a11y 48/48; fixture PASS; size ok; StyleA11y 17/17` | exit 0 |
| audit | `CoworkAudit 16/16` | exit 0 |

*Per-criterion: BrowserUse 124/124 Native/Presentation/UI/Arch/build pass. 72-10 superseded by 72-12..14.*

*Gaps:*
- Prod Wire omits Configure/Pages; browser_* not offered; egress proxy absent (72-12). -> Pass Configure+Pages/broker/audit; wire egress; re-run heads.
- Live CDP fails host; headful zero tests without CHROME_BIN. -> Run live/headful with CHROME_BIN on network host; record.
- 72-13/72-14 unexecuted (D-12/D-13 BlockList-vs-Strict). -> Resolve D-12/D-13; execute 72-13/72-14.

*Human (never automated):*
- Live/headful real-browser + 72-11 SOTA states.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

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

## Refresh 2026-11-28 (current HEAD 994f02b0, Wave 2)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh live-CDP probe TODAY: AUTONOGRAMMER_LIVE_BROWSER=1 CHROME_BIN=/usr/bin/google-chrome dotnet test tests/Autonogrammer.Harness.BrowserUse.Tests -c Release --filter FullyQualifiedName~Live — 1 pass, 2 fail: HeadfulSmokeLiveTests and PipeConnectLiveTests both raise PuppeteerSharp.ProcessException: Failed to create connection ... Target closed (Chrome 153 exists on this host but the browser cannot be spawned here). Prod wiring (72-12) and plans 72-13/72-14 remain code-side. Verdict remains `gaps_found`.*
