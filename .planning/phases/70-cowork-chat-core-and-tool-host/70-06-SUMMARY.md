---
phase: 70-cowork-chat-core-and-tool-host
plan: 06
subsystem: cowork-conversation
tags: [cowork, conversation, accessibility]

requires:
  - 70-05
provides:
  - CoworkConversationModel over SessionViewModel and ComposerViewModel
  - Native and Web conversation, composer and approval card views
affects: []

actuals:
  tokens: 0
  tasks: 3
  commits: 1

tech-stack:
  added: []
  patterns:
    - "Presentation models take SessionViewModel and ComposerViewModel; no Native.Views or Cowork reference"
    - "Approval cards render host-observed Target and Details only"
    - "Cowork image refusal copy is This model cannot read images"

key-files:
  created:
    - src/Autonogrammer.Presentation/Cowork/CoworkConversationModel.cs
    - src/Autonogrammer.Presentation/Cowork/CoworkApprovalQueue.cs
    - src/Autonogrammer.Presentation/Cowork/ProviderStatusChip.cs
    - src/Autonogrammer.Native.Views/Views/Cowork/CoworkConversationView.axaml
    - src/Autonogrammer.UI/Components/Cowork/CoworkConversation.razor
    - src/Autonogrammer.UI/wwwroot/styles/cowork-chat.css
  modified:
    - src/Autonogrammer.Native.Views/ViewMapping.cs
    - src/Autonogrammer.UI/wwwroot/styles/autonogrammer.css

key-decisions:
  - "Image limits match AcpImageLimits defaults through AttachmentLimits, with no Harness reference"
  - "Extra types are separate files so MA0048 holds"
  - "CoworkApprovalQueue keeps the D-28 name; CA1711 is SUP-689"

patterns-established:
  - "Focus order is transcript, composer, approvals on both heads"

requirements-completed: [AG-70-4, AG-70-11]

# Phase 70 Plan 06: Conversation views

One presentation model drives the Native and Web Cowork conversation: transcript, composer, approval cards, provider chips and attachment chips.

## Behaviour

`CoworkConversationModel` composes `SessionViewModel`, `ComposerViewModel`, `CoworkApprovalQueue` of `Contracts.Controls.PendingToolApproval`, and `ProviderStatusChip` (Available, Needs setup, Unavailable with reason). A model that reads no images disables image tools and image attachments with "This model cannot read images", and that flag can be changed in place. `AttachmentCopy.ModelRefuses` is not shown. An image over 8 / 10 MB / 25 MB, or a missing file, shows the reason on the chip and blocks send. Remove clears the chip.

## Heads

Native views are mapped by additive `ViewMapping.Create` cases. Interactive controls have accessible names. Reduced motion sets `reduced-motion`. Web components declare no `@page` and inject no auth, API, Cowork or Harness type. `cowork-chat.css` uses tokens only, has no transition, animation or keyframes, and is imported once before `a11y.css`. The remove control uses the 24 px target token.

## Gaps

Native accessibility tests were not executed: `apps/Autonogrammer.Native` does not compile (sibling Cowork and connector errors), and `SubagentActivityUpdate` currently inherits `EventArgs` from a record, which blocks a fresh Presentation build. Presentation model tests and UI tests passed before that break. CssContractTests and StyleAccessibilityTests passed `--no-build` against the stylesheet on disk (49 tests).

## Commits

- `feat(70-06):` conversation models and both heads

## Self-Check: PASSED
