# 70-01 Summary

The Cowork tool host is the one seam Artifacts, Browser Use and Computer Use use. `InvokeAsync` runs offer, argument cap, preview, policy, approval, provider call, result caps and audit in that order. An over-cap result is a fixed failure, not a truncation. A tainted session asks for every actuating or destructive call even when Skip is on and a grant is stored. `ByProviderGate` returns Allow from the host gate; the shared contract base fails a provider that never asks the broker with `MustBeLocal`.

## Tests

`/tmp/ag-bin/ag-dotnet test --project tests/Autonogrammer.Cowork.Tests/Autonogrammer.Cowork.Tests.csproj -c Release` with the five filter classes: 25 passed, 0 failed.

## Gaps

- D5 types are one file each because MA0048 rejects the two-file layout in the plan. `CoworkToolEvent` stays a record, so `CA1003` and `MA0046` are suppressed (`SUP-678`, `SUP-679`).
- Provider, gate, broker and end-session faults use `CA1031` (`SUP-680` to `SUP-685`). Long methods use `MA0051` (`SUP-686` to `SUP-688`).
- Auto follows Manual for provider calls, as the plan inferred from ADR-0037.
