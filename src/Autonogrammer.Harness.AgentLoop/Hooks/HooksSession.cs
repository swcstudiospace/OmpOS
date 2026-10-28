using System.Text.Json;
using Microsoft.Extensions.Logging;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The hooks of one conversation. It owns the dispatcher, the conversation's <see cref="HookSession"/>, the run log, the loopback bridge and the
/// ACP tap, and turns every decision into run records, notices and the context the next prompt carries. Created by <see cref="HooksService"/>.
/// </summary>
internal sealed class HooksSession : IHooksSession
{
    private readonly HooksService? _service;
    private readonly HookDispatcher? _dispatcher;
    private readonly HookRunLog? _log;
    private readonly Func<CancellationToken, Task<EffectiveHookSet>>? _loader;
    private readonly TimeProvider _clock;
    private readonly Lock _pendingGate = new();
    private HookBridgeServer? _server;
    private AcpHookTap? _tap;
    private string? _pendingContext;
    private int _disposed;

    /// <summary>Creates a session with hooks on.</summary>
    /// <param name="service">The service that publishes the records.</param>
    /// <param name="request">The conversation.</param>
    /// <param name="conversation">The conversation's hook session.</param>
    /// <param name="dispatcher">The dispatcher.</param>
    /// <param name="log">The session's run log.</param>
    /// <param name="loader">Reads the hook files again.</param>
    /// <param name="clock">The clock.</param>
    internal HooksSession(
        HooksService service,
        HooksSessionRequest request,
        HookSession conversation,
        HookDispatcher dispatcher,
        HookRunLog log,
        Func<CancellationToken, Task<EffectiveHookSet>> loader,
        TimeProvider clock)
    {
        _service = service;
        SessionId = request.SessionId;
        IsBackgroundAgent = request.IsBackgroundAgent;
        Conversation = conversation;
        _dispatcher = dispatcher;
        _log = log;
        _loader = loader;
        _clock = clock;
        IsActive = true;
    }

    /// <summary>Creates a session with hooks off: no gate, no endpoint, nothing runs.</summary>
    /// <param name="request">The conversation.</param>
    /// <param name="clock">The clock.</param>
    internal HooksSession(HooksSessionRequest request, TimeProvider clock)
    {
        SessionId = request.SessionId;
        IsBackgroundAgent = request.IsBackgroundAgent;
        Conversation = new HookSession(request.ConversationId ?? request.SessionId, request.WorkspaceRoots);
        _clock = clock;
    }

    /// <inheritdoc/>
    public event EventHandler<HookNoticeEventArgs>? NoticeRaised;

    /// <inheritdoc/>
    public string SessionId { get; }

    /// <inheritdoc/>
    public bool IsActive { get; }

    /// <inheritdoc/>
    public IAcpHookGate? Gate => _tap;

    /// <inheritdoc/>
    public HookBridgeEndpoint? Endpoint { get; private set; }

    /// <inheritdoc/>
    public EffectiveHookSet Hooks => _dispatcher?.Hooks ?? EffectiveHookSet.Empty;

    /// <summary>Gets the conversation's hook session: the common input fields, the generation and the follow-up counters.</summary>
    internal HookSession Conversation { get; }

    /// <summary>Gets a value indicating whether the session is a background (cloud) agent.</summary>
    internal bool IsBackgroundAgent { get; }

    /// <summary>
    /// Gets a value indicating whether the omp hooks extension is connected to this session's bridge. The extension then produces the tool, file,
    /// message and lifecycle events itself, and the ACP tap only adds what the extension cannot see: the prompt, the end of a turn and the context.
    /// </summary>
    internal bool BridgeActive { get; private set; }

    /// <summary>Gets the time source of the session.</summary>
    internal TimeProvider Clock => _clock;

    /// <inheritdoc/>
    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        if (_dispatcher is null || _loader is null)
        {
            return;
        }

        _dispatcher.Replace(await _loader(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public Task<HookDecision> DispatchAsync(HookEvent hookEvent, JsonElement payload, CancellationToken cancellationToken) =>
        DispatchCoreAsync(hookEvent, payload, null, cancellationToken);

    /// <inheritdoc/>
    public IReadOnlyList<HookRunRecord> RecentRuns(int max) => _log?.Recent(max) ?? [];

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_tap is not null)
        {
            await _tap.DisposeAsync().ConfigureAwait(false);
        }

        if (_server is not null)
        {
            await _server.DisposeAsync().ConfigureAwait(false);
        }

        _log?.Dispose();
    }

    /// <summary>Attaches the started bridge and the endpoint the planner uses. The extension counts as connected when the planner will load it.</summary>
    /// <param name="server">The started server.</param>
    /// <param name="endpoint">The endpoint.</param>
    internal void AttachBridge(HookBridgeServer server, HookBridgeEndpoint endpoint)
    {
        _server = server;
        Endpoint = endpoint;
        BridgeActive = endpoint.IsLoadable;
    }

    /// <summary>Creates the ACP tap. It is called once, right after construction.</summary>
    /// <param name="logger">The logger of the tap, or null.</param>
    internal void AttachTap(ILogger<AcpHookTap>? logger) => _tap = new AcpHookTap(this, _clock, logger);

    /// <summary>
    /// Dispatches an event with the session's dispatcher, then publishes its records and raises the notices the decision calls for. With hooks off
    /// it returns <see cref="HookDecision.None"/>.
    /// </summary>
    /// <param name="hookEvent">The event.</param>
    /// <param name="payload">The event-specific payload.</param>
    /// <param name="toolCallId">The tool call the event belongs to, or null.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The merged decision.</returns>
    internal async Task<HookDecision> DispatchCoreAsync(HookEvent hookEvent, JsonElement payload, string? toolCallId, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            return HookDecision.None;
        }

