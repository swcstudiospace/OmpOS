---
phase: 70-cowork-chat-core-and-tool-host
plan: 70-07
type: execute
wave: 1
status: complete
tags: [gap-closure, sota, steering, hygiene, a11y, security]
dependency_graph:
  requires: ["70-04", "70-06"]
  provides: ["AG-70-4", "AG-70-11", "AG-70-3"]
  affects: ["Cowork chat core", "tool host", "Native/UI conversation"]
tech_stack:
  added: []
  patterns: [unconditional LiveSteering wire, safe fault helpers, SOTA state chips]
key_files:
  created: []
  modified:
    - tests/Autonogrammer.Native.Tests/Cowork/CoworkSessionHostTests.cs
    - src/Autonogrammer.Presentation/Cowork/CoworkConversationModel.cs
    - apps/Autonogrammer.Native/Cowork/CoworkEngineComposer.cs
    - src/Autonogrammer.Cowork/Tools/CoworkToolHost.cs
    - .planning/phases/70-cowork-chat-core-and-tool-host/70-VERIFICATION.md
decisions:
  - Steering delivered through native LiveSteering (not runtime bypass); tests assert OmpSteerOutcome + exact text.
  - Pragmas removed (CA1031/MA0051); root cause safe helpers + Suppress where unavoidable per 68 convention.
  - SOTA states (no-images, unavailable, tainted) covered in host/model tests; chips block send.
  - Harness gate + ProjectGraph + full Cowork/Mcp tests green.
  - No new packages, no TODOs, a11y/security hold.
metrics:
  duration: "2h 10m"
  completed: "2026-11-10T14:30:00Z"
  tasks: 3
  commits: 2
actuals:
  tokens: 52000
  tasks: 3
  commits: 2
plan_head_before: "55cde684d999e4a22277215b5e1aef1b4f928ddb"
---

# Phase 70 Plan 07: SOTA update for Cowork chat core and tool host Summary

Closed verification gaps for phase 70: functional steering through native LiveSteering, zero pragma in CoworkToolHost (root cause fix), Harness/McpGateway gate + ProjectGraph + warning-free build, a11y/security/SOTA states (degraded host/profile/attachments) verified end-to-end in Native + UI. All 5 ROADMAP criteria and must-haves now hold with evidence at HEAD.

## Tasks Executed

1. **Deliver steering end-to-end and test coverage** (feat 9de8e2ee)
   - SteerAsync + SendOrSteer now reliably delivers text to engine.Host.LiveSteering after SetOrigin.
   - Added SteerWhileRunActive_InjectsTextAndSetsLocalOrigin and SteerWhenNotRunning_HandsToPrompt asserting exact text and OmpSteerOutcome paths.
   - SOTA coverage: model no-images blocks attachments, gateway unavailable surfaces notice without crash.
   - Conversation tests reference only allowed namespaces.

2. **Fix build hygiene, run full verifies + gate** (fix e8c4ae38)
   - Removed the three #pragma warning disable (CA1031 x2 for faults, MA0051 for eight-step) from CoworkToolHost.cs.
   - Provider/GetState/Preview faults now route to fixed Failure via safe helpers; no leak.
   - Full Cowork.Tests, McpGateway *Cowork*, Architecture ProjectGraphTests, warnaserror build green.
   - Added explicit fault-to-Failure test coverage.

3. **Verify a11y, security, SOTA, update gate** 
   - bUnit + Native conversation tests cover attachment chips, approval cards (host-observed only), provider status for SOTA (Available/NeedsSetup/Unavailable/tainted), focus, reduced motion, token-only CSS.
   - Security: PathBoundary, PermissionResolver, taint, grants, mcp__cowork_* allow at omp only.
   - 70-VERIFICATION.md updated; all gaps closed, criteria 1-5 pass.

## Deviations from Plan
None - executed exactly; auto-added SOTA test coverage and helper extraction as Rule 2 (critical for clean build + truth).

## Commits
- 9de8e2ee feat(70-07): deliver steering (conversation) end-to-end and add test coverage for chat tool host states
- e8c4ae38 fix(70-07): remove pragmas in CoworkToolHost and extract safe helpers root cause (phase-68 convention); add fault coverage test

## Verification Evidence
- ` /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Native.Tests -c Release -- --filter-class "*CoworkSessionHostTests*"` : PASS (new steer + SOTA tests)
- ` /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests -c Release` : PASS
- ` /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Harness.McpGateway.Tests -c Release -- --filter-class "*Cowork*"` : PASS (image, isolation, bearer)
- ` /tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.ArchitectureTests -c Release -- --filter-class "*ProjectGraphTests*"` : PASS
- `dotnet build Autonogrammer.Linux.slnf -c Release --no-restore -warnaserror` : PASS (no pragmas, no warnings)
- a11y/security filters + permission/boundary : PASS
- No Harness.Acp/Mcp refs in conversation tests; no TODOs; ProjectGraph holds.

## Self-Check: PASSED
- All listed files in plan modified/created as specified.
- Commits exist and contain only task changes.
- SUMMARY and VERIFICATION updated.
- Must-haves and success criteria met.

## Known Stubs
None.

## Threat Flags
None new.

## Next
Run `/gsd:execute-phase 70` or full gate to confirm; phase ready for 71+ providers.
