---
phase: 2
reviewed: 2026-10-20
status: findings
counts:
  critical: 1
  high: 2
  medium: 7
  low: 3
  total: 13
---

# Phase 2 (Core) — Code Review

Scope: `src/OmpOS.Parser`, `src/OmpOS.Harness.Abstractions`, `src/OmpOS.Harness.InMemory`,
`src/OmpOS.Harness.Acp`, `src/OmpOS.Harness.Rpc`, `src/OmpOS.Sessions` and their tests.
Checked against VF-1/VF-2 (`.planning/phases/02-core/02-RESEARCH.md`) and the four hard rules
(parser never throws; deny-by-default permissions; strict id correlation; source-generated serialization).

Verified clean (no finding): parser never throws — `PromptParser.Parse` catches per-recognizer
(`FindMatch`) and end-to-end (`Parse`), the `FallbackRecognizer` terminator is hardwired last and
returns a shared result the parser copies, so a throwing/misbehaving recognizer becomes a diagnostic;
`AcpApprovalPolicy.Allows` is deny-by-default, rejects empty kinds, consults the narrowing hook only
after the allowlist passes, so the hook can never grant more than configured; id correlation is strict
on both wires (ACP removes the pending entry on dispatch so duplicates are ignored, not re-applied;
RPC matches `prompt_result` to the single active slot and faults exact-duplicate completions);
all `JsonSerializer` calls in scope carry a source-generated `JsonTypeInfo`
(`AcpJsonContext`, `OmpRpcJsonContext`, `RpcIdConverter`) — no reflection-based serialization found.

## Critical

### CR-01 — Unbounded ACP prompt-turn channel (memory exhaustion by update flood)

**Severity:** critical — `src/OmpOS.Harness.Acp/AcpAgentSession.cs:859`

**What is wrong:** `PromptTurn.Events` is `Channel.CreateUnbounded<HarnessEvent>`. Every
`session/update` notification the agent sends is `TryWrite`-n into it (`HandleSessionUpdate`),
and the pump never applies backpressure. The RPC side bounds the equivalent stream at 4096 events
and faults with `BackpressureHarnessException` (`OmpRpcSession.Publish`, `PromptSlot`); the ACP side
has no bound at all.

**Why it matters to a consumer:** a misbehaving or compromised `omp acp` engine (or a bug emitting
updates in a tight loop) grows the turn channel without limit while the consumer is still reading,
exhausting process memory with no fault, no diagnostic, and no way for the caller to intervene —
`SubmitPromptAsync` just keeps yielding.

**Fix:** use a bounded channel for the turn (same 4096 capacity as `PromptSlot`) and, on
`TryWrite` refusal, fail the turn and fault the session exactly like `OmpRpcSession.Publish` does:

```csharp
public Channel<HarnessEvent> Events { get; } = Channel.CreateBounded<HarnessEvent>(
    new BoundedChannelOptions(4096) { SingleReader = true });
```

```csharp
if (!turn.Events.Writer.TryWrite(harnessEvent))
{
    FailTurn(turn, new BackpressureHarnessException("omp acp produced events faster than the consumer read them.", "acp"));
    // plus fault the session: the wire state after a dropped update is unknown
}
```

## High

### HI-01 — RPC pump lets unexpected exceptions escape without failing the session (prompt stream hangs forever)

**Severity:** high — `src/OmpOS.Harness.Rpc/OmpRpcSession.cs:340-355` (pump), `~224-232` (dispose)

**What is wrong:** `PumpAsync` catches `HarnessException` and `IOException`/`ObjectDisposedException`
only. Any other exception from the pump path — a custom `IRpcTransport` throwing (e.g.
`InvalidOperationException` from a test double or future transport), an `InvalidHarnessTransitionException`
from a state move, or any regression in `Dispatch`/`CompletePrompt` — propagates out of `PumpAsync`
without calling `Fail`. Consequences: `_pending` command waiters hang until the 60s command timeout,
and worse, the active prompt's consumer hangs **forever** (there is no stream-event timeout; the slot
channel is never completed). Additionally, `DisposeAsync` awaits `_pump` with no guard, so the raw
non-`HarnessException` escapes `DisposeAsync` itself.

**Why it matters to a consumer:** one unexpected transport exception turns a live prompt stream into
an indefinite hang for the end user instead of a typed `DisconnectedHarnessException`, and it makes
`await using` on the session throw an untyped exception.

