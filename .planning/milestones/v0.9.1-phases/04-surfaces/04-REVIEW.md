---
phase: 04-surfaces
reviewed: 2026-10-20T00:00:00Z
depth: deep
files_reviewed: 30
files_reviewed_list:
  - src/OmpOS.GenUi/UiTree.cs
  - src/OmpOS.GenUi/UiFolder.cs
  - src/OmpOS.GenUi/A2uiSurfaceCatalog.cs
  - src/OmpOS.GenUi/SurfaceComponent.cs
  - src/OmpOS.UI/Components/Blocks/UiBlockRenderer.razor
  - src/OmpOS.UI/Components/Blocks/MarkdownBlockView.razor
  - src/OmpOS.UI/Components/Blocks/CodeBlockView.razor
  - src/OmpOS.UI/Components/Blocks/ToolCallBlockView.razor
  - src/OmpOS.UI/Components/Blocks/PermissionBlockView.razor
  - src/OmpOS.UI/Components/Blocks/PlanBlockView.razor
  - src/OmpOS.UI/Components/Blocks/DiffPlaceholderView.razor
  - src/OmpOS.UI/Components/Blocks/ErrorBlockView.razor
  - src/OmpOS.UI/Components/Blocks/A2uiSurfaceView.razor
  - src/OmpOS.UI/Components/Blocks/UnknownBlockView.razor
  - src/OmpOS.UI/Components/SessionView.razor
  - src/OmpOS.UI/Components/ConversationList.razor
  - src/OmpOS.UI/Components/Composer.razor
  - src/OmpOS.UI/State/SessionViewModel.cs
  - src/OmpOS.UI/SessionViewState.cs
  - src/OmpOS.Contracts/Streaming/StreamEventPayloadConverter.cs
  - apps/OmpOS.Web/Services/SseEventStream.cs
  - apps/OmpOS.Web/Services/SseParser.cs
  - apps/OmpOS.Web/Services/SseFrame.cs
  - apps/OmpOS.Web/Services/SseStreamOptions.cs
  - apps/OmpOS.Web/Services/ApiSessionClient.cs
  - apps/OmpOS.Web/Services/ApiBaseUrlResolver.cs
  - apps/OmpOS.Web/Pages/Index.razor
  - apps/OmpOS.Web/Program.cs
  - apps/OmpOS.App/Components/SessionHost.razor
  - apps/OmpOS.App/Components/EngineEventMapper.cs
  - apps/OmpOS.App/App.xaml.cs
  - apps/OmpOS.App/MauiProgram.cs
  - src/OmpOS.EngineHost/InProcessEngineHost.cs
  - src/OmpOS.EngineHost/RemoteEngineHost.cs
  - src/OmpOS.EngineHost/IEngineHost.cs
  - src/OmpOS.EngineHost/EngineHostDescriptor.cs
  - vercel.json
  - apps/OmpOS.Web/wwwroot/appsettings.json
findings:
  critical: 1
  warning: 8
  info: 3
  total: 12
status: findings
---

# Phase 04-surfaces: Code Review Report

**Reviewed:** 2026-10-20T00:00:00Z
**Depth:** deep
**Files Reviewed:** 30 (+ plans, contracts converter, tests as context)
**Status:** findings

## Summary

Reviewed the full Phase 4 surface scope (GenUi fold, UI RCL blocks, Web SSE client,
EngineHost twins, both heads, `vercel.json`) with cross-file tracing. Injection posture
is genuinely good: zero `MarkupString`/`Markdig`/raw-HTML sinks exist in scope, all
agent-supplied text renders through Razor-encoded `@` expressions (including
`data-surface`, `src`, `alt`, and `UnknownBlock` raw JSON), and the `UiFolder` fold
provably degrades malformed shapes to `UnknownBlock` instead of throwing
(`StreamEventPayloadConverter` never returns null for any JSON root). EngineHost
placement is clean: the only `Process.Start` in scope is test scaffolding, mobile
binds `RemoteEngineHost` only, and reconnect paths are single-pass/timeout-bounded.

One defect is release-blocking: the Web SSE client buffers the entire connection
before yielding a single event, so live streaming never happens. Eight warnings
(dead permission affordance, unbounded reconnect default with silent failure,
event-field-only terminal detection, unbounded dedup sets, unvalidated
`?apiBaseUrl=` override, unrestricted A2UI image schemes, a throwing mapper
default arm, and a CSP that blocks the configured API origins) should be fixed
before ship. Three info items record accepted trade-offs to confirm.

## Critical Issues

### CR-01: SseEventStream buffers the whole connection — live streaming never happens

