# Phase 21: Parity Surface, Summary

**Status: complete.** All five ROADMAP criteria are verified in `21-VERIFICATION.md`. Criterion 4 covers the approval kinds the owner confirmed on 2026-10-24: edit, delete, move, execute, read and search. Criterion 5 queues steer and follow-up after the active turn, which is the owner's decision F3, because ACP v1 has no steer.

Session chat renders natively, and parity is tested on both heads. The native head composes the engine on the first send or decision. Sends stream through the in-process ACP host. A permission request for an admitted kind reaches the approval UI, and Approve or Deny settles it. Sent prompts echo into the transcript as user messages. Steer and follow-up messages sent during a run are held in send order, and each is sent as its own turn after the active turn ends. Context and decisions: `21-CONTEXT.md`. Plan: `21-01-PLAN.md`. Verification: `21-VERIFICATION.md`.

Mac channel: No. The parity tests run headless on the Linux host and the CI runners.

**No live omp turn was exercised.** Every send, approval and steering test runs against a scripted ACP peer (`tests/OmpOS.Native.Tests/Send/`).

## Success criteria (ROADMAP Phase 21)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | One test per streamed generative UI block type | Met | tests/OmpOS.Native.Tests/Blocks/NativeUiBlockRendererTests.cs; 45 inventory rows in docs/parity/session-chat-parity.md |
| 2 | Same case set runs against Razor and Avalonia | Met | 14 shared scenarios: tests/OmpOS.UI.Tests/SharedScenarioParityTests.cs and tests/OmpOS.Native.Tests/Parity/SharedScenarioParityTests.cs |
| 3 | Inventory names a test for each row | Met | tests/OmpOS.Presentation.Tests/ParityInventoryTests.cs |
| 4 | Tool approvals appear in the run UI; approve or deny reaches the ACP permission bridge and resolves the request | Met for the confirmed kinds | tests/OmpOS.Native.Tests/Send/EngineSendPathTests.cs: `AParkedHostPermissionRequestAppearsInTheApprovalUi`, `AnAdmittedKindRaisedByTheHostAppearsInTheApprovalUiAndApproveGrantsIt`, `AReadRequestRaisedByTheHostAppearsInTheApprovalUi`, `DenyingAParkedHostPermissionRequestSettlesItDenied`, `ApproveUnderDenyAllReportsRefusedAndLeavesTheParkedRequestPending`, `TheProductionPolicyDeniesAKindOutsideTheNamedSetBeforeItParks`. Production kinds: `Program.ProductionAllowedKinds` = edit, delete, move, execute, read, search |
| 5 | Steering and follow-up sent during a run, delivered in send order | Met, with the F3 delivery rule | tests/OmpOS.Native.Tests/Send/EngineRunInputSendTests.cs: `ASteerDuringATurnDoesNotCancelItAndIsSentAfterTheTurnEnds`, `ASteerThenAFollowUpAreSentInTheOrderTheyWereSent`, `AMessageSentAfterAQueuedFollowUpWaitsBehindIt`, `StopSendsNothingFurtherAndSaysHowManyMessagesWereNotSent`, `TwoHeldSteersAreCountedUntilEachStartsItsTurn`. tests/OmpOS.Presentation.Tests/RunInputSchedulerTests.cs: `ASteerWhileFollowUpsWaitGoesBehindThem` |

## Owner decisions and their implementation

- **F1, approval kinds (answered 2026-10-24).** The owner first answered "named kind set" without naming the kinds. The parent proposed edit, delete, move and execute, the ACP kinds that write files or run commands, from https://agentclientprotocol.com/protocol/v1/tool-calls. The owner then answered "also admit read and search", so the confirmed set is edit, delete, move, execute, read and search. fetch, think, switch_mode and other are denied automatically.
- **F2, prompt echo (answered: contract payload).** `UserMessagePayload` (kind `user_message`) is added to the shared contracts, and the folds turn it into `UserMessageBlock`. The block moved to `OmpOS.Contracts.Ui` so the projection can fold it. The native head echoes each prompt when it is sent, and each queued message when its turn starts. The server's `prompt_accepted` path is unchanged.
- **F3, steering (answered: queue after the turn).** Steer and follow-up messages are held in send order and sent as their own turns after the active turn ends. Stop cancels the turn, sends nothing further, and shows "N queued messages were not sent."

