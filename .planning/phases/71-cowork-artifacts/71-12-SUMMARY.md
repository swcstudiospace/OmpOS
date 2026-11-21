# Plan 71-12 SUMMARY: Privacy-preserving sync producer and grant consumers

Status: blocked — not executed. The plan forbids execution until a documented, evidenced runtime rollout gate prevents newly added permission metadata from being silently lost by existing senders and receivers, and `ArtifactSyncService.ForkAsync` is proven to preserve it; plan 71-13 "neither establishes the rollout gate nor clears this blocker" (71-12-PLAN.md:27). No rollout-gate evidence exists in the tree, so no task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- Task 1's disabled-sync allowlist behavior is already in the tree — but it was delivered by plan 71-13, not by this plan: `SyncMetadata` (`ArtifactWireContract.cs:86-108`) carries exactly id, title, kind, versionCount, createdAt, updatedAt and hashes; `RememberGrant`, its `_grants` state and the DTO `Grants` property are gone; `ArtifactSyncService.Metadata(pending)` (`ArtifactSyncService.cs:191`) builds grant-free metadata. This plan's Task 1 would only re-prove 71-13's fix.
- Task 2 is absent: the enabled-sync wire representation still carries only raw version bytes + hash (`ArtifactWireContract.MediaType`, `VersionPath`, `Hash`). There is no per-version permission/request metadata in the enabled form, no `SendOrQueueAsync` permission mapping, and `tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/ArtifactSyncServiceTests.cs` contains only the three 71-13-era cases (`Disabled_sync_sends_metadata_only`, `Same_hash_is_idempotent_and_divergence_forks_without_merge`, `Offline_queue_retries_in_order_and_stays_idempotent`) — no two-version request/reason, retry or fork permission cases. Task 2's core artifact is absent.
- Grant-consumer migration is absent: `ArtifactGrants.cs` (API) and `ArtifactSyncService` still reference the internal wire `Grant` DTO as a transport type; there is no public `IArtifactGrantStore` to migrate them onto (see 71-07-SUMMARY.md).

## Blockers (unchanged from the plan)

- Runtime rollout gate for permission metadata + proven `ForkAsync` preservation: undemonstrated.
- 71-07 (metadata + grant store) unexecuted; 71-08 (API round-trip) depends on this plan and remains blocked behind it.

## Not run

- `ArtifactSyncServiceTests` (Stores.Tests) — the pre-existing 71-13 cases are run as 71-13 evidence (71-13-SUMMARY.md), not as this plan's Task 1/2 evidence. No new permission-carrying cases exist to run.
