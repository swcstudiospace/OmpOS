namespace Autonogrammer.Coordination;

/// <summary>
/// Removes the coordination keys that carry a tenant segment. The composition root binds the account erasure of the
/// relational store to this seam, so neither store project references the other.
/// </summary>
public interface ITenantKeyEraser
{
    /// <summary>Deletes every coordination key of one tenant.</summary>
    /// <param name="tenantHash">The tenant segment from <see cref="ITenantHasher.HashTenant"/>: 32 lowercase hexadecimal characters.</param>
    /// <param name="cancellationToken">Cancels the erasure.</param>
    /// <returns>A task that completes when the keys are deleted.</returns>
    Task DeleteTenantKeysAsync(string tenantHash, CancellationToken cancellationToken);
}
