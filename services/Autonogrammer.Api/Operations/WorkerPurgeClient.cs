using System.Net;
using System.Net.Http.Headers;
using Autonogrammer.Api.Cloud;

namespace Autonogrammer.Api.Operations;

/// <summary>
/// Sends the purge of one account's workspace to a worker: <c>DELETE /accounts/{ownerDir}</c> with a purge token (<see cref="WorkerTokenIssuer.IssuePurge"/>)
/// as the bearer. Fails closed: only 204 and 404 are outcomes, and every other answer throws <see cref="WorkerPurgeException"/>.
/// </summary>
internal sealed class WorkerPurgeClient
{
    private const int OwnerDirLength = 64;

    private readonly HttpClient _http;
    private readonly WorkerTokenIssuer _issuer;

    /// <summary>Initializes a new instance of the <see cref="WorkerPurgeClient"/> class.</summary>
    /// <param name="http">The HTTP client that sends the purge.</param>
    /// <param name="issuer">Signs the purge token with the control plane's key.</param>
    public WorkerPurgeClient(HttpClient http, WorkerTokenIssuer issuer)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(issuer);
        _http = http;
        _issuer = issuer;
    }

    /// <summary>Purges the account's workspace folder on one worker.</summary>
    /// <param name="workerBaseUri">The worker's HTTP base address, for example <c>http://worker:8080</c>.</param>
    /// <param name="ownerDir">The account's workspace directory name: 64 lowercase hexadecimal characters.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see cref="WorkerPurgeOutcome.Purged"/> when the worker deleted the folder, <see cref="WorkerPurgeOutcome.NotFound"/> when it was absent.</returns>
    /// <exception cref="WorkerPurgeException">The worker answered with any other status, such as 401, 403, 409 or 5xx.</exception>
    /// <exception cref="HttpRequestException">The worker could not be reached.</exception>
    public async Task<WorkerPurgeOutcome> PurgeAsync(Uri workerBaseUri, string ownerDir, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workerBaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerDir);
        if (!IsOwnerDir(ownerDir))
        {
            throw new ArgumentException("The owner directory must be 64 lowercase hexadecimal characters.", nameof(ownerDir));
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, AccountUri(workerBaseUri, ownerDir));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.IssuePurge(ownerDir));
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.NoContent => WorkerPurgeOutcome.Purged,
            HttpStatusCode.NotFound => WorkerPurgeOutcome.NotFound,
            _ => throw new WorkerPurgeException(response.StatusCode),
        };
    }

    private static Uri AccountUri(Uri workerBaseUri, string ownerDir) =>
        new UriBuilder(workerBaseUri) { Path = workerBaseUri.AbsolutePath.TrimEnd('/') + "/accounts/" + ownerDir }.Uri;

    private static bool IsOwnerDir(string value) =>
        value.Length == OwnerDirLength && value.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
