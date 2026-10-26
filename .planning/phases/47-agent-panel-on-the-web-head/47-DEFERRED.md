# Phase 47 Deferred: Agent Panel on the Web Head

Status: deferred in writing, 2026-10-26. No code was written for this phase. Its criterion is untouched.

## Criterion (unchanged, from `.planning/ROADMAP.md`)

1. The API exposes the stage events of a session and the web head renders them with the shared presentation models. Verify: API integration test and bunit test.

Requirement: AL-09 (web head half). Depends on Phase 46 and on v0.9.2 Phase 13 (hosted routed harness).

## Why it is deferred

| Reason | Where it is recorded |
|--------|----------------------|
| Phase 13 (hosted routed omp harness) is blocked on external criteria: criterion 2 (a prompt on the deployed API streams omp events) needs a Railway deploy, which is paused, and the sign-in authority is not configured (OD-03). The ROADMAP records the reason for this phase as "the hosted API runs the InMemory harness"; `docs/runbooks/hosting.md` says Production is configured for the ACP adapter and refuses the InMemory adapter at startup, so that wording is not confirmed by this record and the deferral rests on the reasons in the other rows. | `.planning/ROADMAP.md` (Phase 13 status, Phase 47 status, Blocked Records OD-03), `.planning/STATE.md` (Phases 12 to 14, deploys paused), `docs/runbooks/hosting.md` (Known blockers) |
| Railway deploys are paused, so a hosted panel cannot be deployed or checked. | `.planning/STATE.md`; OQ-61, OQ-70 in `docs/open-questions.md` |
| The web head is Blazor WebAssembly in the browser. It cannot spawn processes or read plugin state files, so a faithful panel needs new API endpoints that serve stage events. No such endpoint exists. | `.planning/phases/46-agent-panel/46-CONTEXT.md` (Deferred Ideas) |
| The plugins run inside omp processes on the machine that starts omp. [INFERENCE: a hosted panel would need the hosted API to expose the state of those processes; no such endpoint exists.] | `docs/plugins.md`, `docs/architecture.md` (Agent loop) |

## What was not built

No API endpoint for stage events, no web head view of the panel, no bunit test, no stub and no placeholder control. The native Avalonia head is the only head that shows the default Agent Panel. The web head keeps its existing views.

## Resume condition

Resume when the hosted routed harness (v0.9.2 Phase 13) is unblocked: Railway deploys allowed and the sign-in authority (`Api:Auth:Authority` and `Api:Auth:Audience`) configured by the owner. Then design the stage event endpoint first (an API contract change, `docs/api/`), then the web head rendering over the shared presentation models.

## Not verified

Nothing about this phase is verified. No test, run or screenshot exists for it.

Index of every deferred and blocked item: `docs/runbooks/deferred-and-blocked-2026-10-26.md`.
