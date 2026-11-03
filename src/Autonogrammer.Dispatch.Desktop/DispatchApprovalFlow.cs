using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// Opens the local approval at the start of every dispatched Computer Use task. A timeout denies. An approval grants the task for 30
/// minutes; revocation withdraws it. The per-action gate of phase 73 still runs for every action.
/// </summary>
public sealed class DispatchApprovalFlow
{
    /// <summary>How long a grant lasts.</summary>
    public static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(30);

    /// <summary>How long the dialog waits before it denies. Matches the Computer Use dialog.</summary>
    public static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(120);

    private readonly IDispatchApprovalPrompt _prompt;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _promptTimeout;
    private readonly Lock _gate = new();
    private DispatchGrant? _grant;

    /// <summary>Creates the flow.</summary>
    /// <param name="prompt">The local dialog.</param>
    /// <param name="clock">The clock for the grant lifetime and the prompt timeout.</param>
    /// <param name="promptTimeout">How long the dialog may wait; null for <see cref="PromptTimeout"/>.</param>
    public DispatchApprovalFlow(IDispatchApprovalPrompt prompt, TimeProvider clock, TimeSpan? promptTimeout = null)
    {
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _promptTimeout = promptTimeout ?? PromptTimeout;
    }

    /// <summary>Gets the grant of the most recent approved task, or null when none is held.</summary>
    public DispatchGrant? Grant
    {
        get
        {
            lock (_gate)
            {
                return _grant;
            }
        }
    }

    /// <summary>Asks the local user to approve one Computer Use task.</summary>
    /// <param name="request">The facts to show.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The outcome.</returns>
    public async ValueTask<DispatchApprovalResult> ApproveTaskAsync(DispatchApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DispatchApprovalChoice choice;
        try
        {
            choice = await _prompt.RequestAsync(request, cancellationToken).AsTask()
                .WaitAsync(_promptTimeout, _clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return new DispatchApprovalResult(false, DispatchFailureReason.ApprovalTimeout, false);
        }

        return choice switch
        {
            DispatchApprovalChoice.ApproveOnce => Approve(),
            DispatchApprovalChoice.DenyAndPause => new DispatchApprovalResult(false, DispatchFailureReason.ApprovalDenied, true),
            _ => new DispatchApprovalResult(false, DispatchFailureReason.ApprovalDenied, false),
        };
    }

    /// <summary>Drops the held grant. Called on revocation.</summary>
    public void Withdraw()
    {
        lock (_gate)
        {
            _grant = null;
        }
    }

    private DispatchApprovalResult Approve()
    {
        lock (_gate)
        {
            _grant = new DispatchGrant(_clock.GetUtcNow() + GrantLifetime);
        }

        return new DispatchApprovalResult(true, null, false);
    }
}
