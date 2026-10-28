using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Web.Services;

/// <summary>What the workbench supplies to the runtime of one session.</summary>
internal sealed class WebSessionRuntimeOptions
{
    /// <summary>Gets the catalogue row of the session.</summary>
    public required AgentRow Row { get; init; }

    /// <summary>Gets the API client.</summary>
    public required IOmpApiClient Api { get; init; }

    /// <summary>Gets the catalogue the session updates after close, reopen and resume.</summary>
    public required WebSessionCatalog Catalog { get; init; }

    /// <summary>Gets the bridge that sends tool approval decisions.</summary>
    public required WebToolApprovalBridge Approvals { get; init; }

    /// <summary>Gets the clock of the view models.</summary>
    public required TimeProvider TimeProvider { get; init; }

    /// <summary>Gets the parts of the head the native slash commands reach, or null.</summary>
    public WebSlashTargets? Targets { get; init; }
}
