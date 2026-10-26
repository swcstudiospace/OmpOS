namespace OmpOS.Coordination;

/// <summary>
/// An acquired run lease. Renewing it keeps the owner's claim alive. Disposing it releases the claim, but only while the
/// owner still holds it.
/// </summary>
public interface ISessionRunLease : IAsyncDisposable
{
    /// <summary>Gets the session the lease covers.</summary>
    string SessionId { get; }

    /// <summary>Gets the owner identity that acquired the lease.</summary>
    string OwnerId { get; }

    /// <summary>Extends the lease to a lifetime measured from now.</summary>
    /// <param name="timeToLive">The new lifetime, measured from now. Must be at least one millisecond.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the lease was renewed; <see langword="false"/> when it expired or another owner took it.</returns>
    Task<bool> RenewAsync(TimeSpan timeToLive, CancellationToken cancellationToken);
}
