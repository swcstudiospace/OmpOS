using OmpOS.Contracts.WorkspaceSettings;

namespace OmpOS.Api.Services.WorkspaceSettings;

/// <summary>Lists the automations of the host. A host that runs an automation engine registers an implementation; the default has none configured.</summary>
internal interface IWorkspaceAutomationsProvider
{
    /// <summary>Lists the automations. The call only reads: it enables nothing and starts no run.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The automations, or null when no automation engine is configured.</returns>
    Task<IReadOnlyList<WorkspaceAutomationInfo>?> ListAsync(CancellationToken cancellationToken);
}