## Known limits

- (a) No live omp turn was exercised.
- (b) fetch, think, switch_mode and other are denied automatically. If omp requests permission for one of them, the user will see it denied.
- (c) ACP v1 has no steer. A steer or follow-up is delivered after the active turn, not into it.
- (d) `UserMessageBlock` serializes as kind `user` on write. Read degrades it to `UnknownBlock`, following the existing folded-only pattern in `UiBlockConverter.cs`.
- Pre-existing and outside this phase: `OnClosed` in `MainWindow.axaml.cs` still disposes the MCP host with an unobserved `_ = _mcp.DisposeAsync().AsTask();`. The composed host is cancelled and disposed on close, but no test covers the close path, and a killed process skips the dispose.

## Review and fixes

An independent review (a09-reviewer, read-only) returned CHANGES_REQUESTED. The findings were: the replay fold threw on `user_message` and `diagnostic` events; the SSE name for diagnostics was `unknown`; the composer copy said a steer was sent now; the fault path with held messages had no test; the fault reason and the not-sent line were in the wrong order; window close did not observe disposal; and the held-steer count reset after each turn. Each was fixed, and each fix has a named test. The held-steer count now follows the held queue across turns (`TwoHeldSteersAreCountedUntilEachStartsItsTurn`).

## Verification

Run first: `export DOTNET_ROOT=/root/.dotnet DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1; export PATH=/root/.dotnet:$PATH`.

- Repo gate after the last code change (the approval kinds): `./bootstrap.sh` printed `bootstrap: OK (SDK 10.0.402, locked restore, Release build, tests, format)`, total 2240, failed 0, skipped 0.
- Native test project after the last code change: 334 passed, 0 failed, 0 skipped.
- Lock files: the `OmpOS.Sessions` reference in `apps/OmpOS.Native/OmpOS.Native.csproj` broke the locked restore (NU1004). The lock files were regenerated with `dotnet restore OmpOS.Linux.slnf --force-evaluate`, and `dotnet restore OmpOS.Linux.slnf --locked-mode` then passes.

## Files changed

- Production: `apps/OmpOS.Native/Program.cs`, `App.axaml.cs`, `MainWindow.axaml.cs`, `Engine/EngineComposition.cs`, `Engine/EnginePromptChannel.cs` (new), `Engine/EngineRunInput.cs` (new), `Engine/UnconnectedRunInput.cs`, `Views/ComposerView.axaml.cs`, `OmpOS.Native.csproj`, and the regenerated `packages.lock.json` files.
- Shared: `src/OmpOS.Contracts` (user-message payload; `UserMessageBlock` moved to `Ui`), `src/OmpOS.Presentation/Runs/` (scheduler and `IRunInput`), `src/OmpOS.Sessions/HarnessEventProjection.cs`, `src/OmpOS.GenUi/UiFolder.cs`, `src/OmpOS.EngineHost/RemoteEngineHost.cs` (explicit arm), `services/OmpOS.Api/Sse/StreamEventKind.cs`.
- Tests: `tests/OmpOS.Native.Tests/Send/`, `tests/OmpOS.Native.Tests/RunControls/`, `tests/OmpOS.Presentation.Tests/`, `tests/OmpOS.Contracts.Tests/`, `tests/OmpOS.Sessions.Tests/`, `tests/OmpOS.UI.Tests/`.
- Parity: `docs/parity/session-chat-parity.md` (row 45).
- Records: `21-CONTEXT.md`, `21-01-PLAN.md`, `21-VERIFICATION.md`, `21-SUMMARY.md`.

## Commits

- f73faed: Phase 21 session chat, run controls and parity contract (with the Phase 20 native shell)
- 9e5913d: gate-green HEAD
- Uncommitted: this phase and the rest of the working tree. No commit was made (plan clarification q1).
