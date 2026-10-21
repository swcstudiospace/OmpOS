---
phase: 2
reviewed: 2026-10-20T00:00:00Z
depth: deep
files_reviewed: 41
status: findings
counts:
  critical: 0
  high: 2
  medium: 2
  low: 4
  info: 1
  total: 9
prior_findings_reverified: 13
prior_findings_fixed: 13
---

# Phase 2 (Core) — Final Code Review

Scope: all of `services/OmpOS.Api`, `tests/OmpOS.Api.IntegrationTests`, plus the
fix-up edits in `src/OmpOS.Harness.Rpc`, `src/OmpOS.Harness.Acp`,
`src/OmpOS.Harness.InMemory`, `src/OmpOS.Sessions`, and `src/OmpOS.Parser` as
they now stand, checked against `02-REVIEW.md` (13 findings) and the priority
probes (body limits, content-type, rate-limit partitioning, CORS, SSE
replay/tail, Last-Event-ID, disconnect cancellation, log redaction,
ProblemDetails leakage, suppressions).

## Prior findings — verification

Every earlier finding was re-read against the current source. All 13 are fixed,
and each fix is correct, not merely present:

- **CR-01 (fixed)** — `AcpAgentSession.cs:927,934` bounds the turn channel at
  `TurnCapacity = 4096`; `WriteTurnEvent` (`:756`) fails the turn with
  `BackpressureHarnessException`, completes the writer with the error so the
  consumer unblocks (`FailTurn`, `:732`), and faults the session
  (`TryFaultLocked`). Matches the RPC slot semantics.
- **HI-01 (fixed)** — `OmpRpcSession.cs:451` pump has a catch-all that records
  the real exception as a typed `DisconnectedHarnessException` via `Fail`;
  dispose (`:316`) swallows the observed pump task so disposal never throws.
- **HI-02 (fixed)** — `SessionService.cs` gained `RequestToolApprovalAsync` and
  `ResolveToolApprovalAsync` (both outcomes return to `Running`; no direct
  `AwaitingToolApproval` → terminal transition), and `RecordCompletionAsync`
  still only settles `Running`. The lifecycle is now closed and documented.
- **MED-01 (fixed)** — the approval hook is isolated in `AnswerPermissionAsync`
  (`:545`): a throwing hook denies closed and writes a diagnostic
  `ErrorEvent` naming only the exception type; the pump (`:405`) records the
  real failure instead of masking it; dispose (`:162`) catches `Exception`.
- **MED-02 (fixed)** — `CompletePrompt` faults unknown ids with
  `MalformedFrameHarnessException` while keeping the exact-duplicate
  carve-out first. No silent drop remains.
- **MED-03 (fixed)** — per-session `SemaphoreSlim` dictionary replaced with a
  fixed `SemaphoreSlim[64]` striped array hashed by FNV-1a (`GateFor`,
  `StripeIndex`). Bounded; same-session atomicity preserved.
- **MED-04 (fixed)** — `RpcChunkAssembler` caps in-flight groups at
  `MaxIncompleteGroups = 64` and rejects the 65th with
  `MalformedFrameHarnessException` before allocating. Byte cap retained.
- **MED-05 (fixed)** — `InMemoryOmpSession.ReadNextAsync` throws typed
  `DisconnectedHarnessException` on dispose, matching the real sessions.
- **MED-06 (fixed)** — `OmpRpcClient.StartSessionAsync` guards the
  compensating dispose with an inner try/catch; the start exception stays
  authoritative.
- **MED-07 (fixed)** — `WebSocketOmpTransportStub.CloseAsync` is an
  unconditional no-op; dispose over the stub cannot throw.
- **LOW-01 (fixed)** — `OmpRpcProcess.Start` and `OmpVersionProbe.StartProbe`
  catch `(Win32Exception, InvalidOperationException,
  UnauthorizedAccessException)`; `SendLineAsync` maps
  `(IOException, ObjectDisposedException)`. Consistent with `AcpStdioTransport`.
- **LOW-02 (fixed)** — agent requests with string ids are answered with a
  JSON-RPC error echoing the raw id (`AnswerStringIdRequestAsync`) instead of
  faulting the session; string-id *responses* still fault, which is correct
  because this client only issues numeric ids.
