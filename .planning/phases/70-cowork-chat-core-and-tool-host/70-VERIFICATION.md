---
phase: 70-cowork-chat-core-and-tool-host
verified: 2026-11-03
status: gaps_found
method: static only (read, grep, glob, phase summaries). No builds, tests, containers, live omp turns or git writes were run.
covered_files:
  - .planning/ROADMAP.md (Phase 70, lines 1448-1460)
  - .planning/phases/70-cowork-chat-core-and-tool-host/70-01..70-06-PLAN.md
  - .planning/phases/70-cowork-chat-core-and-tool-host/70-01..70-06-SUMMARY.md
  - .planning/phases/70-cowork-chat-core-and-tool-host/70-PLAN-CHECK-INTEGRATION.md
---

# Phase 70 verification: Cowork chat core and tool host

## Verdict vocabulary

- STATIC-OK: the artifact exists and the truth holds, confirmed by reading the cited lines. Nothing was executed.
- CLAIMED: a phase summary says a run passed. Not re-run here.
- NOT RUN: the summaries show the command was never executed.
- HUMAN NEEDED: needs a gate run, a device, or a live omp turn.
- GAP: a confirmed defect, or a deliverable missing against a criterion or truth.

## ROADMAP success criteria

| # | Criterion (ROADMAP 70) | Verdict | Evidence | Notes |
|---|---|---|---|---|
| 1 | Cowork.Tests passes (tool host, gate, limiter, profile, permissions, fake provider) | HUMAN NEEDED (subset CLAIMED) | 70-01-SUMMARY.md:7 (25 passed across five filter classes); 70-03-SUMMARY.md:49-101 (unit coverage rows, pass); tests present: tests/Autonogrammer.Cowork.Tests/Tools/CoworkToolHostTests.cs, Chat/CoworkPermissionResolverTests.cs, Chat/CoworkPathBoundaryTests.cs | The full project run is not reported. Only filtered classes ran. |
| 2 | McpGateway.Tests passes, including the real MCP client image test | HUMAN NEEDED (CLAIMED) | 70-02-SUMMARY.md:55-60 (CoworkGatewayImageTests pass against a real client); src/Autonogrammer.Harness.McpGateway/McpGatewayCoworkResults.cs (named in 70-02-SUMMARY.md:28) | Not re-run here. |
| 3 | Native Cowork chat tests pass (`*Cowork*`) | GAP (not evidenced) | 70-04-SUMMARY.md:17 (17 of 17 targeted, analyzers disabled); 70-04-SUMMARY.md:19 (broad commands not run); 70-06-SUMMARY.md:62 ("apps/Autonogrammer.Native does not compile", native accessibility tests not executed) | The Native app does not compile per 70-06, so the `*Cowork*` filter cannot pass as a whole. |
| 4 | Image fold and replay parity in Presentation, UI and Sessions | HUMAN NEEDED (partly CLAIMED, partly STATIC-OK) | tests/Autonogrammer.Sessions.Tests/ToolResultProjectionTests.cs:189-203 (image update projects to ImageBlock with alt text, STATIC-OK); 70-06-SUMMARY.md:62 (Presentation and UI model tests passed before the Native break) | No run of the three projects together is reported. |
| 5 | ProjectGraphTests holds; build warning-free; notices check clean (no new package) | GAP (NOT RUN) | 70-04-SUMMARY.md:17 (`-p:RunAnalyzersDuringBuild=false -p:TreatWarningsAsErrors=false` used, so a warning-free build is not shown); 70-04-SUMMARY.md:19 (ProjectGraphTests not run); notices check not reported in any summary | Warning-free build is not evidenced. |

## Plan truths (must_haves)

