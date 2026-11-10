---
phase: 73-cowork-computer-use
verified: 2026-11-03
status: gaps_found
method: static only (read, grep, glob, phase summaries). No builds, tests, Xvfb sessions, desktop drives or git writes were run.
covered_files:
  - .planning/ROADMAP.md (Phase 73, lines 1492-1505)
  - .planning/phases/73-cowork-computer-use/73-01..73-06-PLAN.md
  - .planning/phases/73-cowork-computer-use/73-01..73-06-SUMMARY.md
  - .planning/phases/73-cowork-computer-use/73-PLAN-CHECK.md
---

# Phase 73 verification: Cowork Computer Use

## Verdict vocabulary

- STATIC-OK: the artifact exists and the truth holds, confirmed by reading the cited lines. Nothing was executed.
- CLAIMED: a phase summary says a run passed. Not re-run here.
- NOT RUN: the summaries show the command was never executed.
- HUMAN NEEDED: needs a live desktop, a device, or a gate run.
- GAP: a confirmed defect, or a deliverable missing against a criterion or truth.

## ROADMAP success criteria

| # | Criterion (ROADMAP 73) | Verdict | Evidence | Notes |
|---|---|---|---|---|
| 1 | Computer Use core passes its offline tests (`Category=Live` excluded) | HUMAN NEEDED (partly CLAIMED) | 73-01-SUMMARY.md:36-42 (DenialCodeCatalogTests, CoordinateMap and options tests, pass); 73-02-SUMMARY.md and 73-03-SUMMARY.md report no test result; tests present: tests/Autonogrammer.Harness.ComputerUse.Tests/Tools/ComputerToolProviderTests.cs (15 descriptors), tests/Autonogrammer.Harness.ComputerUse.Tests/Policy/*.cs (per 73-02 frontmatter) | Full offline run not reported for policy, frames or audit. |
| 2 | X11 live tests pass under a private Xvfb, or each skips with a stated reason | CLAIMED (HUMAN NEEDED to confirm) | 73-04-SUMMARY.md:50-52 (unit 9 passed; live with `AUTONOGRAMMER_LIVE_X11=1`: 7 passed; without the opt-in: 7 skipped); live file tests/Autonogrammer.Harness.ComputerUse.X11.Tests/Live/X11BackendLiveTests.cs (73-04 frontmatter); 73-04-SUMMARY.md:46 (fixture starts `Xvfb -displayfd 3 -nolisten tcp -screen 0 1280x800x24`, never reads `DISPLAY`) | Positive evidence from the executor. Not re-run here. |
| 3 | Live X11 cases behave as the brief states (pointer, click with xev, typing, capture vs scrot, hotkey, second grab, window listing) | CLAIMED (HUMAN NEEDED to confirm) | 73-04-SUMMARY.md:51 (all seven listed cases passed); 73-04-SUMMARY.md:57-58 (known caveats: pointer warp disconnect, scratch mapping window) | Same evidence as criterion 2. |
| 4 | Windows and macOS backends compile warning-free on Linux | CLAIMED (HUMAN NEEDED to confirm) | 73-05-SUMMARY.md:58-60 (Windows tests 8 passed; macOS tests 6 passed; both Release builds with `-warnaserror` exit 0, 0 warnings); src/Autonogrammer.Harness.ComputerUse.Windows/WindowsBackend.cs:12 and src/Autonogrammer.Harness.ComputerUse.MacOS/MacOSBackend.cs:14 (present) | Not re-run here. |
| 5 | Project graph, Computer Use boundary and decision records hold; ADR-0035 indexed | HUMAN NEEDED (ADR present and indexed) | docs/decisions/ADR-0035-cowork-computer-use.md (present); docs/SUMMARY.md:125 (indexed); tests/Autonogrammer.ArchitectureTests/ComputerUseBoundaryTests.cs (present, 73-02 frontmatter); 73-06-SUMMARY.md:31 (registry files unchanged) | No run of ProjectGraphTests, ComputerUseBoundaryTests or DecisionRecordTests is reported. |
| 6 | Licence check and notices check pass unchanged (no package added) | HUMAN NEEDED | 73-06-SUMMARY.md:27 ("No package was added. Licences and notices were not regenerated."); 73-06-SUMMARY.md:31 (no registry edit) | The scripts were not run. |

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
| 73-06 | Head composition registers the backend, the settings page and the indicator in the running head | GAP (addressed by 73-07) | 73-07-PLAN.md + production call sites in AgentHead/Cowork; Build reachable. |

## Gaps

1. **Computer Use is not wired into any running head (GAP, phase goal and plan truths 73-03/73-06; addressed by 73-07 gap-closure plan).** 73-07-PLAN.md supplies the missing production call sites (AgentHead Create* paths and Cowork composition) that invoke ComputerUseHeadComposition.Build, ComputerUseComposition.CreateAsync, ComputerUseToolsComposition.AddTools and register the provider (plus publish settings factory and indicator chrome entry). After execution the tools reach the live CoworkToolHost when Cowork+enabled+desktop; SettingsFactory becomes live. 73-07 also adds the three SOTA-*.md overlays (states, a11y, security per query) and re-executes the full gate (criteria 1-6).
   (73-06-SUMMARY:24 and PLAN-CHECK W-B identified the carrier; 73-07 closes it. Gaps 2-5 remain as documented.)

## Human verification required

- Offline core: `nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.Tests -c Release --filter-not-trait "Category=Live"` (criterion 1).
- X11 under a private Xvfb: `AUTONOGRAMMER_LIVE_X11=1 nice -n 15 dotnet test --project tests/Autonogrammer.Harness.ComputerUse.X11.Tests -c Release --filter-trait "Category=Live"` (criteria 2 and 3).
- Windows and macOS warning-free builds on Linux (criterion 4).
- `ProjectGraphTests`, `ComputerUseBoundaryTests` and `DecisionRecordTests` (criterion 5).
- `bash scripts/ci/check-licences.sh` and `python3 scripts/legal/generate-third-party-notices.py --check` (criterion 6).
- After 73-07: desktop smoke (tools registered, indicator, approval/kill) on real Cowork session (still HUMAN for full live sign-off).

## Non-blocking observations

- 73-02 and 73-03 extra type files exist only for MA0048 (73-02-SUMMARY.md:53, 73-03-SUMMARY.md:54). Accepted.
- 73-06 frontmatter lists `CoworkHead.cs` as absent; the summary's "not on disk" note is stale (see gap 1).