**File:** `apps/OmpOS.Web/Services/SseEventStream.cs:58-90, 106-155`
**Issue:** `StreamSessionAsync` awaits `StreamOneConnectionAsync`, which awaits
`ReadFramesAsync`, which reads until EOF/terminal while accumulating into
`List<StreamEvent> Events` — only then does the outer `await foreach` yield. During
a live stream the server holds the connection open, so the UI receives zero events
until the turn ends; per-event `StateHasChanged` in `Index.razor` never fires
incrementally. This defeats WEB-01 "browser-visible streaming". Tests pass only
because fixtures are short, already-complete bodies. Contrast
`RemoteEngineHost.StreamAsync` (`src/OmpOS.EngineHost/RemoteEngineHost.cs`), which
correctly yields per frame.
**Why it matters:** The flagship UX (tokens appearing as they stream) is silently
absent in the Web head; long turns look hung, and the stop control has nothing to
stop until the end.
**Fix:**
```csharp
// Make ReadFramesAsync / StreamOneConnectionAsync stream incrementally:
private async IAsyncEnumerable<StreamEvent> StreamFramesAsync(
    HttpResponseMessage response, HashSet<long> seen,
    [EnumeratorCancellation] CancellationToken ct)
{
    // parse chunk -> frame -> TryConvert -> yield return evt immediately;
    // yield break on IsTerminal(frame)
}
// StreamSessionAsync then: await foreach (var evt in StreamFramesAsync(...)) yield return evt;
// keep reconnect/Last-Event-ID/backoff logic around the per-connection enumeration.
```

## Warnings

### WR-01: Permission approval is a dead affordance in both heads

**File:** `apps/OmpOS.Web/Pages/Index.razor:28-38`, `apps/OmpOS.App/Components/SessionHost.razor:18-25`
**Issue:** Neither head passes `OnOptionSelected` to `SessionView`, so permission
buttons invoke an empty `EventCallback` — clicking does nothing. The model still
transitions to `AwaitingApproval`, leaving the user stuck with no forward path.
Additionally `UiFolder.ReadOptions` (`src/OmpOS.GenUi/UiFolder.cs`) returns `[]`
when `payloadJson` lacks an `options` array, rendering a prompt with zero buttons.
**Why it matters:** Priority (4) requires options to render verbatim *and* the card
to act only on explicit input — currently it cannot act at all, so any turn
requiring approval stalls permanently. (If approve-transport is Phase 5 scope,
wire at minimum a disabled state + explanatory text so the stall is visible.)
**Fix:**
```razor
<SessionView ... OnOptionSelected="ApproveAsync" ... />
```
```csharp
private async Task ApproveAsync(string option)
{
    // POST the verbatim option text to the approval endpoint, then resume the stream.
    await Api.ApproveAsync(_selectedId!, option);
    await StreamFromAsync(sinceSeq: _viewModel.Tree.Blocks.Count);
}
```
and in `UiFolder`, fall back to a single `["Dismiss"]`-style non-destructive option
or an explicit "no options provided" notice when `options` is absent.

### WR-02: Default reconnect policy retries forever and swallows the failure signal

**File:** `apps/OmpOS.Web/Services/SseEventStream.cs:64-90`, `apps/OmpOS.Web/Services/SseStreamOptions.cs:17`, `apps/OmpOS.Web/Program.cs`
**Issue:** `MaxReconnects` defaults to `null` (unbounded) and `Program.cs` registers
both clients with defaults. `StreamOneConnectionAsync` converts `HttpRequestException`
into `SseConnection.Disconnected` (no throw), so a persistently dead origin loops
`Task.Delay(BackoffFor(...))` forever while `StreamFromAsync` never sees the
exception that would call `MarkOffline()` / set `_error`. The user gets an infinite
silent spinner with no offline state.
**Why it matters:** Priority (3) demands bounded reconnect; the production
registration is unbounded *and* invisible.
**Fix:**
```csharp
// SseStreamOptions: default to a finite bound, e.g.
public int? MaxReconnects { get; init; } = 10;
// SseEventStream.StreamSessionAsync: when the bound is reached, throw so callers surface it:
if (_options.MaxReconnects.HasValue && reconnects >= _options.MaxReconnects.Value)
{
    throw new ApiClientException(...); // or a dedicated SseReconnectExceededException
}
```

### WR-03: Terminal detection relies solely on the `event:` field

