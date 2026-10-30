using System.Text.Json;
using System.Threading.Channels;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.Plugins.Hooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The hook gate of an ACP session: it turns the traffic <see cref="AcpAgentSession"/> reports into Cursor hook events and runs them through the
/// session's dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// When the omp hooks extension is connected (<see cref="HooksSession.BridgeActive"/>) it already produces the lifecycle, tool, file and message
/// events from inside omp, so the tap fires only what the extension cannot see: <c>beforeSubmitPrompt</c> (the ACP prompt path), <c>stop</c> (the end
/// of an ACP turn, with its follow-up) and the context a <c>sessionStart</c> hook added. Without the extension the tap is the fallback: it fires
/// <c>sessionStart</c> and <c>sessionEnd</c>; <c>preToolUse</c> and <c>beforeShellExecution</c> for the calls omp asks permission for (a deny rejects the
/// request before the approval policy is consulted, and an <c>ask</c> leaves the decision to the ordinary approval flow); <c>beforeReadFile</c> for
/// <c>fs/read_text_file</c>; <c>afterFileEdit</c> for <c>fs/write_text_file</c>; and, as observations that cannot block, <c>postToolUse</c>,
/// <c>postToolUseFailure</c>, <c>afterShellExecution</c>, <c>afterAgentResponse</c> and <c>afterAgentThought</c> from <c>session/update</c>.
/// A call omp never asks permission for (yolo mode, read-tier tools) is invisible to <c>preToolUse</c> in this mode: that is why the extension exists.
/// </para>
/// <para>
/// Observations are queued and run in order on a task of the tap, so the receive loop of the connection never waits for a hook.
/// </para>
/// </remarks>
public sealed class AcpHookTap : IAcpHookGate, IAsyncDisposable
{
    private const int MaxTrackedCalls = 2048;
    private const int MaxReadContentChars = 256 * 1024;
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(2);

    private readonly HooksSession _session;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly Channel<AcpHookSignal> _signals = Channel.CreateUnbounded<AcpHookSignal>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<string, TrackedCall> _calls = new(StringComparer.Ordinal);
    private readonly Task _worker;
    private long _openedAt;
    private int _followUps;
    private int _closed;

    /// <summary>Initializes a new instance of the <see cref="AcpHookTap"/> class.</summary>
    /// <param name="session">The session whose dispatcher runs the hooks.</param>
    /// <param name="clock">The clock behind durations.</param>
    /// <param name="logger">Receives failures of observations. Null discards them.</param>
    internal AcpHookTap(HooksSession session, TimeProvider clock, ILogger<AcpHookTap>? logger)
    {
        _session = session;
        _clock = clock;
        _logger = logger ?? NullLogger<AcpHookTap>.Instance;
        _openedAt = clock.GetTimestamp();
        _worker = Task.Run(WorkAsync);
    }

