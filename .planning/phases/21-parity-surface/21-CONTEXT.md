# Phase 21 Context: Approval Path and Send Path on the Production Engine

Scope: Phase 21 success criterion 4 (ROADMAP.md, Phase 21): "Approve or deny reaches the ACP permission bridge".
Out of scope: criterion 5 (steering and follow-up over ACP). Steering code is unchanged.

## Decisions

| # | Decision | Source |
|---|----------|--------|
| D1 | The production entry composes the engine with an empty allowed-kind list, so every permission request is denied by default. `Program.ProductionAllowedKinds` is `[]`, and `Program.Main` builds the app over `Program.ComposeProductionEngine`. | `apps/OmpOS.Native/Program.cs`; `docs/open-questions.md` Q-APPROVAL-ADAPTER ("Production approvals stay deny by default") |
| D2 | Composition is lazy. `App(Lazy<EngineComposition>)` shares one `Lazy` between the approval bridge (`App.ComposedApprovalBridge`) and the window's send channel. The first decision or the first send composes the engine. Launch composes nothing and starts no process. | `apps/OmpOS.Native/App.axaml.cs`; `apps/OmpOS.Native/MainWindow.axaml.cs` (constructor); test `TheFirstSendComposesTheEngineAndLaunchComposesNothing` |
| D3 | The send path is `EnginePromptChannel.SubmitAsync`, which calls `IEngineHost.SubmitPromptAsync` on the composed `InProcessEngineHost`. A submitted draft starts a turn in the background, and the composer clears the draft once the host accepts it. `Composer.IsStreaming` blocks a second send while a turn streams. | `apps/OmpOS.Native/Engine/EnginePromptChannel.cs`; `src/OmpOS.EngineHost/InProcessEngineHost.cs`; `src/OmpOS.Presentation/Composer/ComposerViewModel.cs` |
| D4 | Transcript model: each `HarnessEvent` is converted to a `StreamEvent` by the existing pure projection `OmpOS.Sessions.HarnessEventProjection.ToStreamEvents` and folded by `SessionViewModel.ApplyEvent`. The native app gained a project reference to `OmpOS.Sessions` for this one call. | `src/OmpOS.Sessions/HarnessEventProjection.cs`; `apps/OmpOS.Native/MainWindow.axaml.cs` (`ShowTurnEvent`); `apps/OmpOS.Native/OmpOS.Native.csproj` |
| D5 | A failure of the host, including a failure to start `omp acp`, is shown inline as an `ErrorEvent`, which folds to an `ErrorBlock` and sets `SessionViewState.FatalError`. The composer is freed. Nothing is swallowed silently. | `apps/OmpOS.Native/MainWindow.axaml.cs` (`StreamTurnAsync`); `src/OmpOS.Presentation/State/SessionViewModel.cs` |
| D6 | Stop cancels the in-flight turn through `IEngineHost.CancelAsync`, which sends ACP `session/cancel`. With no turn running, the existing copy "No response is running." is shown. | `apps/OmpOS.Native/MainWindow.axaml.cs` (`StopTurnAsync`); `src/OmpOS.Harness.Acp/AcpAgentSession.cs` (`CancelAsync`) |
| D7 | Approvals: a host `PermissionRequestEvent` becomes a `PendingToolApproval` row in `RunControls.Approvals`, with session id `shell-session` and request id `RequestId ?? CallId`. That is the same key `PendingPermissionResolver` parks the request under, so the existing `ToolApprovalCoordinator` and `EngineToolApprovalBridge` settle it. Deny settles the request as denied. Approve under deny-all returns `Refused` and leaves the request pending. | `apps/OmpOS.Native/MainWindow.axaml.cs` (`ShowTurnEvent`); `apps/OmpOS.Native/Engine/PendingPermissionResolver.cs`; `apps/OmpOS.Native/Engine/EngineToolApprovalBridge.cs`; `src/OmpOS.Presentation/Approvals/ToolApprovalCoordinator.cs` |
| D8 | Working directory and omp executable: no new decision. `AcpClientOptions` defaults apply: `WorkingDirectory` is `Environment.CurrentDirectory`, which is also the window's workspace default, and `OmpPath` is null, so `omp` resolves from `PATH`. | `src/OmpOS.Harness.Acp/AcpClientOptions.cs`; `src/OmpOS.Harness.Acp/OmpAcpClient.cs` |
| D9 | Steering is superseded by owner decision F3. `EngineRunInput` holds each steer or follow-up in send order, and the window sends it as a turn once the active turn ends. `RunInputScheduler` still dispatches queued follow-ups. | `apps/OmpOS.Native/Engine/EngineRunInput.cs`; `apps/OmpOS.Native/MainWindow.axaml.cs` (`DriveTurnsAsync`); `docs/open-questions.md` OQ-04, OQ-59 |
| D10 | Closing the window disposes the composed host, which closes the `omp acp` child. A window that never composed an engine disposes nothing. | `apps/OmpOS.Native/MainWindow.axaml.cs` (`OnClosed`); `apps/OmpOS.Native/Engine/EnginePromptChannel.cs` (`DisposeAsync`) |
| D11 | Test seam: `EngineComposition.Compose` takes an optional `transportFactory`. Production passes none, so the host spawns `omp acp`. Tests pass a scripted ACP peer, so no live omp turn runs. | `apps/OmpOS.Native/Engine/EngineComposition.cs`; `tests/OmpOS.Native.Tests/Send/` |

