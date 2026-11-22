# Plan 71-08 SUMMARY: Sync, trusted identity and destination enforcement

Status: blocked — not executed. The plan is blocked until the destination-enforcement prerequisite is resolved (71-08-PLAN.md:36): no documented, evidenced mechanism prevents private destinations and DNS rebinding for a user's-device artifact iframe without breaking the locked isolation. Task 2 additionally requires an approved, server-verifiable device/session proof protocol that is "currently unresolved; halt rather than fabricate it" (71-08-PLAN.md:54). No such evidence or protocol exists in the tree, so no task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- Dependencies absent: plan 71-07's store/wire contracts do not exist (see 71-07-SUMMARY.md: no `IArtifactGrantStore.cs`, no request metadata on `ArtifactVersionRecord`), and plan 71-12's sync producer does not exist. Task 1's precondition ("Plans 71-07 and 71-12 supply the store and real producer contracts") is unmet.
- Existing API surface is the 71-04 baseline: `services/Autonogrammer.Api/Cowork/Artifacts/` holds `ArtifactEndpoints.cs`, `ArtifactAccountDirectory.cs`, `ArtifactGrants.cs`, `ArtifactGrantDocument.cs`, `ArtifactRenderTickets.cs`, `ArtifactsJsonContext.cs`. `ArtifactGrants.cs` still consumes the internal wire `Grant` DTO, not a public owner-scoped `IArtifactGrantStore`; no device/session proof-of-possession exists.
- `ArtifactRenderTickets.cs` tickets bind (account, artifact, version, hash) with 60 s expiry (71-VERIFICATION.md:126) — they do not bind a verified device, session, or selected grant, and no Fetch-interception / approved-destination enforcement exists. Task 3's precondition (documented, tested private-address/rebinding enforcement) is unmet; per the plan, "stop and escalate; do not mark the task or AG-71-17 complete" (71-08-PLAN.md:63).

## Blockers (unchanged from the plan)

- Destination enforcement for private-address resolution and DNS rebinding: no evidence (D-01, D-19, D-23). CSP hostname matching alone does not prove the property.
- Device/session proof protocol: unresolved; caller-supplied device/session strings are not identity (T-71-08-01).
- AG-71-17 remains open; an unavailable fallback is not phase completion (71-08-PLAN.md:78).

## Not run

- `ArtifactWireRoundTripTests`, `ArtifactEndpointsTests`, `ArtifactRenderTicketTests`, `ArtifactDocumentPolicyTests` (plan `<verify>` commands) — not run as plan evidence, because the contracts they would exercise (71-07/71-12) do not exist. Pre-existing 71-04 endpoint/ticket tests are covered by the Phase 71 VERIFICATION re-run, not by this plan.