**File:** `apps/OmpOS.Web/Services/SseEventStream.cs:157-158`
**Issue:** `IsTerminal` checks only `frame.EventType ∈ {"completed","error"}`. A
terminal payload arriving without an `event:` line (or with an unexpected event
name) is converted and yielded but never stops the stream: the client waits for
EOF, then reconnects and re-fetches (dedup drops the replays), looping until the
reconnect bound — unbounded by default per WR-02.
**Why it matters:** One non-conforming server frame turns a finished turn into an
infinite reconnect loop.
**Fix:**
```csharp
private static bool IsTerminal(SseFrame frame, StreamEvent? evt) =>
    frame.EventType is not null && TerminalEventTypes.Contains(frame.EventType)
    || evt?.Payload is CompletedPayload or ErrorPayload;
```
(evaluate after `TryConvert`, passing the parsed event in).

### WR-04: Seq dedup sets grow without bound

**File:** `src/OmpOS.EngineHost/RemoteEngineHost.cs:27,315-355`, `apps/OmpOS.Web/Services/SseEventStream.cs:62`
**Issue:** `_seenSeqs` lives for the whole `RemoteEngineHost` lifetime across all
prompts and is never pruned; the web client's per-call `seen` set likewise grows
for the whole stream. A long-lived mobile/desktop session accumulates one entry
per event forever.
**Why it matters:** Priority (3) explicitly forbids unbounded buffers; this is a
slow memory leak on exactly the long-session surface the product optimizes for.
**Fix:** Track a high-water mark plus a bounded recent window, e.g. keep
`_lastSeq` (already maintained) and only retain seqs within the last N (say 4096)
for overlap dedup:
```csharp
private const int MaxRetainedSeqs = 4096;
// after Add: if (_seenSeqs.Count > MaxRetainedSeqs) drop seqs <= _lastSeq - MaxRetainedSeqs
```
History replay (`since`/`Last-Event-ID`) already bounds resume; the window only
needs to cover replay+live overlap.

### WR-05: `?apiBaseUrl=` override is unvalidated — startup crash and exfil vector

**File:** `apps/OmpOS.Web/Services/ApiBaseUrlResolver.cs:25-45`, `apps/OmpOS.Web/Program.cs`
**Issue:** `Resolve` returns any non-empty string. A garbage value (`?apiBaseUrl=foo`)
makes `new Uri(baseUrl, UriKind.Absolute)` throw `UriFormatException` during service
resolution — uncaught by the `HttpRequestException/ApiClientException/InvalidOperationException`
filters, so the app dies at first load. An `http:` URL is accepted (cleartext,
mixed-content), and any attacker-crafted link (`?apiBaseUrl=https://evil.example`)
redirects session creation and prompt POSTs — i.e. user message content — to an
arbitrary origin with no user confirmation.
**Why it matters:** Malicious or mistyped link = crash or silent prompt exfiltration.
**Fix:**
```csharp
if (!Uri.TryCreate(resolved, UriKind.Absolute, out var uri)
    || (uri.Scheme != Uri.UriSchemeHttps && !IsLoopback(uri)))
{
    throw new InvalidOperationException($"API base URL '{resolved}' must be an absolute https: (or loopback http:) URL.");
}
```
and consider confirming a non-configured origin in the UI before first use.

### WR-06: A2UI image URLs accept any scheme and render into `<img src>`

**File:** `src/OmpOS.GenUi/A2uiSurfaceCatalog.cs:110-117`, `src/OmpOS.UI/Components/Blocks/A2uiSurfaceView.razor:18`
**Issue:** `ReadUri` uses `Uri.TryCreate(..., UriKind.RelativeOrAbsolute)` with no
scheme check, so `javascript:…`, `data:text/html…`, and `file:///…` payloads parse
as `ImageUrl` and render into `<img src="@component.ImageUrl">`. Current browsers
do not execute script from `<img src>`, so this is hardening rather than live XSS —
but agent-supplied URLs must never reach a URL sink unsanitized (priority 1).
**Why it matters:** Scheme allowlisting is the cheap invariant that keeps a future
sink change (lightbox link, anchor, fetch) from becoming injection.
**Fix:**
```csharp
private static Uri? ReadUri(JsonElement element, string name)
{
    ...
    if (uri.IsAbsoluteUri && uri.Scheme is not ("http" or "https") && !IsImageDataUri(uri))
        return null;
    ...
}
```

### WR-07: `EngineEventMapper` throws on unknown harness events instead of degrading

**File:** `apps/OmpOS.App/Components/EngineEventMapper.cs:56`
**Issue:** The `_ => throw new ArgumentOutOfRangeException(...)` default means the
first new `HarnessEvent` subtype added anywhere upstream crashes the MAUI stream
mid-enumeration instead of rendering the shared unknown fallback — the mobile
equivalent of priority (2), which the `UiFolder` side already honors.
**Why it matters:** Forward-compatibility landmine: an additive engine change
becomes a mobile crash.
**Fix:**
```csharp
_ => Unknown("unknown", ("type", evt.GetType().Name)),
```
so `UiFolder` folds it into an `UnknownBlock` like every other unrecognized shape.