- **LOW-03 (fixed)** — `SaveAsync` only replaces the log when the incoming
  events strictly extend it (`Count > Log.Count` plus a `Seq`-prefix conflict
  check); equal-length stale saves keep the stored log and update the header.
  The count-tie clobber is gone.

## New findings

### F-01: SSE resume at or past the terminal Seq on a settled session hangs instead of closing

**Severity:** high — `services/OmpOS.Api/Sse/SessionEventStreamer.cs:50`

**What is wrong:** `SubscribeAsync` replays events with `Seq > afterSeq`, then
unconditionally `await signal.WaitAsync(...)`. When the session is already
settled and `afterSeq` is at or beyond the terminal event's Seq, the replay
yields nothing and there will never be another append, so the request hangs
until the client times out or disconnects. A stale permit left by the settle
`Notify` only buys one spurious loop iteration before the hang. API-02
requires the stream to end with a terminal event; this path never ends at all.

**Why it matters to a consumer:** any client that reconnects with
`Last-Event-ID` equal to the terminal id — exactly what SSE/EventSource
clients do after a completed stream — gets a hung connection per reconnect,
accumulating open connections and tasks instead of an immediate close.

**Fix:** after the replay pass, break when the log already ends in a terminal
event at or below the watermark. The streamer holds the repository, so:

```csharp
await foreach (var evt in _repository.ListFromSeqAsync(sessionId, last + 1, cancellationToken).ConfigureAwait(false))
{
    last = evt.Seq;
    // ... yield, break on terminal as today ...
}

// No new events, and the session already settled: close instead of tailing forever.
var current = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
if (current is not null && current.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled)
{
    yield break;
}

await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
```

Add an integration test: complete a session, then `GET /events` with
`Last-Event-ID` set to the terminal Seq and assert the stream closes promptly
with zero items.

### F-02: Rate-limiter partitions are keyed by attacker-controlled session id with no eviction

**Severity:** high — `services/OmpOS.Api/Security/SessionRateLimiterPolicy.cs:47`

**What is wrong:** `GetPartition` returns one `TokenBucketRateLimiter` per
`RouteValues["id"]` (falling back to `"global"`). The rate-limiter middleware
caches one limiter instance per partition key indefinitely, and each
`TokenBucketRateLimiter` with `AutoReplenishment: true` owns a timer. The
route id is unauthenticated attacker input evaluated before the endpoint's
404 check, so `POST /sessions/<random>/prompts` mints a new limiter plus
timer per distinct id, forever.

**Why it matters to a consumer:** an unauthenticated client can grow server
memory and timer count without bound (and every legitimately prompted session
also leaks one limiter permanently), turning the abuse control itself into a
denial-of-service surface.

**Fix:** stop keying limiter lifetime on unbounded input. For Phase 2
(single-dev principal, no auth identity worth partitioning on), use a single
global bucket and document the per-session upgrade as blocked on real auth:

```csharp
public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
    RateLimitPartition.GetTokenBucketLimiter(
        "prompts",
        _ => new TokenBucketRateLimiterOptions { /* unchanged */ });
```

If per-session fairness is required before auth exists, resolve the session
first (endpoint filter that 404s unknown ids *before* limiting) or front the
policy with a bounded LRU of idle-expiring limiters — do not cache one limiter
per arbitrary route value.

### F-03: `SessionEventStreamer._signals` grows one entry per session forever

**Severity:** medium — `services/OmpOS.Api/Sse/SessionEventStreamer.cs:56`

**What is wrong:** both `Notify` (`:39`) and `SubscribeAsync` (`:56`)
`GetOrAdd` a `SessionSignal` (a `SemaphoreSlim`, i.e. a kernel handle after
the first blocking wait) per session id, and nothing ever removes or disposes
an entry — including signals for sessions that settled months ago.

**Why it matters to a consumer:** on a long-lived server this is a slow
handle/memory leak proportional to total sessions served — the same shape as
the just-fixed MED-03, reintroduced one layer up.

**Fix:** evict the signal when the stream ends and the session is terminal
(see F-01's fix site — the natural place: after `yield break` on terminal,
`_signals.TryRemove(sessionId, out var s); s?.Dispose();`), taking care that
concurrent subscribers re-create the entry via `GetOrAdd`. At minimum dispose
drained signals for terminal sessions on a periodic sweep and prove with a
test that settled sessions leave no entry behind.

