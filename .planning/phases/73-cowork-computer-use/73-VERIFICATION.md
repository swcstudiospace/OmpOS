---
phase: 73-cowork-computer-use
verified: 2026-11-22T01:53:17Z
status: gaps_found
score: "criteria 1-6 pass; prod wiring gap remains"
covered_files:
  - ".planning/phases/73-cowork-computer-use/73-01-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-01-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-02-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-02-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-03-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-03-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-04-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-04-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-05-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-05-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-06-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-06-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-07-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-07-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-08-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-08-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-09-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-09-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-10-PLAN.md"
  - ".planning/phases/73-cowork-computer-use/73-10-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-CONTEXT.md"
  - ".planning/phases/73-cowork-computer-use/73-PLAN-CHECK.md"
  - ".planning/phases/73-cowork-computer-use/73-RESEARCH.md"
  - ".planning/phases/73-cowork-computer-use/73-SOTA-A11Y.md"
  - ".planning/phases/73-cowork-computer-use/73-SOTA-COMPUTER-USE.md"
  - ".planning/phases/73-cowork-computer-use/73-SOTA-SECURITY.md"
  - ".planning/phases/73-cowork-computer-use/73-SOTA-STATES.md"
  - ".planning/phases/73-cowork-computer-use/73-SOTA-SUMMARY.md"
  - ".planning/phases/73-cowork-computer-use/73-VALIDATION.md"
  - ".planning/phases/73-cowork-computer-use/73-VERIFICATION.md"
covered_digest: "v1:sha256:d23c446fb62a9b3b9737ac2ddf80a4c4cb8b0880e30ba3dcd93491c6d7966c42"
behavior_unverified: 1
overrides_applied: 0
re_verification: true
previous_status: gaps_found
previous_score: "gaps_found"
commits: ["b46fd81f"]
gaps:
  - truth: "No provider registered from head; kill/indicator unverified; 73-07/09/10 blocked."
    status: unverified
    closing_step: "Register provider + kill wiring; record gate; desktop smoke."
human_verification:
  - "Real-display topmost/approval/Stop; Win/macOS runtime."
---

---
## Refresh 2026-11-22 (T855b-be, HEAD b46fd81f, SDK 10.0.402)

*Evidence first, then verdict (ADR-0002). No product code touched. Fresh test-services up failed twice at Dragonfly readiness (90 s, loaded host); DB suites ran vs existing healthy throwaway set (ag67-8382). No live-model, live-CDP, Win/macOS install, Railway, sign-in or screen-reader run claimed.*

*Status: gaps_found - criteria 1-6 pass; prod wiring gap remains*

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

*Per-criterion: ComputerUse 90/90 X11 9/9 Arch/build pass. Gap 1 unchanged.*

*Gaps:*
- No provider registered from head; kill/indicator unverified; 73-07/09/10 blocked. -> Register provider + kill wiring; record gate; desktop smoke.

*Human (never automated):*
- Real-display topmost/approval/Stop; Win/macOS runtime.

*Digest: v1:sha256 over concatenated bytes of covered_files in listed order excluding this VERIFICATION file (self-reference not computable). Prior content below retained as history.*

# Phase 73 verification: Cowork Computer Use

**Current-tree update (2026-11-10):** `KillSwitchCoordinatorTests` passed 3/3 and `ComputerUseHeadCompositionTests` passed 14/14. These focused tests do not prove the actual local opt-in, visible indicator, approval, audit or desktop actuation path. Plans 73-09 and 73-10 remain blocked by their phase-75 prerequisites; the production safety gap remains open.

**Re-verification (2026-11-21, executed):** ROADMAP criteria 1-6 all pass as executed (table below): offline core 90/90; X11 live 7/7 under a private Xvfb covering all seven brief cases; Windows/macOS Release builds warning-free; ArchitectureTests 107/107; licence/notices exit 0. Plans 73-07, 73-09 and 73-10 remain blocked and unexecuted (summaries written 2026-11-21): the production wiring gap (gap 1 below) is still open — no provider is registered from the running head, so the phase goal is not delivered despite the green core/back-end gates.

## Verdict vocabulary

- STATIC-OK: the artifact exists and the truth holds, confirmed by reading the cited lines. Nothing was executed.
- CLAIMED: a phase summary says a run passed. Not re-run here.
- NOT RUN: the summaries show the command was never executed.
- HUMAN NEEDED: needs a live desktop, a device, or a gate run.
- GAP: a confirmed defect, or a deliverable missing against a criterion or truth.

## ROADMAP success criteria

