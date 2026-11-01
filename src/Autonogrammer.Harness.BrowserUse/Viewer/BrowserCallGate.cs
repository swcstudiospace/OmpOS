using Autonogrammer.Contracts.Cowork.Browser;
using Autonogrammer.Harness.BrowserUse.Tools;

namespace Autonogrammer.Harness.BrowserUse.Viewer;

/// <summary>
/// The gate every provider call waits on. Take-over makes agent calls wait. Stop cancels the in-flight token and revokes control.
/// </summary>
public sealed class BrowserCallGate : IBrowserCallGate, IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private TaskCompletionSource<bool> _release = Completed();
    private bool _userHasControl;
    private bool _revoked;

    /// <inheritdoc />
    public CancellationToken Cancellation => _stop.Token;

    /// <inheritdoc />
    public bool UserHasControl
    {
        get
        {
            lock (_gate)
            {
                return _userHasControl;
            }
        }
    }

    /// <summary>Gets who may act. Stop sets <see cref="BrowserControlState.Revoked"/> and leaves the browser open.</summary>
    public BrowserControlState Control
    {
        get
        {
            lock (_gate)
            {
                if (_revoked)
                {
                    return BrowserControlState.Revoked;
                }

                return _userHasControl
                    ? BrowserControlState.User
                    : BrowserControlState.Agent;
            }
        }
    }

    /// <inheritdoc />
    public Task WaitIfTakenOverAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _stop.Token.ThrowIfCancellationRequested();
            if (!_userHasControl)
            {
                return Task.CompletedTask;
            }
        }

        return WaitAsync(cancellationToken);
    }

    /// <summary>Marks the user as holding control so agent calls wait.</summary>
    public void EnterUserControl()
    {
        lock (_gate)
        {
            if (_revoked)
            {
                return;
            }

            _userHasControl = true;
            if (_release.Task.IsCompleted)
            {
                _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    /// <summary>Lets waiting agent calls continue, unless Stop already revoked control.</summary>
    public void LeaveUserControl()
    {
        TaskCompletionSource<bool> release;
        lock (_gate)
        {
            if (_revoked)
            {
                return;
            }

            _userHasControl = false;
            release = _release;
        }

        release.TrySetResult(true);
    }

    /// <summary>Cancels in-flight call tokens and revokes control. Does not close the browser.</summary>
    public void Revoke()
    {
        TaskCompletionSource<bool> release;
        lock (_gate)
        {
            _revoked = true;
            _userHasControl = false;
            release = _release;
        }

        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
        }

        release.TrySetResult(true);
    }

    /// <inheritdoc />
    public void Dispose() => _stop.Dispose();

    private async Task WaitAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        while (UserHasControl)
        {
            Task wait;
            lock (_gate)
            {
                wait = _release.Task;
            }

            await wait.WaitAsync(linked.Token).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource<bool> Completed()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.TrySetResult(true);
        return source;
    }
}
