# Plan 71-13 SUMMARY: Sync-off privacy repair

Status: complete (privacy-only fix; no phase-wide or AG-71-17 completion claimed, per 71-13-PLAN.md:58).

## What landed (by file, tree at c04095ba, 2026-11-21)

- `src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactWireContract.cs`: the uncalled internal `RememberGrant` method, its `_grants` state, and the `Grants` property/assignment on `SyncMetadata` are deleted. `SyncMetadata` (lines 86-108) now carries exactly `Id`, `Title`, `Kind`, `VersionCount`, `CreatedAt`, `UpdatedAt`, `Hashes` — the DTO property itself was removed, so the source-generated serializer cannot emit an empty `grants` array. The independent internal wire `Grant` DTO (line 83) is preserved as transport data for the Stores/API consumers; actual grant storage, rendering, approval and enabled-sync paths are unchanged.
- `src/Autonogrammer.Cowork.Artifacts.Stores/ArtifactSyncService.cs`: `Metadata(pending)` (line 191) builds grant-free metadata from the pending artifact; the disabled-sync path (`PushMetadataAsync`, line 125) sends only that allowlist. No request metadata, grant activation or deployment was added.
- `tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/ArtifactSyncServiceTests.cs`: `Disabled_sync_sends_metadata_only` drives the real `ArtifactSyncService.PushAsync` → `RetryAsync` through the real `HttpArtifactRemote` to a recording handler, parses every outbound JSON body and asserts the property-name set is exactly `["id","title","kind","versionCount","createdAt","updatedAt","hashes"]`, the distinctive content string appears in no body, and no header contains grant/permission/origin/reason. The two enabled-sync cases (idempotency/fork, offline retry) are retained unchanged.
- `tests/Autonogrammer.Api.IntegrationTests/Artifacts/ArtifactWireRoundTripTests.cs`: `SyncOffMetadataDoesNotChangeExistingGrant` (line 47) PUTs a real grant via the authenticated API, sends grant-free source-generated metadata through the real remote path, and asserts the pre-existing grant is unchanged (`grants.List("alice", id)` before = after). The metadata-only `PutAsync` early-return branch is documented as not proving listing persistence.

## Evidence (run 2026-11-21, SDK 10.0.402, worktree auto-78)

| Command | Result |
| --- | --- |
| `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Stores.Tests -c Release -- --filter-class "*ArtifactSyncServiceTests*"` (plan `<verify>`) | Passed: total 3, failed 0, skipped 0 |
| Phase 71 SC1: `dotnet test --project tests/Autonogrammer.Cowork.Artifacts.Tests -c Release --filter-not-trait "Category=Live"` | Passed: total 32, failed 0, skipped 0 (includes hostile-corpus inert-rendering tests) |

The plan's Task 2 `<verify>` (`ArtifactWireRoundTripTests` via Api.IntegrationTests) was not re-run as this plan's evidence; the current-tree focused result already recorded in 71-VERIFICATION.md:109 (`ArtifactWireRoundTripTests` passed 2/2, including preservation of an existing grant) covers it, and the full Api.IntegrationTests project is run in the Phase 71 VERIFICATION re-run.

## Deviations

None. No new request metadata, grant activation, deployment or enabled-sync change (71-13-PLAN.md:26).

## Gaps (explicitly not claimed)

- Compatibility with external older receivers remains unknown; only the current source receiver's observed response and unchanged grant state are evidenced (71-13-PLAN.md:48).
- The metadata-only endpoint's HTTP 200 does not prove listing persistence; the endpoint returns before parsing/persisting the body.
- Plans 71-07 and 71-12 remain blocked on the separately evidenced runtime rollout gate; this fix does not clear that blocker.