        var matched = Hooks.For(hookEvent).Where(hook => hook.MatcherProblem is null && HookMatcher.Matches(hook.Matcher, hook.CompiledMatcher, HookEvents.MatcherValue(hookEvent, payload)));
        IReadOnlyList<string> running = _service?.BeginRun(matched) ?? [];
        HookDecision decision;
        try
        {
            decision = await _dispatcher.DispatchAsync(hookEvent, Conversation, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _service?.EndRun(running);
        }

        AfterDispatch(HookEvents.Format(hookEvent), hookEvent, decision, toolCallId);
        return decision;
    }

    /// <summary>Dispatches by name, as the bridge receives events: a Cursor event name, or an omp event name when <paramref name="isOmpEvent"/> is true.</summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="isOmpEvent">True for an omp extension event.</param>
    /// <param name="payload">The payload object.</param>
    /// <param name="toolCallId">The tool call the event belongs to, or null.</param>
    /// <param name="cancellationToken">Cancels the dispatch.</param>
    /// <returns>The merged decision.</returns>
    internal async Task<HookDecision> DispatchNamedAsync(string eventName, bool isOmpEvent, JsonElement payload, string? toolCallId, CancellationToken cancellationToken)
    {
        if (_dispatcher is null)
        {
            return HookDecision.None;
        }

        HookEvent? parsed = !isOmpEvent && HookEvents.TryParse(eventName, out var hookEvent) ? hookEvent : null;
        IEnumerable<EffectiveHook> candidates = isOmpEvent
            ? Hooks.ForOmp(eventName)
            : parsed is { } known
                ? Hooks.For(known).Where(hook => hook.MatcherProblem is null && HookMatcher.Matches(hook.Matcher, hook.CompiledMatcher, HookEvents.MatcherValue(known, payload)))
                : [];
        IReadOnlyList<string> running = _service?.BeginRun(candidates) ?? [];
        HookDecision decision;
        try
        {
            decision = await _dispatcher.DispatchNamedAsync(eventName, isOmpEvent, Conversation, payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _service?.EndRun(running);
        }

        AfterDispatch(eventName, parsed, decision, toolCallId);
        return decision;
    }

    /// <summary>Returns the context hooks added since the last prompt and forgets it, or null when there is none.</summary>
    /// <returns>The context text.</returns>
    internal string? TakePendingContext()
    {
        lock (_pendingGate)
        {
            var context = _pendingContext;
            _pendingContext = null;
            return context;
        }
    }

    /// <summary>Raises a notice.</summary>
    /// <param name="notice">The notice.</param>
    internal void RaiseNotice(HookNotice notice)
    {
        var handlers = NoticeRaised;
        if (handlers is null)
        {
            return;
        }

        var args = new HookNoticeEventArgs(notice);
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<HookNoticeEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never break the hooks or the other subscribers.
                _ = ex;
            }
        }
    }

    private void AfterDispatch(string eventName, HookEvent? hookEvent, HookDecision decision, string? toolCallId)
    {
        _service?.Publish(decision.Records);
        if (hookEvent == HookEvent.SessionStart && !string.IsNullOrWhiteSpace(decision.AdditionalContext))
        {
            lock (_pendingGate)
            {
                _pendingContext = _pendingContext is null ? decision.AdditionalContext : _pendingContext + "\n" + decision.AdditionalContext;
            }
        }

        var now = _clock.GetUtcNow();
        if (decision.Cause == HookDecisionCause.ConsentMissing)
        {
            var record = decision.Records.FirstOrDefault(static r => r.Status == HookRunStatus.AwaitingConsent);
            RaiseNotice(new HookNotice
            {
                Kind = HookNoticeKind.ConsentRequired,
                SessionId = SessionId,
                At = now,
                EventName = eventName,
                ToolCallId = toolCallId,
                HookId = record?.HookId,
                Message = decision.AgentMessage ?? "A hook that guards this action has no consent. Review it on the Hooks page.",
            });
        }
        else if (decision.Cause != HookDecisionCause.None || decision.ShouldContinue == false)
        {
            var record = decision.Records.FirstOrDefault(static r => r.Status is HookRunStatus.Denied or HookRunStatus.PluginQuarantined or HookRunStatus.InvalidResponse or HookRunStatus.Failed or HookRunStatus.TimedOut);
            RaiseNotice(new HookNotice
            {
                Kind = HookNoticeKind.Denied,
                SessionId = SessionId,
                At = now,
                EventName = eventName,
                ToolCallId = toolCallId,
                HookId = record?.HookId,
                Message = FirstText(decision.UserMessage, decision.AgentMessage) ?? "A hook blocked this action.",
            });
        }

        if (!string.IsNullOrWhiteSpace(decision.AdditionalContext) && decision.Cause == HookDecisionCause.None)
        {
            RaiseNotice(new HookNotice
            {
                Kind = HookNoticeKind.Context,
                SessionId = SessionId,
                At = now,
                EventName = eventName,
                ToolCallId = toolCallId,
                HookId = decision.Records.FirstOrDefault(static r => r.Status == HookRunStatus.Succeeded)?.HookId,
                Message = decision.AdditionalContext,
            });
        }
    }

    private static string? FirstText(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : null;
}
