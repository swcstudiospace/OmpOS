namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>
/// The way into one running omp session: a directive is queued for the omp plugin, which delivers it into the session and
/// acknowledges it. The channel reports what it can prove: a receipt says the directive is queued, only an acknowledgement says
/// what omp did with it.
/// </summary>
public interface ISteeringChannel
{
    /// <summary>Queues a directive for the session.</summary>
    /// <param name="directive">The directive.</param>
    /// <param name="cancellationToken">Cancels the call before the directive is queued.</param>
    /// <returns>The receipt for the queued directive.</returns>
    /// <exception cref="SteeringUnavailableException">The directive was not queued; <see cref="SteeringUnavailableException.Reason"/> says why and the message is safe to show.</exception>
    Task<SteeringReceipt> SendAsync(SteeringDirective directive, CancellationToken cancellationToken);

    /// <summary>Waits for the acknowledgement of a directive.</summary>
    /// <param name="id">The identifier from the <see cref="SteeringReceipt"/>.</param>
    /// <param name="timeout">How long to wait. Zero looks once.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The acknowledgement, or null when none arrived in time.</returns>
    Task<SteeringAck?> WaitForAckAsync(string id, TimeSpan timeout, CancellationToken cancellationToken);
}
