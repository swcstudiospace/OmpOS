using OmpOS.Contracts.Catalog;

namespace OmpOS.Api.Services;

/// <summary>Checks the plugins of the workspace the API serves. The result is plain text and never holds a path or a credential.</summary>
internal interface IWorkspacePluginCheck
{
    /// <summary>Runs the check.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The result.</returns>
    Task<PluginCheckResult> CheckAsync(CancellationToken cancellationToken);
}