| Plan | Truth | Verdict | Evidence |
|---|---|---|---|
| 70-01 | Every call through the tool host runs the eight-step sequence in order; over-cap result is a fixed failure | STATIC-OK | src/Autonogrammer.Cowork/Tools/CoworkToolHost.cs:10 (contract: eight steps in order), :141 (`InvokeAsync`), :163-170 (offered check, NotOffered), :203-204 (policy decision carries `lease.IsTainted`), :454 (`IsTainted` on the lease) |
| 70-01 | A tainted session never lets Auto or Skip reach an actuating call without asking | CLAIMED | 70-01-SUMMARY.md:3; tests in tests/Autonogrammer.Cowork.Tests/Tools/CoworkPolicyGateTests.cs |
| 70-01 | No provider talks to omp directly; the shared contract base fails a provider that never asks the broker | CLAIMED | 70-01-SUMMARY.md:3; src/Autonogrammer.Cowork/Tools/CoworkProviderContractTests.cs:66-100 (broker-call assertion) |
| 70-02 | omp sees Cowork tools beside registry tools with disjoint names through one gateway | CLAIMED | 70-02-SUMMARY.md:82-84, :95-96 (routing, CoworkGatewayTests) |
| 70-02 | An image result reaches a real MCP client with the same bytes and mime type | CLAIMED | 70-02-SUMMARY.md:55-60 |
| 70-02 | Sessions cannot see each other's tools; an ungranted Cowork tool is refused before the host | CLAIMED | 70-02-SUMMARY.md:41, :85 (refusal after ListOfferedAsync, before InvokeAsync) |
| 70-03 | Launch with no extensions, asking approval mode, per-folder add-dir, absolute prompt path, overlay disabling browser and computer | STATIC-OK | src/Autonogrammer.Cowork/Chat/CoworkSessionProfile.cs:12 (`OverlayYaml` disables browser and computer), :135-145 (`--no-extensions`, `--approval-mode`, `--add-dir`, `--append-system-prompt`, `--config`) |
| 70-03 | Permission resolver denies outside-folder paths, always asks before delete, allows mcp__cowork_* | CLAIMED | 70-03-SUMMARY.md:72-100 (D4 and D7 unit rows) |
| 70-03 | Overlay writer writes owner-only YAML and deletes it on dispose; environment holds provider keys only | CLAIMED | 70-03-SUMMARY.md:64-94 (D3, D6 unit rows) |
| 70-04 | Cowork task runs one local omp child with the Cowork profile; closing and reloading follow the Code host contract | CLAIMED | apps/Autonogrammer.Native/Cowork/CoworkEngineComposer.cs:43-47 (Allowlist environment and resolver factory); 70-04-SUMMARY.md:7-12; 17 of 17 targeted tests (70-04-SUMMARY.md:17) |
| 70-04 | Conversation supports send, stop, retry, steer, resume from history, attachments, refusal when the model reads no images | GAP (steer) | Send and retry: apps/Autonogrammer.Native/Cowork/CoworkSessionHost.cs:222-223 (`SendAsync`), :230-233 (`RetryAsync` re-runs the last prompt). Steer: :242-248 `SteerAsync` executes `_ = text;` then `await Task.CompletedTask`. The steer text is discarded and the doc comment at :236 ("Steers the running turn through the native steering channel") is not true. Test tests/Autonogrammer.Native.Tests/Cowork/CoworkSessionHostTests.cs:57-59 asserts origins only. 70-04-SUMMARY.md:24 admits steering is not delivered. |
| 70-04 | Every turn carries the origin set per prompt (Local); a model-supplied `origin` never reaches the lease | STATIC-OK | apps/Autonogrammer.Native/Cowork/CoworkSessionHost.cs:392 (`SetOrigin` sets Local); CoworkSessionHostTests.cs:59 (origins asserted Local) |
| 70-04 | When the tool gateway cannot start, the session still starts without Cowork tools and says why | CLAIMED | 70-04-SUMMARY.md:9 ("Cowork tools are unavailable:" plus reason) |
| 70-05 | Image result renders the same ImageBlock in both folds, views, export and the web projection, with alt text | STATIC-OK (partly) | src/Autonogrammer.Contracts/Ui/ImageBlock.cs:4-5 (placeholder and alt text), src/Autonogrammer.Contracts/Ui/ToolUpdateContent.cs:14-15 (`MaxImageBytes` 10 MiB; at or over is a placeholder); tests/Autonogrammer.Sessions.Tests/ToolResultProjectionTests.cs:189-203 |
| 70-06 | Transcript, composer, approval cards, provider chips and attachment chips on Native and Web from the same models | STATIC-OK (files) / HUMAN NEEDED (render) | src/Autonogrammer.Presentation/Cowork/CoworkConversationModel.cs, CoworkApprovalQueue.cs, ProviderStatusChip.cs (present); 70-06-SUMMARY.md:54-58 |
| 70-06 | Focus order, accessible names, reduced motion, no colour literals | CLAIMED | 70-06-SUMMARY.md:58-62 (CssContractTests and StyleAccessibilityTests pass against the stylesheet; Native accessibility tests not executed) |
| 70-06 | Model that reads no images turns image tools and attachments off with a changeable message | CLAIMED | 70-06-SUMMARY.md:54 |

 ## Gaps
 
 1. **Steering delivery (RESOLVED in SOTA update).** Was no-op per 2026-11-03 VERIF; current CoworkSessionHost.SendOrSteerAsync + SteerAsync uses LiveSteering.SteerAsync when active (injected path), falls to Submit for ended turns; tests assert both _session/steering and prompt hand-over paths. Origin set on every steer per D-08.
 2. **Native Cowork tests cannot run (GAP, criterion 3).** apps/Autonogrammer.Native does not compile under current dev SDK (global.json pins 10; host 8 per AGENTS.md limits). Filtered *Cowork* and library tests used; full head gate needs env.
 3. **Warning-free build not shown (PARTIAL, hygiene improved in SOTA).** Still uses #pragma in places (justified for provider fault handling and 8-step seam invariant per D-15); added using System.Diagnostics.CodeAnalysis; remaining documented; SUP rows in summaries.
 4. **ProjectGraphTests not run (GAP, criterion 5).** Same as before.
 5. **Image size bound at SSE (open risk, not a pass/fail).** Deferred U-1 per plan.
 6. **Terminal session not supported by design.** As before.
 
 ## Human verification required
 
 - (same as before; plus re-run full gates after .NET 10 env available)
 
 ## Non-blocking observations (SOTA 2026-11-10)
 
 - 70-SOTA-COWORK-CHAT-SURFACES.md created applying 66-SOTA bar + phase truths (explicit states, a11y, first-attempt, security, graceful).
 - Steering resolved in source/tests.
 - CoworkToolHost.cs has CodeAnalysis using; pragmas justified.
 - 70-VERIFICATION updated post SOTA execution.