**Fix:** widen the pump catch-all and harden dispose:

```csharp
catch (Exception ex)
{
    _ = Fail(ex is HarnessException hex ? hex
        : new DisconnectedHarnessException("The omp session failed.", Context, ex));
}
```

and in `DisposeAsync`, never let the pump observation throw:

```csharp
if (_pump is { } pump)
{
    try { await pump.ConfigureAwait(false); }
    catch (Exception) { /* session already failed; disposal must not throw */ }
}
```

### HI-02 — `SessionService` dead-ends sessions in `AwaitingToolApproval`

**Severity:** high — `src/OmpOS.Sessions/SessionService.cs:186`

**What is wrong:** `RecordCompletionAsync` accepts only `SessionStatus.Running`
(`session.Status != SessionStatus.Running` → `InvalidTransition`), yet `IsPromptActive` treats
`Running` **and** `AwaitingToolApproval` as active, `SubmitPromptAsync` rejects prompts in both,
and `SubmitSteerAsync` explicitly supports steering an `AwaitingToolApproval` session. Nothing in
scope ever transitions a session back from `AwaitingToolApproval` to `Running` (or directly to a
terminal state): there is no approval-resolution API. A session that enters `AwaitingToolApproval`
can never be completed through this service — every settle attempt throws `InvalidTransition`.

**Why it matters to a consumer:** the API/worker layer driving tool approval has no service-level path
to finish the turn: approve/deny the tool, and `RecordCompletionAsync` still throws. Sessions wedge in
`AwaitingToolApproval` permanently, and subsequent prompts on them are rejected with `PromptInFlight`
forever.

**Fix:** either accept `AwaitingToolApproval` as a settleable state in `RecordCompletionAsync`, or
(preferable, explicit lifecycle) add the missing transition, e.g.:

```csharp
public Task ResolveToolApprovalAsync(string sessionId, bool approved, CancellationToken ct)
// approved  -> status back to Running (prompt continues)
// denied    -> record denial event; caller then settles via RecordCompletionAsync
```

and cover `Running ↔ AwaitingToolApproval ↔ terminal` in `SessionServiceTests`.

## Medium

### MED-01 — ACP pump masks non-harness failures and lets raw exceptions escape dispose

**Severity:** medium — `src/OmpOS.Harness.Acp/AcpAgentSession.cs:400-418` (pump), `~196-207` (dispose)

**What is wrong:** `PumpAsync` catches only `HarnessException`. The pump thread invokes untrusted
caller code — `_options.ApprovalPolicy.Allows(permission)` inside `AnswerPermissionAsync` — and an
arbitrary `IAcpTransport.SendAsync` implementation. If the narrowing hook throws (any user bug:
`NullReferenceException`, etc.), the pump faults with the raw exception: `finally` reports a generic
`DisconnectedHarnessException("omp acp closed the stream.")`, destroying the real cause, the raw
exception stays on `_pumpTask`, and `DisposeAsync` (which catches only `HarnessException` from the
pump await) rethrows the raw exception to the disposer.

**Why it matters to a consumer:** a one-line bug in an approval predicate converts a diagnosable
failure into a misleading "stream closed" error plus a throwing dispose — exactly the two places a
consumer looks first.

**Fix:** isolate the untrusted hook and harden the pump/dispose:

```csharp
bool granted;
try { granted = _options.ApprovalPolicy.Allows(permission); }
catch (Exception ex)
{
    // fail-closed: deny, and record the hook failure as a diagnostic event
    ActiveTurn()?.Events.Writer.TryWrite(new ErrorEvent($"Approval policy failed: {ex.GetType().Name}. Denied.", false));
    granted = false;
}
```

plus catch-all in `PumpAsync` that records the real exception as `failure`, and a guarded pump await
in `DisposeAsync` (catch `Exception`, not just `HarnessException`).

### MED-02 — RPC silently ignores `prompt_result` for unknown ids (stream hangs instead of faulting)

**Severity:** medium — `src/OmpOS.Harness.Rpc/OmpRpcSession.cs:417-431`

**What is wrong:** `CompletePrompt` → `ClaimActivePrompt` returns null for any id that is not the
active prompt; only an exact repeat of the last completed id faults (`DuplicateCompletion`). Any
other unknown id falls through to `return` — dropped without a diagnostic. In a single-prompt session
there is no legitimate unknown `prompt_result`, so a mis-correlated completion leaves the consumer
blocked on the slot channel until it cancels; there is no stream timeout.

