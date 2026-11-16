namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Keeps the confirmation of each environment. The provisioning service reads it before the first write and never records one itself: the
/// confirmation comes from the user. Persistence is a later unit; <see cref="InMemoryProvisionConfirmationStore"/> is the in-process store.
/// </summary>
public interface IProvisionConfirmationStore
{
    /// <summary>Returns the confirmation recorded for an environment.</summary>
    /// <param name="environmentId">The environment.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The confirmation, or null when none is recorded.</returns>
    Task<ProvisionConfirmation?> FindAsync(string environmentId, CancellationToken cancellationToken);

    /// <summary>Records a confirmation, replacing any earlier one for the same environment.</summary>
    /// <param name="confirmation">The confirmation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task RecordAsync(ProvisionConfirmation confirmation, CancellationToken cancellationToken);
}
