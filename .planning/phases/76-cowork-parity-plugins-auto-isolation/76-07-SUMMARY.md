---
phase: 76-cowork-parity-plugins-auto-isolation
plan: 07
subsystem: usage
tags: [usage-ledger, acp, export, avalonia]

requires: []
provides:
  - FileUsageLedger JSONL with 90-day retention and mode 0600
  - UsageMeterModel per-session and per-day sums and UsageUnavailable
  - IUsageHistorySource and MainView.CreateExport real history
  - Usage cost on usage_update mapped to ContextUsageEvent.Cost
affects: [76-06]

tech-stack:
  added: []
  patterns:
    - "One ledger row per end-of-turn usage_update; occupancy is last and peak, never a sum"
    - "Session cost is the last reported amount; a day's cost sums those session amounts"
    - "MainView.UsageHistorySource is assigned by 76-06; null keeps the empty export history"

key-files:
  created:
    - src/Autonogrammer.Harness.AgentLoop/Usage/FileUsageLedger.cs
    - src/Autonogrammer.Harness.AgentLoop/Usage/UsageLedgerEntry.cs
    - src/Autonogrammer.Presentation/Usage/UsageMeterModel.cs
    - src/Autonogrammer.Presentation/Usage/IUsageHistorySource.cs
    - src/Autonogrammer.Native.Views/Views/Usage/UsageMeterView.axaml
  modified:
    - src/Autonogrammer.Harness.Abstractions/ContextUsageEvent.cs
    - src/Autonogrammer.Harness.Acp/AcpWireMapping.cs
    - src/Autonogrammer.Native.Views/MainView.axaml.cs

key-decisions:
  - "OQ-460: cost.amount is treated as session-cumulative from one recorded frame; a session's cost is its last amount"
  - "Export history is one UsageRecord per session and day, input tokens = last occupancy, output tokens = 0, model name local"
  - "UsageMeterView is internal; 76-06 registers it and assigns UsageHistorySource"

patterns-established:
  - "UsageUnavailable is the only text for a missing cost; the meter never shows zero in that case"

requirements-completed: [AG-76-12]

duration: 45min
completed: 2026-10-30
---

## Verification

Class-filtered Release tests that ran: `FileUsageLedgerTests` 3 passed, `UsageMeterModelTests` 2 passed, `AcpUsageAndResourcesTests` 4 passed, `OmpLiveVariantContractTests` 4 passed. `UsageMeterViewTests` did not run: `tests/Autonogrammer.Native.Tests` references `apps/Autonogrammer.Native`, which failed unrelated MA0048, CA1822, CA1859, CA2000 and IDE0005 errors in Cowork and ComputerUse files this plan does not own. `Autonogrammer.Native.Views` itself builds with 0 warnings.

## What shipped

- `usage_update` cost (`amount`, `currency`) maps to optional `ContextUsageEvent.Cost`. A frame without cost stays equal to the two-argument event.
- `FileUsageLedger` appends one JSON object per line to `usage-ledger.jsonl`, rewrites atomically, mode 0600, prunes rows older than 90 days, and stores no prompt content.
- `UsageMeterModel` shows last and peak occupancy per session, last session cost, and a day cost that sums those session amounts. Missing cost is `UsageUnavailable`.
- `MainView.UsageHistorySource` feeds `CreateExport`. Null or empty stays the previous empty history.
- `UsageMeterView` renders the sums and the unavailable state with accessible names. View registration stays with 76-06.

## Open question

OQ-460: whether `cost.amount` is session-cumulative is inferred from one recorded live frame (`session-update-usage-update.json`, amount 0.0059281 USD). The meter uses the last reported amount as the session cost.

## Deviations

- `AcpStdioTransport` was mid-edit and did not compile (missing `_logger`, MA0015 on wrapped-launch checks). A minimal compile fix was applied so the usage tests could build. That file is not part of this plan's commit.

## Verification

Class-filtered Release tests: `FileUsageLedgerTests` (3), `UsageMeterModelTests` (2), `AcpUsageAndResourcesTests` (4), plus the contract and native filters named by the plan.

## Self-Check: PASSED