**Why it matters to a consumer:** an engine bug (or id-echo regression) that shifts one completion id
presents as an infinite stall rather than a loud, typed protocol fault, and the session looks healthy
(`Streaming`) while no progress is possible.

**Fix:** fault unknown completions the same way duplicates fault:

```csharp
PromptSlot? slot = ClaimActivePrompt(frame.Id);
if (slot is null)
{
    throw Fail(new MalformedFrameHarnessException($"prompt_result for unknown prompt id '{frame.Id}'.", Context));
}
```

(keeping the `DuplicateCompletionHarnessException` carve-out first if both signals are wanted).

### MED-03 — `SessionService` per-session gates grow without bound

**Severity:** medium — `src/OmpOS.Sessions/SessionService.cs:38`

**What is wrong:** `_gates` is a `ConcurrentDictionary<string, SemaphoreSlim>` with `GetOrAdd` on
every operation and no removal or disposal. Every session id ever touched leaves a `SemaphoreSlim`
(plus kernel handle on first blocking wait) in the dictionary for the process lifetime.

**Why it matters to a consumer:** on a long-lived server this is a slow memory/handle leak proportional
to total sessions served — the exact shape that only shows up in production after weeks.

**Fix:** evict idle gates (reference-counted gate holder removed when the count drops to zero), or
replace per-id semaphores with a bounded striped-lock array (`SemaphoreSlim[64]` hashed by session id).
Do not naively `TryRemove` after release — that reintroduces the check-and-set race the gate exists to
prevent.

### MED-04 — `RpcChunkAssembler` accumulates incomplete chunk groups forever

**Severity:** medium — `src/OmpOS.Harness.Rpc/RpcChunkAssembler.cs:18,85-95`

**What is wrong:** total buffered *bytes* are capped (`MaxReassembledFrameBytes`), but the *number* of
incomplete chunk groups is not, and abandoned groups are never evicted. A sender trickling one chunk
under many distinct `chunkId`s (each `Parts` array holds up to `MaxChunkCount` = 256 entries) grows
`_pending` without limit and never triggers the byte cap. The 64-entry `_completed` memory bounds the
opposite side but not this one.

**Why it matters to a consumer:** a buggy engine (or a malicious frame source in tests/fuzzing) can
drive unbounded memory growth through small, individually legal chunks.

**Fix:** cap the group count (e.g. refuse a new `chunkId` beyond N in-flight groups with
`MalformedFrameHarnessException`) and/or evict groups by age/size — the assembler is pump-owned, so a
simple count check in `AcceptChunk` before `_pending.Add` suffices.

### MED-05 — InMemory dispose surfaces raw `ObjectDisposedException` to streaming consumers

**Severity:** medium — `src/OmpOS.Harness.InMemory/InMemoryOmpSession.cs:255` (`ReadNextAsync`)

**What is wrong:** when the session is disposed mid-stream, the next `ReadNextAsync` throws a raw
`ObjectDisposedException` (via the pre-await disposed check) instead of a typed `HarnessException`.
Both real sessions complete the stream with a typed failure (`DisconnectedHarnessException` via
`FailTurn`/`Fail`); the in-memory stand-in is supposed to be contract-identical
(`InMemoryOmpSessionContractTests`, `OmpSessionContractTests`).

**Why it matters to a consumer:** contract tests and any consumer `catch (HarnessException)` logic
written against real harnesses will not catch the in-memory session's dispose signal — behavior
diverges exactly where the fake must match.

**Fix:** throw (or complete the channel with) `DisconnectedHarnessException` instead of
`ObjectDisposedException` in `ReadNextAsync` when `_disposed`, e.g.:

```csharp
lock (_gate)
{
    if (_disposed)
    {
        throw new DisconnectedHarnessException("The session was disposed while streaming.", SessionId);
    }
}
```

### MED-06 — `OmpRpcClient.OpenAsync` dispose can mask the original start failure

**Severity:** medium — `src/OmpOS.Harness.Rpc/OmpRpcClient.cs:61-65`

**What is wrong:** the `catch (Exception)` around `start(session)` awaits `session.DisposeAsync()`,
which itself can throw (faulted pump propagates from the unguarded pump await, HI-01). When it does,
the dispose exception replaces the original handshake/start exception.

**Why it matters to a consumer:** the error a caller sees for a failed start becomes an unrelated
dispose/pump artifact, misdirecting diagnosis of version-pin, handshake, and resume failures.

