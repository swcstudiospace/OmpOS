using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Lists the plugins of the workspace the API serves. A host that can resolve them registers an implementation; the default lists none.</summary>
internal interface IWorkspacePluginInventory
{
    /// <summary>Lists the plugins in composition order. A row never holds a path or a credential.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The plugins.</returns>
    Task<IReadOnlyList<WorkspacePluginInfo>> ListAsync(CancellationToken cancellationToken);
}
