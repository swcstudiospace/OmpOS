using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;
using OmpOS.UI.Components.Settings;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// The <see cref="IWorkspaceSettingsReader"/> of the web head: the runtime, hooks and conventions routes of the API, read through the
/// <see cref="WorkspaceSettingsClient"/>. An expected failure (unreachable, a refusal, an unreadable answer) becomes a
/// <see cref="SettingsSourceException"/> whose message says why, so the page shows it instead of throwing.
/// </summary>
internal sealed class WorkspaceSettingsReader : IWorkspaceSettingsReader
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSettingsReader"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceSettingsReader(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc />
    public Task<RuntimeSettingsResponse> GetRuntimeAsync(CancellationToken cancellationToken) =>
        ReadAsync(_client.GetRuntimeAsync, cancellationToken);

    /// <inheritdoc />
    public Task<HooksResponse> GetHooksAsync(CancellationToken cancellationToken) =>
        ReadAsync(_client.GetHooksAsync, cancellationToken);

    /// <inheritdoc />
    public Task<ConventionsResponse> GetConventionsAsync(CancellationToken cancellationToken) =>
        ReadAsync(_client.GetConventionsAsync, cancellationToken);

    private static async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        try
        {
            return await read(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new SettingsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }
    }
}
