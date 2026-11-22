# Plan 72-14 SUMMARY: Real Native composition, approvals and navigation

Status: blocked — not executed, by the plan's own blocking decision. On 2026-11-10 the user retained both the D-12 PuppeteerSharp BlockList and D-13 Strict Allowlist requirements; the pinned 25.15.0 API documents them as mutually exclusive, and the plan says "Do not execute this plan or claim Strict complete until a compatible implementation establishes both named boundaries with real browser evidence" (72-14-PLAN.md:35). No such implementation exists in the tree. The cross-phase prerequisite (72-12, 72-13, 75-11 chain) is also absent. No task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- Task 1 artifacts absent: `tests/Autonogrammer.Native.Tests/Browser/BrowserProductionSafetyTests.cs` does not exist. The only Browser composition tests are the 72-08 `BrowserUseCompositionTests.cs`, which exercise the baseline `BrowserUseComposition.Wire`. That composition now builds a real `CoworkApprovalBroker`, `FileCoworkGrantStore` and `FileCoworkToolAudit` (`BrowserUseComposition.cs:239,264-270,290-295`), but `AgentHead.cs` still wires it with only `StateDirectory` + `Artifacts` (lines 110, 230) — no `Configure: o => o.Enabled = true`, no real `Pages` (the provider receives `request.Pages ?? new ClosedPages()`, line 247), no `Shell` (broker `windowAvailable: false`). So `offerTools` (line 237) stays false and no `browser_*` tools are offered in the desktop Cowork head (72-VERIFICATION.md:31,54,61).
- Task 2 artifacts absent: `tests/Autonogrammer.Native.Tests/Browser/BrowserProductionEgressLiveTests.cs` does not exist. No test instantiates the actual Native `AgentHead` + Cowork composition against real Chromium through a session guard.
- Navigation boundaries absent as independent controls: `src/Autonogrammer.Harness.BrowserUse/Policy/NavigationGate.cs` exists from 72-04, but there is no independent PuppeteerSharp BlockList configuration in production code, no Fetch interception authorizing each redirect/frame before continuation (the post-`GoAsync` redirect decision remains), and no default-off / `AllowDispatchSessions` matrix test through actual composition. The two named boundaries required by the blocking decision were never established together.
- `BrowserUseComposition.cs` exists (72-08) and `AgentHead.cs` wires it, but the production defaults-off / Dispatch gating, per-session approvals, scoped grants and required audit the plan requires were not implemented or proven.

## Blockers (unchanged from the plan)

- D-12 BlockList + D-13 Strict Allowlist mutually exclusive in pinned PuppeteerSharp 25.15.0 — needs a compatible implementation with real browser evidence before any execution.
- 72-12 (guarded session) and 72-13 (required audit) unexecuted; 75-11 transitive chain not integrated.
- No live omp model turn authorized (OD-07).

## Not run

- `BrowserProductionSafetyTests`, `BrowserUseCompositionTests` (new cases), `NavigationGateTests` (new cases) and `BrowserProductionEgressLiveTests` (live) — not run as plan evidence; the new classes do not exist. The pre-existing 72-08 `BrowserUseCompositionTests` and `NavigationGateTests` are covered by the Phase 72 VERIFICATION re-run, not claimed here.
