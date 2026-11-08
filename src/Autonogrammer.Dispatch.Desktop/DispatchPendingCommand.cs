using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>One admitted command retained while the desktop reconnects.</summary>
internal sealed class DispatchPendingCommand(DispatchCommand command)
{
    private readonly TaskCompletionSource<(long Generation, bool Sent)> _receipt = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DispatchCommand Command { get; } = command;

    public List<DispatchCommandState> States { get; } = [DispatchCommandState.Received];

    public long SentGeneration { get; set; }

    public int SentCount { get; set; }

    public void FinishReceipt(long generation, bool sent) => _receipt.TrySetResult((generation, sent));

    public Task<(long Generation, bool Sent)> WaitForReceiptAsync(CancellationToken cancellationToken) => _receipt.Task.WaitAsync(cancellationToken);
}