### WR-08: CSP `connect-src`/`img-src` block the product's own supported origins

**File:** `vercel.json:31`, `apps/OmpOS.Web/wwwroot/appsettings.json:3`, `apps/OmpOS.App/MauiProgram.cs:22`
**Issue:** CSP is `connect-src 'self' https://api.ompos.local`, but the shipped
default is `http://127.0.0.1:5187`, the mobile default is `https://localhost:5001`,
and `?apiBaseUrl=` may point anywhere — all blocked by the CSP in any environment
other than the placeholder. `img-src 'self' data:` likewise blocks remote
`https:` A2UI images that `A2uiSurfaceView` is built to render.
**Why it matters:** Secure-but-wrong: the first real deployment either breaks
(API calls/image loads refused) or pressures someone to weaken the whole policy
ad hoc. (The rest of the CSP is correctly strict — `wasm-unsafe-eval` is required
for Blazor WASM, `object-src 'none'`, `frame-ancestors 'none'` + `DENY` all good;
no `routes` key, no secrets — both verified clean.)
**Fix:** Parameterize the CSP per environment at deploy time (Vercel environment /
`vercel.json` env substitution) to list the real API origin, and extend image
sources, e.g. `img-src 'self' data: https:` — no broader.

## Info

### IN-01: No Markdig/MarkupString anywhere — safe by absence, markdown unformatted

**File:** `src/OmpOS.UI/Components/Blocks/MarkdownBlockView.razor:4`
**Issue:** Priority (1) asks to check "Markdig configuration with HTML disabled" —
there is no Markdig dependency and no `MarkupString` in scope, so agent markdown
renders as Razor-encoded plain text. Injection-proof, but bold/lists/code-fences
render as literal characters, not formatted markdown.
**Fix:** Confirm unformatted markdown is the intended UI-01 fidelity; if rich
rendering is wanted later, introduce Markdig with
`new MarkdownPipelineBuilder().DisableHtml().Build()` plus scheme-filtered link
rendering — never raw-HTML passthrough.

### IN-02: Composer sends whitespace-only drafts

**File:** `src/OmpOS.UI/Components/Composer.razor:36-45`
**Issue:** `Send` guards `_draft.Length == 0` only; a whitespace-only draft is
submitted as a prompt.
**Fix:** `if (string.IsNullOrWhiteSpace(_draft)) return;` (and optionally trim).

### IN-03: History replay skips Seq ordering protection against duplicates

**File:** `apps/OmpOS.Web/Pages/Index.razor:SelectSessionAsync`
**Issue:** History events are applied in `OrderBy(Seq)` order with no dedup, while
the live path dedups by Seq — a log containing duplicate Seqs renders duplicate
blocks on load but not on stream.
**Fix:** Track seen Seqs during replay (same `HashSet<long>` pattern) or fold
through a helper shared with the stream path.

---

## Explicitly verified clean (no finding)

- **Markup injection:** every agent-controlled string (`Block.Markdown`, `Code`,
  `ToolName/Status`, `Prompt/Options`, `Steps`, `Path/Reason`, `Message`,
  `SurfaceId`, image `src`/`alt`, `Raw.GetRawText()`, error text, conversation
  titles) renders via encoded `@`; attribute values are Razor-encoded. No
  `MarkupString`, `HtmlString`, `innerHTML`, Markdig, or `dangerouslySetInnerHTML`
  in scope.
- **Renderer fold:** `UiFolder.Fold` + `StreamEventPayloadConverter` degrade every
  tested shape (array/string/number/null/boolean roots, unknown kinds) to
  `UnknownBlock`/no-op; `SessionViewModel.ApplyEvent` preserves partial output and
  maps states correctly. (`Fold` throws only on null args, which callers guard —
  documented contract, not a shape.)
- **vercel.json posture:** no `routes` key, `rewrites`+`headers` only, no secrets
  (env is telemetry/globalization flags), `framework: null`, pinned-SDK install,
  WASM project build, correct `outputDirectory`.
- **EngineHost:** no process spawn outside `InProcessEngineHost.ForAcp` (the
  in-process factory; injectable so tests never spawn); `RemoteEngineHost` is
  HTTP-only; `ReconnectAsync` is single-pass and the foreground-resume caller
  bounds it with a 30s CTS (`App.xaml.cs`); descriptors round-trip honestly.

_Reviewed: 2026-10-20T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: deep_
