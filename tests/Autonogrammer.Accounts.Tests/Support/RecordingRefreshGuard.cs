namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>A guard that journals when it is acquired and released, and runs a callback while holding, so a test can prove the order of events.</summary>
internal sealed class RecordingRefreshGuard(Journal journal, Func<ValueTask>? whileHeld = null) : IRefreshGuard
{
    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        journal.Add("acquire");
        if (whileHeld is not null)
        {
            await whileHeld().ConfigureAwait(false);
        }

        return new Handle(journal);
    }

    private sealed class Handle(Journal journal) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            journal.Add("release");
            return ValueTask.CompletedTask;
        }
    }
}
