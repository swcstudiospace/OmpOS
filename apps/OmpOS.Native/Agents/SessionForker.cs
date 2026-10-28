using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Agents;

/// <summary>
/// Forks a session over the Agent Client Protocol <c>unstable_forkSession</c> request: the engine creates a second session that continues from the same history, and the head
/// adds a session of its own to the registry that resumes it. The ACP request forks the whole conversation, so a fork from a message is not offered.
/// </summary>
internal sealed class SessionForker : ISessionForker
{
    private readonly SessionEngineSet _owner;

    /// <summary>Creates the forker over the head's engine set.</summary>
    /// <param name="owner">The engine set that owns the sessions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public SessionForker(SessionEngineSet owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc/>
    public bool CanForkFromMessage => false;

    /// <inheritdoc/>
    public async Task<ForkResult> ForkAsync(ForkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FromMessageIndex is not null)
        {
            throw new NotSupportedException("omp forks the whole conversation. Fork the conversation, then continue from there.");
        }

        var slot = _owner.SlotOf(request.SessionId) ?? throw new InvalidOperationException("The conversation is not open, so it cannot be forked.");
        var nativeSessionId = await slot.ForkAsync(cancellationToken).ConfigureAwait(false);
        return await _owner.AddForkedSessionAsync(request.SessionId, nativeSessionId, cancellationToken).ConfigureAwait(false);
    }
}
