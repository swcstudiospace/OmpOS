# Plan 71-07 SUMMARY: Artifact request metadata and durable grant storage

Status: blocked — not executed. The plan itself forbids execution until an evidenced runtime rollout gate prevents new permission metadata being silently lost by existing senders/receivers and `ArtifactSyncService.ForkAsync` is proven to preserve it (71-07-PLAN.md:37). Plan 71-13 (privacy-only) does not clear that blocker. No rollout-gate evidence exists in the tree, so no task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- `src/Autonogrammer.Cowork.Artifacts/Model/ArtifactVersionRecord.cs` still has exactly 9 positional parameters (ArtifactId, Number, Content, ContentSha256, ParentNumber, ParentContentSha256, Author, CreatedAt, ChangeNote) — no bounded origin/reason request metadata. Task 1's core artifact is absent.
- `src/Autonogrammer.Cowork.Artifacts/Model/` contains no `ArtifactGrantBinding.cs`, and `src/Autonogrammer.Cowork.Artifacts/` contains no `IArtifactGrantStore.cs`. The only grant binding is the internal wire `Grant` record in `ArtifactWireContract.cs:83` and the internal `ArtifactGrantBinding` used inside the Stores assembly (`FileArtifactStore.cs:118`, `PostgresArtifactStore.cs:148`) — no public, API-consumable, owner-scoped grant-store contract. Task 3's core artifact is absent.
- `PostgresArtifactStore.cs:17,343,365-377` still stores version permissions as `{"language":"..."}` / `[]` only (language), with no per-version origin/reason round-trip. Task 2's artifact is absent.
- Therefore none of the plan's three must-have truths can be claimed: requests do not survive create/update/store round-trips, and no atomic public grant store exists.

## Blockers (unchanged from the plan)

- Runtime rollout gate: undemonstrated sender/receiver compatibility for new permission metadata, and unproven `ForkAsync` preservation (71-07-PLAN.md:37). Rolling out per-version requests before compatible senders and receivers can silently lose them.
- AG-71-17 remains open until both scoped approvals are enforceable and independently verified (71-07-PLAN.md:78).

## Not run

- `ArtifactToolProviderTests`, `FileArtifactStoreTests`, `PostgresArtifactStoreTests` (plan `<verify>` commands) — not run, because the code they test does not exist in the tree. Running them would test pre-existing 71-03/71-05 behavior, not this plan.