**Fix:** guard the compensating dispose:

```csharp
catch (Exception)
{
    try { await session.DisposeAsync().ConfigureAwait(false); }
    catch (Exception) { /* original exception below is authoritative */ }
    throw;
}
```

### MED-07 — WebSocket stub `CloseAsync` throws, so disposing any session built on it throws

**Severity:** medium — `src/OmpOS.Harness.InMemory/WebSocketOmpTransportStub.cs:31`

**What is wrong:** every operation on the stub fails with `ProcessStartHarnessException` — including
`CloseAsync`. `AcpAgentSession.DisposeAsync` calls `_transport.CloseAsync` unconditionally, so
`await using` a session built over this transport throws from dispose.

**Why it matters to a consumer:** dispose must never throw; any current or future test/consumer that
composes the stub transport with a real session gets disposal explosions unrelated to what is being
tested.

**Fix:** make `CloseAsync` (and `DisposeAsync`, already a no-op) succeed unconditionally — an
unconnected stub has nothing to close:

```csharp
public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
```

## Low

### LOW-01 — Inconsistent process-start/write error mapping across transports and probe

**Severity:** low — `src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:69`,
`src/OmpOS.Harness.Rpc/OmpVersionProbe.cs:60`, `src/OmpOS.Harness.Rpc/OmpRpcProcess.cs:SendLineAsync`

**What is wrong:** `OmpRpcProcess.Start` and `OmpVersionProbe.StartProbe` catch only `Win32Exception`
from `Process.Start`, while `AcpStdioTransport.StartProcess` catches `Win32Exception` **or**
`InvalidOperationException`. `UnauthorizedAccessException` (non-executable bit, directory permissions)
escapes all three as a raw BCL exception instead of the typed `ProcessStartHarnessException` the
contract promises. Similarly, `OmpRpcProcess.SendLineAsync` maps only `IOException`, while
`AcpStdioTransport.SendAsync` maps `IOException`/`ObjectDisposedException` — a disposed-stdin
`ObjectDisposedException` escapes the RPC transport raw.

**Why it matters to a consumer:** two deployment failures with the same operational meaning surface as
different exception types depending on which transport started the engine, breaking uniform
`catch (ProcessStartHarnessException)` / `catch (DisconnectedHarnessException)` handling.

**Fix:** catch `(Win32Exception, InvalidOperationException, UnauthorizedAccessException)` in both
`Start` paths, and add `ObjectDisposedException` to the `SendLineAsync` filter, matching
`AcpStdioTransport`.

### LOW-02 — ACP agent requests with string ids fault the whole session

**Severity:** low — `src/OmpOS.Harness.Acp/AcpAgentSession.cs:818-826` (`TryGetId`), used at `~426-447`

**What is wrong:** request/response correlation requires a JSON-number id; anything else
(strings are legal JSON-RPC 2.0 ids) raises `Malformed` and faults the session — including for
agent-initiated requests such as `session/request_permission`, where the correct behavior is to
answer with a JSON-RPC error, not tear down the session.

**Why it matters to a consumer:** one spec-legal frame from a future engine version kills a healthy
session instead of producing a per-request error the turn could survive.

**Fix:** accept string ids end-to-end (key `_pending` by raw id text like `RpcIdConverter` does), or
at minimum answer non-numeric agent requests with `InvalidParams`/`MethodNotFound` instead of
faulting. No change needed for the client’s own outbound ids (VF-1 capture shows numeric).

### LOW-03 — `InMemorySessionRepository.SaveAsync` can clobber a longer event log on count ties

**Severity:** low — `src/OmpOS.Sessions/InMemorySessionRepository.cs` (`SaveAsync`)

**What is wrong:** the log-preservation guard is `if (session.Events.Count >= entry.Log.Count)` —
a stale session snapshot whose event count merely *ties* the current log overwrites it. The guard
prevents shorter-over-longer clobbers but not equal-length stale overwrites from direct repository
users racing outside the `SessionService` gate.

**Why it matters to a consumer:** silent event-log divergence between what was appended and what a
later `GetAsync`/replay sees — the exact class of bug the SESS-03 replay-equality guarantee exists
to exclude.

**Fix:** only replace the log when the incoming events extend it (compare the common prefix / last
`Seq`), or document that `SessionService`’s per-session gate is the only supported writer and make
out-of-band `SaveAsync` with events an error.
