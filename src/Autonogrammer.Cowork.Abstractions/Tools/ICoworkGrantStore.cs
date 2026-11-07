namespace Autonogrammer.Cowork.Abstractions;

/// <summary>Stores allow-always grants. A grant never satisfies an every-call rule.</summary>
public interface ICoworkGrantStore
{
    /// <summary>Reads whether the grant is stored.</summary>
    /// <param name="key">The grant key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the grant is stored.</returns>
    ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken);

    /// <summary>Stores a grant.</summary>
    /// <param name="key">The grant key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the grant is stored.</returns>
    ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken);

    /// <summary>Removes a grant.</summary>
    /// <param name="key">The grant key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the grant is gone.</returns>
    ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken);
}
