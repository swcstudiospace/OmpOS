namespace Autonogrammer.Harness.Abstractions;

/// <summary>
/// Supplies the decision for a pending permission request. A harness that pauses for a decision awaits this
/// interface and treats a cancelled or failed wait as <see cref="PermissionDecision.Deny"/>. An implementation
/// must never grant a request its own policy ceiling forbids.
/// </summary>
public interface IPermissionResolver
{
    /// <summary>Waits until the decision for one permission request is known.</summary>
    /// <param name="request">The permission request. Its <see cref="PermissionRequestEvent.RequestId"/> addresses the decision.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The decision.</returns>
    Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken);
}
