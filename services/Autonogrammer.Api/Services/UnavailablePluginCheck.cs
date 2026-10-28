using Autonogrammer.Contracts.Catalog;

namespace Autonogrammer.Api.Services;

/// <summary>The plugin check of an API that has no plugin inventory: it reports the check as unavailable rather than inventing a result.</summary>
internal sealed class UnavailablePluginCheck : IWorkspacePluginCheck
{
    /// <inheritdoc />
    public Task<PluginCheckResult> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new PluginCheckResult
        {
            Status = PluginCheckStatus.Unavailable,
            Summary = "This API has no plugin inventory. The plugin check runs in the desktop app.",
        });
}
