---
status: passed
score: 5/5
phase: 21-parity-surface
verified: 2026-10-24
---

# Phase 21 (Parity Surface) Re-verification

Re-verification after the owner decision on approval kinds (2026-10-24).
Checked by grep and by reading the test bodies. No build, test, or format
command was run.

## Criteria

| criterion | status | evidence (file and test name) | notes |
|---|---|---|---|
| 1. Session chat renders the streamed generative UI blocks that the Razor head renders, with one test per block type | passed | `tests/OmpOS.Native.Tests/Blocks/NativeUiBlockRendererTests.cs`: `RendersMarkdownText`, `RendersCodeBlockWithCopyControl`, `RendersToolCallCardWithStatus`, `RendersPermissionOptionsVerbatimReadOnly`, `RendersPlanPanel`, `RendersDiffPlaceholder`, `RendersErrorBlock`, `RendersA2uiSubset`, `RemoteA2uiImageRendersFallbackLink`, `UndecodableInlineImageFallsBackToItsAltText`, `RendersUnknownCollapsed`, `RendersUserMessage`; inventory `docs/parity/session-chat-parity.md` (45 rows) | Cited names re-checked; each exists. Unchanged evidence per `21-SUMMARY.md`. |
| 2. Parity tests run against the presentation layer for the Razor head and the Avalonia head, with the same test cases for both | passed | `tests/OmpOS.UI.Tests/SharedScenarioParityTests.cs` (`SharedScenarioParityTests`, 14 shared scenarios) and `tests/OmpOS.Native.Tests/Parity/SharedScenarioParityTests.cs` (`SharedScenarioParityTests`) | Both files exist with the shared case set; unchanged evidence. |
| 3. A parity inventory lists each RCL and MAUI feature in this surface with the test that covers it | passed | `tests/OmpOS.Presentation.Tests/ParityInventoryTests.cs` (`ParityInventoryTests`; gate test fails when an inventory row has no test name or the named test does not exist) | File and gate behavior confirmed; unchanged evidence. |
| 4. Tool approvals appear in the run UI, and an approve or deny decision reaches the ACP permission bridge and resolves the pending request | passed | `apps/OmpOS.Native/Program.cs`: `ProductionAllowedKinds` is exactly `["edit", "delete", "move", "execute", "read", "search"]`; `tests/OmpOS.Native.Tests/Send/EngineSendPathTests.cs`: `TheProductionPolicyDeniesAKindOutsideTheNamedSetBeforeItParks` (uses `permissionKind: "fetch"`, asserts no approval row and the agent receives `reject`), `AReadRequestRaisedByTheHostAppearsInTheApprovalUi` (uses `permissionKind: "read"` under the production composition, asserts the approval row appears and approve grants `allow`), plus `AParkedHostPermissionRequestAppearsInTheApprovalUi`, `AnAdmittedKindRaisedByTheHostAppearsInTheApprovalUiAndApproveGrantsIt`, `DenyingAParkedHostPermissionRequestSettlesItDenied`, `ApproveUnderDenyAllReportsRefusedAndLeavesTheParkedRequestPending` | Test bodies read: `fetch` is the non-admitted kind; the `read` test exists and asserts UI reach plus grant under the production composition. |
| 5. Steering and follow-up prompts can be sent while a run is active and are delivered in send order | passed | `tests/OmpOS.Native.Tests/Send/EngineRunInputSendTests.cs`: `ASteerDuringATurnDoesNotCancelItAndIsSentAfterTheTurnEnds`, `ASteerThenAFollowUpAreSentInTheOrderTheyWereSent`, `AMessageSentAfterAQueuedFollowUpWaitsBehindIt`, `StopSendsNothingFurtherAndSaysHowManyMessagesWereNotSent`, `TwoHeldSteersAreCountedUntilEachStartsItsTurn`; `tests/OmpOS.Presentation.Tests/RunInputSchedulerTests.cs`: `ASteerWhileFollowUpsWaitGoesBehindThem` | Each cited test name re-checked; all exist. Delivery follows the F3 rule (after the active turn). |

## Known limits

(a) no live omp turn was exercised, every send and approval test uses a scripted ACP peer;
(b) the approval kinds edit, delete, move, execute, read and search were confirmed by the owner on 2026-10-24;
(c) fetch, think, switch_mode and other are denied automatically before they park;
(d) ACP v1 has no steer, so a steer or follow-up is delivered after the active turn, not into it (owner decision F3);
(e) UserMessageBlock serializes as kind user on write, and read degrades it to UnknownBlock, following the existing folded-only pattern in UiBlockConverter.cs.
