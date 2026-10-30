using Autonogrammer.Contracts.WorkspaceSettings;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The read-only Runtime values of the API (run lease, prompt caps, rate limits, harness adapter). The Presentation Runtime page edits two desktop
/// values (idle stop and retention) and has no source, so this source hands the contract values to a web page of its own.
/// </summary>
internal sealed class WorkspaceRuntimeSource
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceRuntimeSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceRuntimeSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>Reads the values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The values.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read; the message says why.</exception>
    internal async Task<RuntimeSettingsResponse> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _client.GetRuntimeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new SettingsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }
    }
}