### F-04: `POST /sessions` has no rate limit, and created sessions accumulate unboundedly

**Severity:** medium — `services/OmpOS.Api/Endpoints/SessionEndpoints.cs:33`,
`services/OmpOS.Api/Services/SessionRegistry.cs:13`

**What is wrong:** only `POST /sessions/{id}/prompts` carries
`RequireRateLimiting`; session creation is unlimited and unauthenticated, and
every created id is retained forever in both `SessionRegistry._ids` and the
repository. There is no delete/TTL path.

**Why it matters to a consumer:** anyone who can reach the API can grow
server memory without bound at the cost of one cheap JSON POST per session,
with no 429 ever firing.

**Fix:** apply a (global, per F-02) rate-limit policy to `POST /sessions` and
decide the retention story: either a session TTL/eviction pass over registry
plus repository, or an explicit "Phase 2 keeps everything; single-dev only"
note in the endpoint docs so the exposure is a recorded decision rather than
an accident. Minimum viable fix is the creation limiter plus a test that
over-budget creation returns 429 with `Retry-After`.

### F-05: Inbound `x-correlation-id` is unvalidated and persisted into the event log

**Severity:** low — `services/OmpOS.Api/Middleware/CorrelationIdMiddleware.cs:36`

**What is wrong:** any inbound header value (up to Kestrel's header-size
limits, ~32 KB) is adopted verbatim as `TraceIdentifier`, echoed in the
response, placed in the log scope, and — via `SubmitPromptAsync`'s
`context.TraceIdentifier` — stored as `StreamEvent.CorrelationId` on *every*
event the prompt appends, amplifying one header into N log records.

**Why it matters to a consumer:** log bloat and trace-backend cardinality
from attacker-shaped ids (whitespace, 8 KB blobs, non-printables), plus
odd characters echoed back in a response header.

**Fix:** accept only a tight shape, mint otherwise:

```csharp
var inbound = context.Request.Headers[HeaderName].FirstOrDefault();
var correlationId = Guid.TryParse(inbound, out _) && inbound!.Length <= 128
    ? inbound
    : Guid.CreateVersion7().ToString("N");
```

### F-06: Resume positions are not validated (`since`/`Last-Event-ID` negative or `long.MaxValue`)

**Severity:** low — `services/OmpOS.Api/Endpoints/EventStreamEndpoints.cs:46-69`,
`services/OmpOS.Api/Sse/SessionEventStreamer.cs:57`

**What is wrong:** a negative `afterSeq` replays the whole log (harmless but
unintended), and `afterSeq == long.MaxValue` makes `last + 1` wrap to
`long.MinValue` in unchecked arithmetic, replaying the entire log to a client
that asked for "everything after the end". API-02 promises client-side
dedup-by-Seq, so this is tolerated today, but the server should not rely on
client dedup for a value it can cheaply sanitize.

**Why it matters to a consumer:** a single corrupt/stale bookmark (or a
fuzzer) turns a resume into a full-log dump the client must discard.

**Fix:** clamp at the boundary: `var afterSeq = Math.Max(0, header ?? query ?? 0);`
and compute the floor as `last == long.MaxValue ? long.MaxValue : last + 1`
(or `checked` and break on overflow). Return 400 for negatives to match the
existing invalid-resume contract.

### F-07: `HarnessEventMapper` throws on unknown event subtypes despite promising degradation

**Severity:** low — `services/OmpOS.Api/Harness/HarnessEventMapper.cs:32`

