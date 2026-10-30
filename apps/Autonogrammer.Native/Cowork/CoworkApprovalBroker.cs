using Autonogrammer.Contracts.Controls;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Tools;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Asks on this machine. The timeout is <see cref="CoworkToolHostOptions.ApprovalTimeout"/>, never a hosted approval setting.
/// A remote decision never completes a <see cref="CoworkApprovalRequest.MustBeLocal"/> request.
/// </summary>
internal sealed class CoworkApprovalBroker : ICoworkApprovalBroker
{
    /// <summary>The sentence the model reads when an ask is not approved.</summary>
    public const string NotApproved = "The tool call was not approved";

    private readonly CoworkToolHostOptions _options;
    private readonly TimeProvider _clock;
    private readonly bool _windowAvailable;
    private readonly Dictionary<string, Pending> _pending = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Creates the broker.</summary>
    /// <param name="options">The host options. Null uses the defaults, including a 120 second timeout.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="windowAvailable">Whether a desktop window can show the card.</param>
    public CoworkApprovalBroker(CoworkToolHostOptions? options = null, TimeProvider? clock = null, bool windowAvailable = true)
    {
        _options = options ?? new CoworkToolHostOptions();
        _clock = clock ?? TimeProvider.System;
        _windowAvailable = windowAvailable;
    }

    /// <summary>Gets the timeout the broker waits. It is the host option.</summary>
    public TimeSpan ApprovalTimeout => _options.ApprovalTimeout;

    /// <summary>Gets the cards raised into the single conversation queue.</summary>
    public IReadOnlyList<CoworkApprovalCard> Cards
    {
        get
        {
            lock (_gate)
            {
                return [.. _pending.Values.Select(static item => item.Card)];
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<CoworkApprovalDecision> RequestAsync(CoworkApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_windowAvailable)
        {
            Remember(request, "Unavailable");
            return new CoworkApprovalDecision(CoworkApprovalOutcome.Unavailable);
        }

        var pending = Remember(request, null);
        using var timeout = new CancellationTokenSource(_options.ApprovalTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var decision = await pending.Completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
            return decision;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            pending.Card = pending.Card with { StatusText = "Timed out" };
            return new CoworkApprovalDecision(CoworkApprovalOutcome.TimedOut);
        }
    }

    /// <summary>Completes a pending ask from this machine.</summary>
    /// <param name="callId">The call identifier.</param>
    /// <param name="outcome">The outcome.</param>
    /// <returns>True when a pending ask was completed.</returns>
    public bool CompleteLocal(string callId, CoworkApprovalOutcome outcome)
    {
        lock (_gate)
        {
            if (!_pending.Remove(callId, out var pending))
            {
                return false;
            }

            pending.Completion.TrySetResult(new CoworkApprovalDecision(outcome));
            return true;
        }
    }

    /// <summary>A decision from another device. A <see cref="CoworkApprovalRequest.MustBeLocal"/> ask is left pending.</summary>
    /// <param name="callId">The call identifier.</param>
    /// <param name="outcome">The remote outcome.</param>
    /// <returns>True when the ask was completed.</returns>
    public bool CompleteFromRemote(string callId, CoworkApprovalOutcome outcome)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(callId, out var pending) || pending.Request.MustBeLocal)
            {
                return false;
            }

            _ = _pending.Remove(callId);
            pending.Completion.TrySetResult(new CoworkApprovalDecision(outcome));
            return true;
        }
    }

    private Pending Remember(CoworkApprovalRequest request, string? status)
    {
        var card = new CoworkApprovalCard(
            new PendingToolApproval
            {
                RequestId = request.CallId,
                ToolName = request.ToolName,
                RequestedAt = _clock.GetUtcNow(),
                GrantableTools = [],
            },
            request.Preview.Target,
            request.Preview.Details,
            status);
        var pending = new Pending(request, card);
        lock (_gate)
        {
            _pending[request.CallId] = pending;
        }

        return pending;
    }

    private sealed class Pending(CoworkApprovalRequest request, CoworkApprovalCard card)
    {
        public CoworkApprovalRequest Request { get; } = request;

        public CoworkApprovalCard Card { get; set; } = card;

        public TaskCompletionSource<CoworkApprovalDecision> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

/// <summary>A card in the single conversation queue. Target and details are host-observed.</summary>
/// <param name="Approval">The contract card.</param>
/// <param name="Target">The host-observed target.</param>
/// <param name="Details">The host-observed details.</param>
/// <param name="StatusText">Timed out, Unavailable, or null while waiting.</param>
internal sealed record CoworkApprovalCard(PendingToolApproval Approval, string? Target, IReadOnlyList<string> Details, string? StatusText);
