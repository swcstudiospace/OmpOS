# Phase 66 Plan Check (2026-10-28)

Verdict: ADVISORY ISSUES FOUND — 0 blockers, 3 warnings (accepted deviations, no re-plan), 2 info.
Probes (orchestrator): verify-command-path not_applicable (0/80); failing-direction ok (80/80).

## Dimension results
- Req coverage (AG-66-1..14): PASS. 01->1-5; 02->11; 03->5,6; 04->7; 05->8,10; 06->9; 07->1,2,3,4,6,12,13,14.
- Task completeness: PASS w/ INFO-1. All tasks have read_first + action + verify/automated + acceptance_criteria; every automated has a fails_when (counts 8/8, 9/9, 15/15, 9/9, 10/10, 5/5, 28/28). Plans use acceptance_criteria + plan-level files_modified per dispatch contract instead of per-task files/done.
- Dependencies: PASS. Waves 1:[01,02] 2:[03] 3:[04,06] 4:[05] 5:[07]; edges acyclic, consistent with waves.
- Undeclared coupling: PASS. Same-wave pairs: 01/02 read-only; 04/06 disjoint (verified empty intersection).
- Key links: PASS. 04 adds 2 downward edges (L2/L4->L1, no tuple needed) + relock + locked-mode verify; 05 consumes pre-existing Abstractions edges (verified: AgentLoop/Mcp/Api have them, Workspace added by 04, Plugins deliberately avoids); 06/03/07 wiring intra-plan or sequential.
- Scope: 3 WARNINGs (below). 07 has 7 tasks; 04 lists 34 files; 05 lists 30 files.
- must_haves derivation: PASS. Truths user-observable; artifacts + links concrete.
- Context (D-01..D-24): PASS. All decisions mapped to tasks; no contradictions.
- Scope reduction: PASS. Only benign hits (RED-precondition wording, "code-behind stays minimal").
- Deferred ideas: PASS. --urls/externals/artwork/trademark appear only as owner-action records, never as work.
- Arch tiers: PASS. Downward-only new edges; no new same-layer edge; LayerMap/slnx/slnf/Directory.Packages.props/ContractVersion untouched by files_modified (verified clean).
- Data contracts: PASS. Shared LegacyBrandNames.cs extensions sequenced 04(w3)->05(w4).
- CLAUDE.md (repo): no file at root of this checkout; plans obey cited conventions (ag-dotnet, MTP filters, Live-gate naming).
- Research resolution: PASS (no Open Questions section).
- Hygiene: PASS. Boundary-aware scan of all 7 PLAN.md: 0 literal legacy-token hits (all bracket-safe: omp[o]s, OMP[O]S, Omp[O][Ss], omp[O]s).
- 66.1 collision: PASS. 66 plans touch none of LayerMap.cs, ArchitectureTests csproj, Directory.Packages.props, slnx/slnf, ContractVersion.cs, seam files.
- 66-07 safety: PASS. Ports 5387/5380/5391, display :131, podman-ps foreign-container pre-check, double-down assert, no secret printing.

## Warnings (accepted deviations — no re-plan)
- W1 scope_sanity/66-04: 34 files_modified (15+ over generic threshold). Coherent single-resolver migration; splitting by site would break the tracer RED/GREEN flow. Accept.
- W2 scope_sanity/66-05: 30 files_modified. Same rationale; overlaps with 04 limited to 4 declared files sequenced by depends_on (verified). Accept.
- W3 scope_sanity/66-07: 7 tasks (5+ over threshold). All small read-only gate verifications, sequential in one wave-5 plan; splitting buys no parallelism. Accept.

## Info
- INFO-1: acceptance_criteria used instead of `<done>`, plan-level files_modified instead of per-task `<files>` — required by dispatch contract item 4; all present 1:1.
- INFO-2: 66-07 Task 3 record-only `grep -c '^-autonogrammer-test-'` pattern appears to never match real container names (leading dash); exit-status neutral (followed by `;`), records only. Suggest `autonogrammer-test-` for accurate records; no gate impact.

## Recommendation
No revision required. Warnings are intended coarse granularity per contract; proceed to execute-phase in wave order.
