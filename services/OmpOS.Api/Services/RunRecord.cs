using OmpOS.Contracts.Runs;

namespace OmpOS.Api.Services;

/// <summary>
/// The API's record of one hosted run: which session turn it is, when it was accepted, whether its harness has started the prompt,
/// and the cancellation that stops it. The status itself is projected from the session store by <see cref="RunProjection"/>, so a
/// record holds no copy of it beyond the settled status cached once the run is seen settled.
/// </summary>
internal sealed class RunRecord : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private int _started;
    private int _cancelRequested;
    private int _settled = -1;

    /// <summary>Initializes a new instance of the <see cref="RunRecord"/> class.</summary>
    /// <param name="id">The run identifier.</param>
    /// <param name="sessionId">The identifier of the session the run executes in.</param>
    /// <param name="turn">The 1-based turn of the session the run executes: the count of user prompts admitted up to and including its own.</param>
    /// <param name="createdAt">The time the run was accepted.</param>
    public RunRecord(string id, string sessionId, int turn, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sessionId);
        Id = id;
        SessionId = sessionId;
        Turn = turn;
        CreatedAt = createdAt;
    }

    /// <summary>Gets the run identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the identifier of the session the run executes in.</summary>
    public string SessionId { get; }

    /// <summary>Gets the 1-based turn of the session the run executes.</summary>
    public int Turn { get; }

    /// <summary>Gets the time the run was accepted.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the token that is cancelled when the run is asked to stop.</summary>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>Gets a value indicating whether the harness has started the run's prompt.</summary>
    public bool Started => Volatile.Read(ref _started) != 0;

    /// <summary>Gets a value indicating whether a client asked the run to stop.</summary>
    public bool CancelRequested => Volatile.Read(ref _cancelRequested) != 0;

    /// <summary>Gets the status the run was last seen to settle with, or null when it was not seen settled.</summary>
    public RunStatus? SettledStatus
    {
        get
        {
            var settled = Volatile.Read(ref _settled);
            return settled < 0 ? null : (RunStatus)settled;
        }
    }

    /// <summary>Records that the harness started the run's prompt.</summary>
    public void MarkStarted() => Volatile.Write(ref _started, 1);

    /// <summary>Records the status the run settled with, so a later turn of the session cannot change it.</summary>
    /// <param name="status">The terminal status.</param>
    public void MarkSettled(RunStatus status) => Volatile.Write(ref _settled, (int)status);

    /// <summary>Asks the run to stop. Repeated calls are harmless.</summary>
    public void RequestCancel()
    {
        Volatile.Write(ref _cancelRequested, 1);
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The host is shutting down and the run is already being stopped.
        }
    }

    /// <inheritdoc />
    public void Dispose() => _cancellation.Dispose();
}
