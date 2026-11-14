using System.Net;
using Autonogrammer.Accounts;
using Autonogrammer.Contracts.Cowork.Projects;
using Autonogrammer.Cowork.Projects;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// The project API of one head. Each call reads the active account's bearer when it is made and builds the transport call on that bearer, so a call made
/// after sign-out or an account switch uses the account active at that moment. Without a client, or without a token, the call is refused with the matching
/// degraded state and no request is sent.
/// </summary>
internal sealed class AccountProjectApi : IProjectApi
{
    private readonly HttpClient? _http;
    private readonly IAccountSession _session;

    /// <summary>Initializes a new instance of the <see cref="AccountProjectApi"/> class.</summary>
    /// <param name="http">The API client, or null when the head has no API address. The client is not disposed here.</param>
    /// <param name="session">The head's account session.</param>
    public AccountProjectApi(HttpClient? http, IAccountSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _http = http;
        _session = session;
    }

    /// <inheritdoc />
    public Task<ProjectApiResult<IReadOnlyList<ProjectSyncRecord>>> ListAsync(CancellationToken cancellationToken) =>
        CallAsync<IReadOnlyList<ProjectSyncRecord>>(static (api, token) => api.ListAsync(token), cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<ProjectSyncRecord>> UpsertAsync(ProjectSyncRecord project, CancellationToken cancellationToken) =>
        CallAsync<ProjectSyncRecord>((api, token) => api.UpsertAsync(project, token), cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> ArchiveAsync(string id, CancellationToken cancellationToken) =>
        CallAsync<bool>((api, token) => api.ArchiveAsync(id, token), cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken) =>
        CallAsync<bool>((api, token) => api.DeleteAsync(id, token), cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<string>> GetGlobalInstructionsAsync(CancellationToken cancellationToken) =>
        CallAsync<string>(static (api, token) => api.GetGlobalInstructionsAsync(token), cancellationToken);

    /// <inheritdoc />
    public Task<ProjectApiResult<bool>> PutGlobalInstructionsAsync(string instructions, CancellationToken cancellationToken) =>
        CallAsync<bool>((api, token) => api.PutGlobalInstructionsAsync(instructions, token), cancellationToken);

    private async Task<ProjectApiResult<T>> CallAsync<T>(Func<HttpProjectApi, CancellationToken, Task<ProjectApiResult<T>>> call, CancellationToken cancellationToken)
    {
        if (_http is null)
        {
            return Refused<T>(ProjectSyncState.Offline);
        }

        var (token, state) = await AccountBearer.ResolveAsync(_session, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return Refused<T>(state);
        }

        return await call(new HttpProjectApi(_http, () => token), cancellationToken).ConfigureAwait(false);
    }

    private static ProjectApiResult<T> Refused<T>(string state)
    {
        var offline = string.Equals(state, ProjectSyncState.Offline, StringComparison.Ordinal);
        return new ProjectApiResult<T>(false, offline ? null : (int)HttpStatusCode.Unauthorized, offline, default, state, state);
    }
}
