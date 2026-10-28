using OmpOS.Api.Services.WorkspaceSettings;
using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>A plugin inventory that lists fixed rows.</summary>
internal sealed class StubPluginInventory : IWorkspacePluginInventory
{
    private readonly IReadOnlyList<WorkspacePluginInfo> _plugins;

    /// <summary>Initializes a new instance of the <see cref="StubPluginInventory"/> class.</summary>
    /// <param name="plugins">The rows every listing returns.</param>
    public StubPluginInventory(IReadOnlyList<WorkspacePluginInfo> plugins)
    {
        _plugins = plugins;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkspacePluginInfo>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(_plugins);
}
