# Phase 2 · Plan 05 Summary — Sessions: repository, service, projection (SESS-01..03)

**Completed:** 2026-10-20 · **Agent:** sonic `P2Sessions` · **Verified:** orchestrator

## What landed

- `src/OmpOS.Sessions`: `ISessionRepository` (get/save/append/list-from-seq) + `InMemorySessionRepository` (atomic monotonic Seq per session under 10-way concurrency, no gaps/dupes), `SessionService` (`PromptInFlight` rejection via `ErrorCode.InvalidTransition`, steer/follow-up exempt queueing documented, completion from non-Running rejected), `HarnessEventProjection` (pure fold; TextDelta/UiBlock/Error/Completed → typed payloads; others → `UnknownPayload` with `OriginalKind` set).
- `tests/OmpOS.Sessions.Tests`: 35 tests green — contract fixture (`SessionRepositoryContractTests<T>`), `InMemoryRepositoryFixture`, `SessionServiceTests` (PromptInFlight + steer/follow-up), `ProjectionReplayTests` (23-event script → serialize → replay = identical).

## Deviations accepted

- `OmpOSError` is a record, not an `Exception` — `SessionException` wraps it with `.Error` (thrown instead).
- `ErrorCode.PromptInFlight` absent — used `ErrorCode.InvalidTransition`.
- `ToStreamEvents` takes explicit `timestamp` (spec forbids reading the clock).
- `ProjectionState` record equality by reference → tests compare serialized state.
- `AppendAsync` overwrites caller's `Seq` (repository assigns).
- Prompt ack = `UnknownPayload(kind="prompt_accepted")` (interpreted "envelope ack" as such).
- Non-object `UnknownPayload.Raw` likely doesn't round-trip via the converter (known, test uses object payloads).

## Verification

`dotnet test tests/OmpOS.Sessions.Tests` → 35/35. Locked restore clean. Architecture tests pass.