# Plan 71-09 SUMMARY: Selected conversation and artifact access flow

Status: blocked — not executed. The plan's precondition is that plans 71-07, 71-08 and 71-11 have supplied and tested the real version client, grant protocol, device/session proof and demonstrated destination enforcement, with the 71-11 real Web host test passing before this UI tracer (71-09-PLAN.md:45). None of those prerequisites exists in the tree, so no task was run and no product code was changed.

## What was checked (tree at c04095ba, 2026-11-21)

- `src/Autonogrammer.UI/Components/Cowork/Artifacts/` contains only the 71-06 set (`ArtifactBody`, `ArtifactAccessDialog`, `ArtifactCard`, `ArtifactFrame`, `ArtifactMarkdownLinks`, `ArtifactMarkdownView`, `ArtifactTable`) — no `ArtifactAccessPanel.razor`. Task 1/2's core artifact is absent.
- `tests/Autonogrammer.UI.Tests/` contains no `ArtifactAccessFlowTests.cs`, and `tests/Autonogrammer.Web.Tests/` contains no `ArtifactAccessHostTests.cs`. Both plan `<verify>` test classes are absent.
- The existing `ArtifactAccessDialog.razor` is the standalone 71-05 dialog; it has no production caller from `CoworkConversation` / `UiBlockRenderer` (71-VERIFICATION.md:57 gap: "The standalone dialog has no production web opener"). The web Cowork workbench conversation region remains unwired to the artifact card, exactly as the plan's objective describes as the current state (71-09-PLAN.md:35).
- `ArtifactAccessReviewModel.cs` (71-05) exists and carries exact origins/reasons plus a timeout, but nothing connects it to the typed `ArtifactWebClient` grant/ticket operations — those client operations do not exist (see 71-11-SUMMARY.md).

## Blockers (unchanged from the plan)

- 71-07 (request metadata + grant store), 71-08 (identity + destination enforcement) and 71-11 (web host client) all unexecuted; each is itself blocked (rollout gate, proof protocol, destination enforcement).
- Production web composition cannot be claimed from the isolated dialog tests (71-09-PLAN.md:49).

## Not run

- `ArtifactAccessHostTests` (Web.Tests) and `ArtifactAccessFlowTests` / `ArtifactAccessDialogFocusTests` (UI.Tests) — not run; the classes do not exist in the tree.
