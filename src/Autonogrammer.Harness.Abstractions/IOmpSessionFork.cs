namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// An omp session that can be forked: the engine creates a second session that continues from the same history. It is a separate
/// interface because only an engine that advertises the capability implements it; a session that does not is not forkable.
/// </summary>
public interface IOmpSessionFork
{
    /// <summary>Gets a value indicating whether the engine advertised that it can fork a session.</summary>
    bool CanFork { get; }

    /// <summary>Forks the bound session. The fork continues from the whole history; the bound session is unchanged.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The identifier the engine gave the new session.</returns>
    /// <exception cref="NotSupportedException">The engine did not advertise forking.</exception>
    Task<string> ForkAsync(CancellationToken cancellationToken);
}