## Findings that constrain criterion 4

F1. Under the production policy no approval row can appear. `AcpAgentSession.AnswerPermissionAsync` consults the policy first. A kind outside the allowlist is denied at once, with an informational diagnostic on the turn, and no `PermissionRequestEvent` is emitted (`src/OmpOS.Harness.Acp/AcpAgentSession.cs`, `IsGrantedByPolicy`; `src/OmpOS.Harness.Acp/AcpApprovalPolicy.cs`, `Allows`). With `Compose([])`, the approve and deny controls therefore never see a real request. They are wired and tested, but reachable only once an allowlist admits a tool kind. Which kinds to admit is an owner decision. Resolved by owner decision F1 (named kinds), see Owner decisions.

F2. The sent prompt is not echoed into the transcript. `OmpOS.Contracts.Streaming` has no user-message payload, and the projection cannot represent one. This is a transcript-model question, not a wiring gap. Resolved by owner decision F2 (user-message event), see Owner decisions.

## Tests

Added in `tests/OmpOS.Native.Tests/Send/EngineSendPathTests.cs`:
`TheFirstSendComposesTheEngineAndLaunchComposesNothing`, `TheProductionEngineComposesOnTheFirstDecisionAndSettlesAParkedAdmittedRequestAsAllowed`, `TheProductionPolicyDeniesAKindOutsideTheNamedSetBeforeItParks`, `AnAdmittedKindRaisedByTheHostAppearsInTheApprovalUiAndApproveGrantsIt`, `ASentPromptAppearsInTheTranscriptAsAUserMessage`, `ASendSubmitsThePromptThroughTheComposedHostAndStreamsTheReply`, `AParkedHostPermissionRequestAppearsInTheApprovalUi`, `DenyingAParkedHostPermissionRequestSettlesItDenied`, `ApproveUnderDenyAllReportsRefusedAndLeavesTheParkedRequestPending`, `ASendThatFailsShowsItsReasonInlineInTheTranscript`, `StopCancelsTheInFlightTurnThroughTheHost`.

Support: `Send/ScriptedAgent.cs` (scripted `omp acp` peer that answers the handshake, sessions, prompts, permissions and cancel) and `Send/ScriptedAgentTransport.cs` (in-memory `IAcpTransport`).

Added in `tests/OmpOS.Native.Tests/Send/EngineRunInputSendTests.cs` (steer, order, queue and Stop, against a peer that holds each turn):
`ASteerDuringATurnDoesNotCancelItAndIsSentAfterTheTurnEnds`, `ASteerThenAFollowUpAreSentInTheOrderTheyWereSent`,
`AMessageSentAfterAQueuedFollowUpWaitsBehindIt`, `StopSendsNothingFurtherAndSaysHowManyMessagesWereNotSent`.
Each was checked to fail when the drain is removed (`EngineRunInput` holds nothing).

