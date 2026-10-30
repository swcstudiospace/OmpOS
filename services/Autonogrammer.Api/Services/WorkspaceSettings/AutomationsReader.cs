using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Reads the automations the host configured, or says that none is configured.</summary>
internal sealed class AutomationsReader
{
    private readonly IWorkspaceAutomationsProvider _provider;

    /// <summary>Initializes a new instance of the <see cref="AutomationsReader"/> class.</summary>
    /// <param name="provider">The host's automations provider.</param>
    public AutomationsReader(IWorkspaceAutomationsProvider provider)
    {
        _provider = provider;
    }

    /// <summary>Lists the automations.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The automations, or an unavailable answer when no automation engine is configured.</returns>
    public async Task<AutomationsResponse> ReadAsync(CancellationToken cancellationToken)
    {
        var automations = await _provider.ListAsync(cancellationToken).ConfigureAwait(false);
        return automations is null
            ? new AutomationsResponse
            {
                Available = false,
                Reason = "No automation engine is configured on this API. Automations run in the desktop app.",
                Automations = [],
            }
            : new AutomationsResponse { Available = true, Automations = automations };
    }
}
