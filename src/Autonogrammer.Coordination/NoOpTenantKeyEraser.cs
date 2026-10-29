namespace Autonogrammer.Coordination;

/// <summary>An <see cref="ITenantKeyEraser"/> for the in-memory store, which holds no tenant-keyed state that outlives the process.</summary>
public sealed class NoOpTenantKeyEraser : ITenantKeyEraser
{
    /// <summary>Does nothing.</summary>
    /// <param name="tenantHash">The tenant segment.</param>
    /// <param name="cancellationToken">Cancels the erasure.</param>
    /// <returns>A completed task.</returns>
    public Task DeleteTenantKeysAsync(string tenantHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