Added in `tests/OmpOS.Presentation.Tests/RunInputSchedulerDiscardTests.cs`: `DiscardingQueuedFollowUpsDropsThemWithoutDispatch`,
`DiscardingWithNothingQueuedDropsNothing`. Added in `tests/OmpOS.Contracts.Tests/Streaming/StreamEventJsonTests.cs`: the
`UserMessagePayload` round trip (theory row). Added in `tests/OmpOS.UI.Tests/UserMessagePayloadFoldTests.cs`:
`UserMessagePayloadFoldsToOneUserTurn`.

## Owner decisions (2026-10-24)

| # | Owner answer | Implementation | Source |
|---|---|---|---|
| F1 | Named kind set. The owner answered "named kind set" without naming the kinds. The parent chose `edit`, `delete`, `move` and `execute` from the ACP tool-kind list. The owner then answered "also admit read and search". Admitted: `edit`, `delete`, `move`, `execute`, `read`, `search`. Auto-denied before they park: `fetch`, `think`, `switch_mode`, `other`. | `apps/OmpOS.Native/Program.cs` (`ProductionAllowedKinds`). Tests: `TheProductionEngineComposesOnTheFirstDecisionAndSettlesAParkedAdmittedRequestAsAllowed`, `TheProductionPolicyDeniesAKindOutsideTheNamedSetBeforeItParks` (non-admitted kind `fetch`), `AnAdmittedKindRaisedByTheHostAppearsInTheApprovalUiAndApproveGrantsIt`, `AReadRequestRaisedByTheHostAppearsInTheApprovalUi`. | Owner answers. Kind list: https://agentclientprotocol.com/protocol/v1/tool-calls |
| F2 | Contract payload: a user-message event is added to the shared contracts. | `src/OmpOS.Contracts/Streaming/UserMessagePayload.cs` (kind `user_message`), registered in `StreamEventPayloadConverter` and `OmpOsJsonContext`. The shared folds (`src/OmpOS.Sessions/HarnessEventProjection.cs` and `src/OmpOS.GenUi/UiFolder.cs`) turn it into `UserMessageBlock`, which now lives in `OmpOS.Contracts.Ui` so the projection can fold it. Both heads render it. `MainWindow` echoes the prompt at send (`StartTurnAsync`) and each held message when its turn starts (`DriveTurnsAsync`). Parity row 45 in `docs/parity/session-chat-parity.md`. | Owner answer. The existing `prompt_accepted` path (server) is unchanged. |
| F3 | Queue after the turn. Steer and follow-up messages are delivered after the current turn, in send order. Nothing in flight is cancelled. | `apps/OmpOS.Native/Engine/EngineRunInput.cs` (new, composed path only). `MainWindow.axaml.cs`: `DriveTurnsAsync` drains the held messages and `StopTurnAsync` raises the stop flag. `ComposerView.SubmitAsync`: while a follow-up waits, a send queues behind it, so the order holds. `RunInputScheduler.DiscardQueuedFollowUps` drops queued follow-ups on Stop, and Stop shows "N queued messages were not sent." | Owner answer. ACP v1 has no steer operation (`AcpAgentSession.cs`, steer throws `NotSupportedException`). |

Changes made while implementing these decisions:

- `src/OmpOS.Sessions/HarnessEventProjection.cs` maps `DiagnosticEvent` to `DiagnosticPayload`, as `HarnessEventMapper` already does. Before this, a request the policy denied (which emits a diagnostic) ended the native turn with a `FatalError`. The projection doc says every harness event yields one stream event, and the old code threw for diagnostics.
- The composed window takes the engine's own run input. `MainWindow` throws if a composed engine is given any other `IRunInput`. `App` passes `EngineRunInput` on the composed path. The uncomposed path still uses `UnconnectedRunInput`.
- `IRunInput.SteerAsync` remains the contract. The ACP engine holds the message until the turn ends, which is the behaviour owner decision F3 specifies.
