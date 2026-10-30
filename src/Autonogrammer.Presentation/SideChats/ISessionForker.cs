namespace Autonogrammer.Presentation.SideChats;

/// <summary>
/// Forks a session into a new one that continues from the same history. The desktop head implements it over the Agent Client Protocol <c>unstable_forkSession</c> request that omp advertises
/// (<c>sessionCapabilities.fork</c>); omp's own <c>/fork</c> and <c>/branch</c> run in its terminal UI only. The ACP request forks the whole session, so a head that cannot fork from a message says so
/// through <see cref="CanForkFromMessage"/>.
/// </summary>
public interface ISessionForker
{
    /// <summary>Gets whether the engine can fork from a message and drop the ones after it. When it is <see langword="false"/> only the whole conversation can be forked.</summary>
    bool CanForkFromMessage { get; }

    /// <summary>Forks a session.</summary>
    /// <param name="request">The session and how much of it to keep.</param>
    /// <param name="cancellationToken">Cancels the fork.</param>
    /// <returns>The new session. A fork the engine refuses is an exception whose message is shown to the user.</returns>
    Task<ForkResult> ForkAsync(ForkRequest request, CancellationToken cancellationToken);
}
