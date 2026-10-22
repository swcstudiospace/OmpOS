# Phase 30 compatibility and admin: status record

Scope: ADE-21 (VS Code extension compatibility) and ADE-22 (admin, analytics and team controls). Status as of 2026-10-22, at HEAD 9e5913d.

This record states status only. Nothing in it is built, run or deployed by this document. Phase 30 has no plan files yet (`.planning/ROADMAP.md`: Plans TBD).

## Sources

- `.planning/ROADMAP.md`, Phase 30 success criteria 1 to 5 (ADE-21 and ADE-22) and Phase 26 criterion 4 (cloud runs).
- `.planning/REQUIREMENTS.md`, ADE-21 (line 245) and ADE-22 (line 248), and XP-09 (line 173).
- `docs/open-questions.md`, rows OQ-60 (ADE-21 scope) and OQ-61 (ADE-22 deferral).
- `docs/runbooks/hosting.md`, sections on the Railway topology and Known blockers 1 and 5.
- `docs/plans/ade-harness-plan.md`, rows ADE-21 and ADE-22.

## Status key

- **MET**: the Phase 30 criterion holds, and the repository shows it.
- **NOT MET**: the criterion does not hold. The item stays deferred with the unmet criterion named.
- **NOT STARTED**: no code or contract exists for the item.

## ADE-21: VS Code extension compatibility

Status: NOT STARTED. Criterion 1 is MET. The item is not deferred by the roadmap, but its work has not begun.

| Criterion | Status | Evidence |
| --- | --- | --- |
| 1. XP-09 plugin host contract tests pass at HEAD | MET | Gate at HEAD 9e5913d: bootstrap OK, 1596 tests passed, 0 failed, 0 skipped. |
| 2. Compatibility report, one row per supported VS Code extension API, each backed by a passing contract test | NOT STARTED | No compatibility report or extension host code exists. |

Open decision: which VS Code extension APIs ship first. This needs an owner decision before work starts (OQ-60, owner product owner). Until it is decided, no ADE-21 code is merged.

No compatibility code ships in Phases 26 to 29. No stub endpoint or placeholder control was added for ADE-21.

## ADE-22: admin, analytics and team controls

Status: DEFERRED in writing. Criterion 2 is NOT MET.

- **Unmet criterion.** Phase 30 criterion 2 requires the cloud runs of Phase 26 criterion 4 to be live on the hosted API. They are not live.
- **Why they are not live.** ADE-07 (cloud runs) is NOT STARTED. It needs a contract change request for run status and diff. Its live run is BLOCKED by the Railway deploy state below.
- **Prerequisite 1: Railway deploys are paused.** `docs/runbooks/hosting.md` (Known blocker 5; the status note of 2026-10-21 in the deploy checklist) states that nothing has been deployed to Railway, that no Railway service or deploy exists, and that Railway refuses deploys for this account while they are paused.
- **Prerequisite 2: the sign-in authority is not configured.** `docs/runbooks/hosting.md` (Known blocker 1) states that the API requires an OIDC bearer token, that no identity provider authority is configured, and that the API is therefore not exposed publicly. This is the OD-03 gate.

Deferral rule: ADE-22 starts only when the cloud runs of Phase 26 criterion 4 are live on the hosted API, as Phase 30 criterion 2 requires. Until then no admin, analytics or team-control endpoint, and no placeholder control, is added (Phase 30 criterion 5). Tracked in OQ-61.

## Verification

- Phase 30 criterion 3 and 4 apply only when the item starts. They are not evaluated here.
- Phase 30 criterion 5 (no stub endpoint or placeholder control) is checked by a repository search for `ADE-21`, `ADE-22` and VS Code extension terms on 2026-10-22, outside `bin`, `obj`, `docs` and `.planning`. The only match is `.gitignore`. No code, endpoint or control matches.
- Mac channel: not involved. Phase 30 runs on the Linux gate.
