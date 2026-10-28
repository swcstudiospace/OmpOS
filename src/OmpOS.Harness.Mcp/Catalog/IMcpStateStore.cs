namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>Persists <see cref="McpState"/>. A head gives the catalog a file store; a test gives it memory.</summary>
public interface IMcpStateStore
{
    /// <summary>Loads the state. A missing or damaged store is an <see cref="McpState.Empty"/> state: consent is never assumed.</summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The persisted state.</returns>
    Task<McpState> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Persists the state.</summary>
    /// <param name="state">The state to keep.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the state is written.</returns>
    /// <exception cref="IOException">The state cannot be written.</exception>
    Task SaveAsync(McpState state, CancellationToken cancellationToken);
}