    /// <inheritdoc/>
    public async ValueTask SessionOpenedAsync(string sessionId, bool resumed, CancellationToken cancellationToken)
    {
        _openedAt = _clock.GetTimestamp();
        if (_session.BridgeActive)
        {
            return;
        }

        var payload = HookPayloads.SessionStart(_session.Conversation.ConversationId, _session.IsBackgroundAgent);
        await _session.DispatchCoreAsync(HookEvent.SessionStart, payload, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<AcpHookVerdict> BeforePromptAsync(string prompt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _session.Conversation.NewGeneration();
        Interlocked.Exchange(ref _followUps, 0);
        var decision = await _session.DispatchCoreAsync(HookEvent.BeforeSubmitPrompt, HookPayloads.BeforeSubmitPrompt(prompt, []), null, cancellationToken).ConfigureAwait(false);
        var denied = decision.IsDenied || decision.Cause != HookDecisionCause.None || decision.ShouldContinue == false;
        return new AcpHookVerdict
        {
            Denied = denied,
            UserMessage = decision.UserMessage,
            AgentMessage = decision.AgentMessage,
            PromptContext = denied ? null : _session.TakePendingContext(),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<AcpHookVerdict> BeforePermissionAsync(AcpPermissionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_session.BridgeActive)
        {
            return AcpHookVerdict.Allow;
        }

        var rawInput = ReadRawInput(request.PayloadJson);
        var cwd = _session.Conversation.ProjectDirectory;
        var preTool = AcpHookPayloads.PreToolUse(AcpHookPayloads.ToolName(request.Kind, request.Name), rawInput, request.CallId, cwd);
        var decisions = new List<HookDecision>(2)
        {
            await _session.DispatchCoreAsync(HookEvent.PreToolUse, preTool, request.CallId, cancellationToken).ConfigureAwait(false),
        };

        if (!IsDenial(decisions[0]) && string.Equals(request.Kind, "execute", StringComparison.Ordinal))
        {
            var command = AcpHookPayloads.Command(rawInput, request.Name);
            decisions.Add(await _session.DispatchCoreAsync(HookEvent.BeforeShellExecution, HookPayloads.BeforeShellExecution(command, cwd, sandbox: false), request.CallId, cancellationToken).ConfigureAwait(false));
        }

        return Verdict(decisions);
    }

    /// <inheritdoc/>
    public async ValueTask<AcpHookVerdict> BeforeReadFileAsync(string fullPath, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(content);
        if (_session.BridgeActive)
        {
            return AcpHookVerdict.Allow;
        }

        var shown = content.Length > MaxReadContentChars ? content[..MaxReadContentChars] : content;
        var decision = await _session.DispatchCoreAsync(HookEvent.BeforeReadFile, HookPayloads.BeforeReadFile(fullPath, shown, []), null, cancellationToken).ConfigureAwait(false);
        return Verdict([decision]);
    }

    /// <inheritdoc/>
    public async ValueTask AfterFileWriteAsync(string fullPath, string? previousContent, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(content);
        if (_session.BridgeActive)
        {
            return;
        }

        var edits = AcpHookPayloads.Edits(previousContent, content);
        await _session.DispatchCoreAsync(HookEvent.AfterFileEdit, HookPayloads.AfterFileEdit(fullPath, edits), null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Observe(AcpHookSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (Volatile.Read(ref _closed) == 0 && !_session.BridgeActive)
        {
            _ = _signals.Writer.TryWrite(signal);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AcpTurnEndDecision> TurnEndedAsync(string stopReason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stopReason);
        if (_session.BridgeActive)
        {
            return AcpTurnEndDecision.End;
        }

        var status = stopReason switch
        {
            "cancelled" => "aborted",
            "error" => "error",
            _ => "completed",
        };
        var decision = await _session.DispatchCoreAsync(HookEvent.Stop, HookPayloads.Stop(status), null, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(status, "completed", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(decision.FollowupMessage))
        {
            return AcpTurnEndDecision.End;
        }

        var count = Interlocked.Increment(ref _followUps);
        var limit = LoopLimitOf(decision);
        var notice = limit is { } cap
            ? $"Hook continued the conversation ({count} of {cap})"
            : $"Hook continued the conversation ({count})";
        _session.RaiseNotice(new HookNotice
        {
            Kind = HookNoticeKind.FollowUp,
            SessionId = _session.SessionId,
            At = _clock.GetUtcNow(),
            EventName = "stop",
            HookId = decision.Records.FirstOrDefault(static r => r.Status == HookRunStatus.Succeeded)?.HookId,
            Message = notice,
            Count = count,
            Limit = limit,
        });
        return new AcpTurnEndDecision { FollowUpPrompt = decision.FollowupMessage, Notice = notice };
    }

    /// <inheritdoc/>
    public async ValueTask SessionClosedAsync(string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reason);
        await DrainAsync(cancellationToken).ConfigureAwait(false);
        if (_session.BridgeActive)
        {
            return;
        }

        var duration = (long)_clock.GetElapsedTime(_openedAt).TotalMilliseconds;
        var finalStatus = string.Equals(reason, "error", StringComparison.Ordinal) ? "error" : "completed";
        var payload = HookPayloads.SessionEnd(_session.Conversation.ConversationId, reason, duration, _session.IsBackgroundAgent, finalStatus);
        await _session.DispatchCoreAsync(HookEvent.SessionEnd, payload, null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        using var limit = new CancellationTokenSource(DrainTimeout, _clock);
        await DrainAsync(limit.Token).ConfigureAwait(false);
    }

    private static bool IsDenial(HookDecision decision) => decision.IsDenied || decision.Cause != HookDecisionCause.None;

    private static AcpHookVerdict Verdict(IReadOnlyList<HookDecision> decisions)
    {
        var denying = decisions.Where(IsDenial).ToArray();
        if (denying.Length == 0)
        {
            return AcpHookVerdict.Allow;
        }

        return new AcpHookVerdict
        {
            Denied = true,
            UserMessage = Join(denying.Select(static d => d.UserMessage)),
            AgentMessage = Join(denying.Select(static d => d.AgentMessage)),
        };
    }

    private static string? Join(IEnumerable<string?> parts)
    {
        var text = string.Join('\n', parts.Where(static p => !string.IsNullOrWhiteSpace(p)));
        return text.Length == 0 ? null : text;
    }

    private int? LoopLimitOf(HookDecision decision)
    {
        // The record names the hook that ran; its loop limit comes from the hook set in force.
        var answered = decision.Records.FirstOrDefault(static r => r.Status == HookRunStatus.Succeeded);
        return answered is null ? null : _session.Hooks.Hooks.FirstOrDefault(h => string.Equals(h.Id, answered.HookId, StringComparison.Ordinal))?.LoopLimit;
    }

    private static JsonElement? ReadRawInput(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("toolCall", out var toolCall)
                && toolCall.ValueKind == JsonValueKind.Object
                && toolCall.TryGetProperty("rawInput", out var input)
                && input.ValueKind == JsonValueKind.Object
                    ? input.Clone()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _closed, 1);
        _ = _signals.Writer.TryComplete();
        try
        {
            await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A hook that outlives the drain budget keeps running on its own; the session must still close.
        }
    }

    private async Task WorkAsync()
    {
        await foreach (var signal in _signals.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                await ProcessAsync(signal).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ObservationFailed(_logger, signal.Kind.ToString(), ex.GetType().Name, null);
            }
        }
    }

    private async Task ProcessAsync(AcpHookSignal signal)
    {
        var cwd = _session.Conversation.ProjectDirectory;
        switch (signal.Kind)
        {
            case AcpHookSignalKind.ToolCall when signal.ToolCallId is { } started:
                if (_calls.Count >= MaxTrackedCalls)
                {
                    _calls.Clear();
                }

                _calls[started] = new TrackedCall(signal.Title, signal.ToolKind, signal.RawInput, _clock.GetTimestamp());
                break;
            case AcpHookSignalKind.ToolCallUpdate when signal.ToolCallId is { } id
                && signal.Status is "completed" or "failed"
                && _calls.Remove(id, out var call):
                await FinishToolCallAsync(signal, id, call, cwd).ConfigureAwait(false);
                break;
            case AcpHookSignalKind.AgentResponse when !string.IsNullOrEmpty(signal.Text):
                await _session.DispatchCoreAsync(HookEvent.AfterAgentResponse, HookPayloads.AfterAgentResponse(signal.Text), null, CancellationToken.None).ConfigureAwait(false);
                break;
            case AcpHookSignalKind.AgentThought when !string.IsNullOrEmpty(signal.Text):
                var duration = signal.Duration is { } elapsed ? (long?)elapsed.TotalMilliseconds : null;
                await _session.DispatchCoreAsync(HookEvent.AfterAgentThought, HookPayloads.AfterAgentThought(signal.Text, duration), null, CancellationToken.None).ConfigureAwait(false);
                break;
            default:
                // A plan has no Cursor event, and an update of a call the tap never saw start has no tool to name.
                break;
        }
    }

    private async Task FinishToolCallAsync(AcpHookSignal signal, string id, TrackedCall call, string cwd)
    {
        var toolName = AcpHookPayloads.ToolName(call.Kind, call.Title);
        var elapsed = _clock.GetElapsedTime(call.StartedAt);
        if (string.Equals(signal.Status, "failed", StringComparison.Ordinal))
        {
            var failure = AcpHookPayloads.PostToolUseFailure(toolName, call.RawInput, signal.Text, id, cwd, elapsed);
            await _session.DispatchCoreAsync(HookEvent.PostToolUseFailure, failure, id, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var success = AcpHookPayloads.PostToolUse(toolName, call.RawInput, signal.Text, id, cwd, elapsed);
        await _session.DispatchCoreAsync(HookEvent.PostToolUse, success, id, CancellationToken.None).ConfigureAwait(false);
        if (string.Equals(call.Kind, "execute", StringComparison.Ordinal))
        {
            var command = AcpHookPayloads.Command(call.RawInput, call.Title);
            var shell = HookPayloads.AfterShellExecution(command, signal.Text ?? string.Empty, (long)elapsed.TotalMilliseconds, sandbox: false);
            await _session.DispatchCoreAsync(HookEvent.AfterShellExecution, shell, id, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static readonly Action<ILogger, string, string, Exception?> ObservationFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(20, "AcpHookObservationFailed"),
            "A hook observation of kind {Kind} failed with {ExceptionType}; the next observation was processed.");

    private readonly record struct TrackedCall(string? Title, string? Kind, JsonElement? RawInput, long StartedAt);
}