| # | Criterion (ROADMAP 73) | Verdict | Evidence | Notes |
|---|---|---|---|---|
| 1 | Computer Use core passes its offline tests (`Category=Live` excluded) | ✅ VERIFIED (executed 2026-11-21) | `dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release --filter-not-trait "Category=Live"` → total 90, failed 0, skipped 0. |
| 2 | X11 live tests pass under a private Xvfb, or each skips with a stated reason | ✅ VERIFIED (executed 2026-11-21) | `AUTONOGRAMMER_LIVE_X11=1 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release --filter-trait "Category=Live"` → total 7, failed 0, skipped 0. Fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24` and never reads `DISPLAY` (73-04). |
| 3 | Live X11 cases behave as the brief states (pointer, click with xev, typing, capture vs scrot, hotkey, second grab, window listing) | ✅ VERIFIED (executed 2026-11-21) | The 7 live cases in `X11BackendLiveTests.cs` (PointerMoveIsReadBack, ClickReachesXev, TypingReachesXevAsTheSameKeysyms, CaptureAgreesWithScrot, HotkeyFiresOnTheChord, SecondGrabReportsKillSwitchUnavailable, BareXvfbListsWindowsThroughXQueryTree) all passed in the criterion-2 run. Known caveats from 73-04 (pointer warp disconnect, scratch mapping window) remain open as executor notes. |
| 4 | Windows and macOS backends compile warning-free on Linux | ✅ VERIFIED (executed 2026-11-21) | `dotnet build` Windows + MacOS csprojs `-c Release --no-restore -warnaserror -nodeReuse:false -m:1` → 2 projects each, 0 errors, 0 warnings, exit 0. Runtime behavior on Windows/macOS remains compile-only (no host available). |
| 5 | Project graph, Computer Use boundary and decision records hold; ADR-0035 indexed | ✅ VERIFIED (executed 2026-11-21) | `dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release` → total 107, failed 0, skipped 0 (includes ProjectGraphTests, ComputerUseBoundaryTests, DecisionRecordTests with ADR-0035 indexed). |
| 6 | Licence check and notices check pass unchanged (no package added) | ✅ VERIFIED (executed 2026-11-21) | `bash scripts/ci/check-licences.sh` → `licence checks passed` exit 0; `python3 scripts/legal/generate-third-party-notices.py --check` → `THIRD-PARTY-NOTICES.md is up to date` exit 0. |

## Plan truths (must_haves)

| Plan | Truth | Verdict | Evidence |
|---|---|---|---|
| 73-01 | Backend, gate, ports and availability remediation ids exist | CLAIMED | 73-01-SUMMARY.md:85-91 (accomplishments), :36-42 (D1 and D4 unit rows) |
| 73-01 | Denial codes each have one message id in the D-06 step order | CLAIMED | 73-01-SUMMARY.md:45-51 (D2) |
| 73-01 | Options default off; validator names keys; reader maps `Cowork__ComputerUse__` variables | CLAIMED | 73-01-SUMMARY.md:53-59, :89 (D3) |
| 73-02 | Gate evaluates steps 1 to 8; first failing step wins; only `InjectAsync` caller is the executor | STATIC-OK (types) / CLAIMED (behaviour) | src/Autonogrammer.Harness.ComputerUse/Policy/ComputerActionGate.cs:9 (class); Executor/ComputerActionExecutor.cs:11 (class); 73-02-SUMMARY.md:44-45; ComputerUseBoundaryTests flags a second caller (73-02-SUMMARY.md:48) |
| 73-02 | Grants and denylist; built-in rows cannot be removed; only granted apps project titles | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Harness.ComputerUse/Policy/GrantStore.cs:9 and AppCatalogue.cs:10 (classes); 73-02-SUMMARY.md:42-43 |
| 73-02 | Kill switch refuses to arm until the registrar confirms; a trip releases input, sets Revoked, cancels, audits once | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Harness.ComputerUse/Policy/KillSwitchCoordinator.cs:8 (class); 73-02-SUMMARY.md:46 |
| 73-02 | Composition root builds the core and never constructs an OS backend | STATIC-OK | src/Autonogrammer.Harness.ComputerUse/ComputerUseComposition.cs:11 (class), :13 (doc: never constructs an OS backend) |
| 73-03 | Frames show only granted applications, downscaled, PNG under the byte cap, tracked per session | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Harness.ComputerUse/Frames/VisionPipeline.cs:11 (class); 73-03-SUMMARY.md:42-44 |
| 73-03 | Audit log owner-only JSON Lines with SHA-256 prev chain; write before injection; denial on write failure; no typed text or clipboard bytes | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Harness.ComputerUse/Audit/ComputerAuditLog.cs:16, :19 (genesis prev); 73-03-SUMMARY.md:45 |
| 73-03 | Exactly the 15 `computer_*` tools with descriptor mapping, host-observed previews, untrusted envelopes; overlay disables computer and browser | STATIC-OK (overlay) / CLAIMED (tools) | src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs:12 (overlay disables browser and computer); src/Autonogrammer.Harness.ComputerUse/Tools/ComputerToolProvider.cs:18 (class); tests/.../Tools/ComputerToolProviderTests.cs:12-40 (asserts 15 descriptors); 73-03-SUMMARY.md:47 |
| 73-03 | `computer_*` registers only for a cowork task naming computer_use on a desktop with both flags on; no relay operation reaches a backend | STATIC-OK (gate) / GAP (never registered) | src/Autonogrammer.Harness.ComputerUse/Tools/ComputerToolProvider.cs:80-83 (`ShouldRegisterFor` refuses Code, Api, Web, cloud, code prompt, non-Cowork); the method is never called from a running head (gap 1) |
| 73-04 | X11 captures with XGetImage, injects with XTest, lists displays via XRandR and windows via EWMH or XQueryTree, types unmapped characters | CLAIMED | 73-04-SUMMARY.md:41-43, :51 (live cases passed) |
| 73-04 | X11 hotkey on a second connection; BadAccess reports KillSwitchUnavailable | CLAIMED | 73-04-SUMMARY.md:44, :51 (second grab case) |
| 73-04 | Wayland and NoDisplay report typed unavailable states with remediation ids and register no tools | CLAIMED | 73-04-SUMMARY.md:43, :45 |
| 73-05 | Windows backend uses SendInput, GDI capture, UIPI, secure-desktop detection and RegisterHotKey; builds warning-free on Linux | CLAIMED | 73-05-SUMMARY.md:50-53, :58-60 |
| 73-05 | macOS backend uses CGEvent, screencapture, CoreFoundation window listing and Carbon hotkey; builds warning-free on Linux | CLAIMED | 73-05-SUMMARY.md:53, :59-60 |
| 73-06 | Approval dialog shows host facts only; 120 s timer denies; remote decision never applies | CLAIMED | 73-06-SUMMARY.md:7 (`TryApplyRemote` always returns false); src/Autonogrammer.Presentation/Computer/ComputerApprovalViewModel.cs:12 (class) |
| 73-06 | Indicator reads the kill switch; Stop trips it; dispatch shows the device | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Presentation/Computer/ComputerIndicatorViewModel.cs:11 (class, takes `IKillSwitchControl`); 73-06-SUMMARY.md:8 |
| 73-06 | Permission advisor maps every availability to a deep link or why-blocked text | STATIC-OK (type) / CLAIMED | src/Autonogrammer.Presentation/Computer/ComputerPermissionAdvisor.cs:11 (class), :14 (1 s poll); 73-06-SUMMARY.md:10 |
| 73-06 | Head composition registers the backend, settings page and functional kill indicator in the running head | GAP | The 73-08 tracer discarded composition results and used an unreachable indicator action. Those insertions were removed; actual provider registration and kill wiring remain absent. |

## Gaps

1. **Computer Use is not wired into the running head (GAP).** The 73-08 tracer did not register a provider or construct a functional indicator. Its dead-code additions were removed, restoring the five Native files to the `5ce514eb` baseline; the baseline indicator callback is also not a verified kill action. Real registration, kill-switch wiring and passing gate evidence remain required.

## Human verification required

- ~~Offline core~~ ✅ executed 2026-11-21 (90/90).
- ~~X11 under a private Xvfb~~ ✅ executed 2026-11-21 (7/7, all seven brief cases).
- ~~Windows and macOS warning-free builds on Linux~~ ✅ executed 2026-11-21 (0 warnings/errors).
- ~~`ProjectGraphTests`, `ComputerUseBoundaryTests` and `DecisionRecordTests`~~ ✅ executed 2026-11-21 (ArchitectureTests 107/107).
- ~~Licence + notices checks~~ ✅ executed 2026-11-21 (both exit 0).
- After gap 1 closes (73-07/73-09): desktop smoke (tools registered, indicator visibility, local approval, emergency Stop) on a real Cowork session — still HUMAN; a headless pass is not proof of topmost behavior across real displays. Windows/macOS runtime behavior remains compile-only until a real host is available.

## Non-blocking observations

- 73-02 and 73-03 extra type files exist only for MA0048 (73-02-SUMMARY.md:53, 73-03-SUMMARY.md:54). Accepted.
- 73-06 frontmatter lists `CoworkHead.cs` as absent; the summary's "not on disk" note is stale (see gap 1).