**What is wrong:** the type's own doc comment says engine-specific events
"degrade to unknown-kind payloads", and every known subtype does — but the
default arm throws `ArgumentOutOfRangeException`. The first new
`HarnessEvent` subtype added to Abstractions will fail the whole background
run (settled `Failed` via `PromptRunner`'s catch-all) instead of degrading.

**Why it matters to a consumer:** a harness-side feature addition becomes an
API-side outage for every prompt that emits the new event, rather than a
gracefully unknown payload the UI already knows how to render.

**Fix:** degrade instead of throwing:

```csharp
_ => Unknown(evt.GetType().Name, ("type", evt.GetType().FullName ?? "unknown")),
```

### F-08: `PromptRunner` can evict a newer run's tracking entry when an older run settles late

**Severity:** low — `services/OmpOS.Api/Services/PromptRunner.cs:109`

**What is wrong:** `Start` overwrites `_pending[sessionId]` unconditionally,
and each continuation does a blind `TryRemove(key, out _)`. The 409 gate
makes overlap rare, but sequential prompts can interleave: run 1's
continuation may execute after run 2's `Start`, removing run 2's entry. Run 2
then runs untracked — an unobserved fault surfacing as
`BackgroundRunFaulted`-less silence and a missed shutdown signal.

**Why it matters to a consumer:** failed prompts that vanish from tracking
and metrics, visible only as a client-side stream that stops without a
terminal event.

**Fix:** remove only if still ours:

```csharp
pending.TryRemove(new KeyValuePair<string, Task>(key, completed));
```

i.e. remove the exact task instance, not whatever currently sits under the
key.

### F-09 (info): `CreateSessionRequest.OwnerId` is unbounded and echoed verbatim

**Severity:** info — `services/OmpOS.Api/Endpoints/SessionEndpoints.cs` (`CreateSessionAsync`)

**What is wrong:** `OwnerId` has no length cap (bounded only by the 1 MiB
body limit) and is stored on the session and returned by `GET /sessions` /
`GET /sessions/{id}`. Combined with the no-op auth boundary this is a
spoofable, arbitrarily long owner label persisted per session.

**Why it matters to a consumer:** mostly hygiene today (single-devPhase 2),
but the first real `IUserContext` must stop trusting this field, or owner
checks will be bypassable by construction.

**Fix:** cap `OwnerId` (e.g. 256 chars, 400 otherwise) now, and record that
the real auth provider owns this field — the middleware stays the single
decision point, as already documented.

## Explicitly verified clean (no finding)

- **Body-size enforcement:** `ReadLimitedAsync` checks `Content-Length`
  up front *and* aborts the 8 KiB streaming copy the moment `total > limit`,
  so the cap holds before the body is buffered — for chunked bodies and lying
  lengths alike. Both prompt and create paths use it.
- **Content-type enforcement:** prompt submission 415s on missing/wrong
  `Content-Type` (JSON and `+json` suffixes accepted, parameters tolerated);
  create skips the check only for a truly empty body. Matrix covered by
  `SessionEndpointTests` (415/413/400/404/409/429).
- **CORS default-deny:** empty `Api:Cors:Origins` builds a policy with no
  origins (the `WithMethods` arm), so no cross-origin request is allowed;
  configured origins get exact-match + no credentials. Covered by
  `Cors_DisallowedOriginGetsNoHeaders`.
- **SSE replay/tail under concurrency:** re-query-from-`last` on every wake
  plus a coalescing permit means appends between drain and wait are never
  lost (permit) and never duplicated (watermark); snapshots are per-session
  locked with monotonic Seq. No gap/no-dup confirmed by code; resume tests
  assert contiguity.
- **Last-Event-ID errors:** non-integer header/query values return 400
  `ProblemDetails`; header wins over `since` when both are present.
- **Disconnect cancellation:** the request token flows
  endpoint → `EnumerateAsync` → `SubscribeAsync` → repository query and
  signal wait; disconnect cancels the enumeration. Background prompt work
  correctly uses host shutdown, not the request token.
- **Log redaction:** all prompt logging goes through `PromptRedactor`
  (length-only marker unless `OmpOS:Debug:LogPrompts`); failure logs carry
  only exception *type names*; the 429 path logs method+path only.
  `Redaction_PromptNeverLogged` guards this.
- **ProblemDetails leakage:** every error response uses static titles/details
  or typed domain `ErrorCode` details; no `ex.Message`, stack, or inner
  exception reaches the wire. No global exception handler exists to do
  otherwise (minimal-API default 500 in production).
- **Suppressions:** each `SuppressMessage`/`pragma` was read in context and
  hides no live risk — pump/dispose catch-alls convert to typed failures or
  guarded boundaries with the cause preserved; the hook isolation fails
  closed; `CA1515` on the factory interface is a documented test seam;
  `CA1711`/`CA1034` are naming/shape choices with justification.

---

_Reviewed: 2026-10-20_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_
