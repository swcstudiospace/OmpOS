using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>The automations provider of an API with no automation engine configured.</summary>
internal sealed class NoAutomationsProvider : IWorkspaceAutomationsProvider
{
    /// <inheritdoc />
    public Task<IReadOnlyList<WorkspaceAutomationInfo>?> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<WorkspaceAutomationInfo>?>(null);
}
