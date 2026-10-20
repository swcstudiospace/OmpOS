---
phase: "2"
slug: "core"
status: validated
nyquist_compliant: true
wave_0_complete: true
created: "2026-10-20"
---

# Phase 2 — Validation Strategy

## Requirement Coverage

| Req | Automated Check(s) | Status |
|---|---|---|
| PARSE-01 | `dotnet test tests/OmpOS.Parser.Tests --filter "PromptParserTests.EmptyString\|PromptParserTests.WhitespaceOnly\|PromptParserTests.MaxPromptLengthTruncates"` | ✅ |
| PARSE-02 | `dotnet test tests/OmpOS.Parser.Tests --filter "PromptParserTests.MagicKeyword\|PromptParserTests.SlashCommand\|PromptParserTests.Mention\|PromptParserTests.Directive\|PromptParserTests.FenceIgnored"` | ✅ |
| PARSE-03 | `dotnet test tests/OmpOS.Parser.Tests --filter "RecognizerOrderingTests.FallbackUnremovable\|RecognizerOrderingTests.RecognizerExceptionBecomesDiagnostic"` | ✅ |
| PARSE-04 | `dotnet test tests/OmpOS.Parser.Tests --filter "RecognizerOrderingTests.CustomRecognizerRegisteredViaDI"` | ✅ |
| HARN-01 | `dotnet test tests/OmpOS.Harness.Abstractions.Tests --filter "HarnessContractShapeTests"` + `dotnet test tests/OmpOS.Harness.ContractTests` | ✅ |
| HARN-02 | `dotnet test tests/OmpOS.Harness.Abstractions.Tests --filter "HarnessStateMachineTests"` (19 allowed + 45 disallowed transitions) | ✅ |
| HARN-03 | `dotnet test tests/OmpOS.Harness.InMemory.Tests --filter "FailureModeTests"` (6 modes: ProcessStart, HandshakeTimeout, MalformedFrame, Disconnected, Backpressure, DuplicateCompletion) | ✅ |
| HARN-04 | `dotnet test tests/OmpOS.Harness.Acp.Tests --filter "AcpLiveSmokeTests\|AcpFrameReplayTests"` (live smoke against installed omp 18.12.3, fixture replay) | ✅ |
| HARN-05 | `dotnet test tests/OmpOS.Harness.Rpc.Tests --filter "RpcLiveSmokeTests\|RpcChunkReassemblyTests"` (live smoke + 8-chunk shuffled reassembly) | ✅ |
| HARN-06 | `dotnet test tests/OmpOS.Harness.InMemory.Tests --filter "WebSocketOmpTransportStubTests"` + `dotnet test tests/OmpOS.Harness.ContractTests` | ✅ |
| SESS-01 | `dotnet test tests/OmpOS.Sessions.Tests --filter "InMemoryRepositoryFixture\|SessionRepositoryContractTests"` | ✅ |
| SESS-02 | `dotnet test tests/OmpOS.Sessions.Tests --filter "SessionServiceTests.PromptInFlight\|SessionServiceTests.SteerFollowUp"` | ✅ |
| SESS-03 | `dotnet test tests/OmpOS.Sessions.Tests --filter "ProjectionReplayTests.ReplayEquality"` (23-event fixture) | ✅ |
| API-01 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| API-02 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| API-03 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| OPS-01 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| OPS-02 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| OPS-03 | *Not in Phase 2* — Phase 3/4 | ⏳ |
| OPS-04 | *Not in Phase 2* — Phase 3/4 | ⏳ |

## Manual-Only Verifications

| Req | Reason |
|---|---|
| API-01..03 | API project is a class-library placeholder in Phase 2; real endpoints land in Phase 3/4 |
| OPS-01..04 | Options/health/OTel/auth boundary implementations are in Phase 3/4 |

## Notes

- Phase 2 covers **14/20** requirements directly (PARSE-01..04, HARN-01..06, SESS-01..03).
- The remaining 6 (API-01..03, OPS-01..04) are structurally prepared (abstractions, in-memory adapter, session/service) but implemented in later phases.
- All 14 covered requirements have at least one passing automated test listed above.
- `nyquist_compliant: true` because every requirement assigned to this phase has a concrete passing command.
