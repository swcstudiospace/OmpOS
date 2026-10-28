using OmpOS.Api.Services;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Api.IntegrationTests.WorkspaceSettings;

/// <summary>A plugin check that answers a fixed result.</summary>
internal sealed class StubPluginCheck : IWorkspacePluginCheck
{
    private readonly PluginCheckResult _result;

    /// <summary>Initializes a new instance of the <see cref="StubPluginCheck"/> class.</summary>
    /// <param name="result">The result every check returns.</param>
    public StubPluginCheck(PluginCheckResult result)
    {
        _result = result;
    }

    /// <inheritdoc />
    public Task<PluginCheckResult> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(_result);
}
