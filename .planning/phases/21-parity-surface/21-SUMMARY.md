# Phase 21: Parity Surface, Summary

**Status: partially complete.** Criteria 1 to 3 are met. Criteria 4 and 5 are open on the production engine path.

Session chat renders natively and parity is tested on both heads. Tool approvals and steering have their coordinators and tests, but the production engine wiring is not connected.

Mac channel: No. The parity tests run headless on the Linux host and the CI runners.

## Success criteria (ROADMAP Phase 21)

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | One test per streamed generative UI block type | Met | tests/OmpOS.Native.Tests/Blocks/NativeUiBlockRendererTests.cs; 44 inventory rows in docs/parity/session-chat-parity.md |
| 2 | Same case set runs against Razor and Avalonia | Met | 14 shared scenarios: tests/OmpOS.UI.Tests/SharedScenarioParityTests.cs and tests/OmpOS.Native.Tests/Parity/SharedScenarioParityTests.cs |
| 3 | Inventory names a test for each row | Met | tests/OmpOS.Presentation.Tests/ParityInventoryTests.cs (guard green) |
| 4 | Approve or deny reaches the ACP permission bridge | Open | Approval coordinator (ADE-02) tested with tests/OmpOS.Native.Tests/RunControls/ToolApprovalDecisionTests.cs and ScriptedApprovalBridge.cs. No IToolApprovalBridge implementation calls the ACP bridge; the default UnconnectedToolApprovalBridge fails visibly |
| 5 | Steering and follow-up sent during a run, in send order | Open | Run input scheduler (ADE-03) tested with tests/OmpOS.Native.Tests/RunControls/ComposerRunInputTests.cs. Steer is not sent to an engine |

## Open items

- Production approval path: add an IToolApprovalBridge implementation that calls the ACP permission bridge. Owner: engineering. Route: ROADMAP Phase 21 criterion 4.
- Production steering path: deliver steer and follow-up prompts to the engine during an active run. Owner: engineering. Route: ROADMAP Phase 21 criterion 5.

## Commits

- f73faed: Phase 21 session chat, run controls and parity contract (with Phase 20 native shell)
- 9e5913d: gate-green HEAD (ADE-02 and ADE-03 coordinators on the gate-green tree)
