using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>The inventory of an API that has none: it lists no plugin rather than inventing one.</summary>
internal sealed class EmptyPluginInventory : IWorkspacePluginInventory
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WorkspacePluginInfo>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkspacePluginInfo>>([]);
}
