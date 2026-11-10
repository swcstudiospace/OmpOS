using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>One admitted command retained while the desktop reconnects.</summary>
internal sealed class DispatchPendingCommand(DispatchCommand command) : IDisposable
{
    private readonly TaskCompletionSource<(long Generation, bool Sent)> _receipt = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private bool _disposed;

    public DispatchCommand Command { get; } = command;

    public CancellationToken Cancellation => _cancellation.Token;

    public List<DispatchCommandState> States { get; } = [DispatchCommandState.Received];

    public long SentGeneration { get; set; }

    public int SentCount { get; set; }

    public void FinishReceipt(long generation, bool sent) => _receipt.TrySetResult((generation, sent));

    public Task<(long Generation, bool Sent)> WaitForReceiptAsync(CancellationToken cancellationToken) => _receipt.Task.WaitAsync(cancellationToken);

    public void Cancel()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _cancellation.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                _cancellation.Dispose();
            }
        }
    }
}
