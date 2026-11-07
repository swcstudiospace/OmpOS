using System.Globalization;
using System.Net;
using System.Text.Json.Serialization.Metadata;

namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>
/// The artifact routes of the web head over <see cref="ApiHttp"/>. The bearer is added by <c>BearerTokenHandler</c>. This client never
/// names the token types and never reads browser storage. It returns the ticketed render URL of an interactive version; the artifact
/// frame loads that URL, and nothing else from the API is ever framed.
/// </summary>
internal sealed class ArtifactWebClient
{
    /// <summary>Content of one version is bounded. The desktop store caps a version at 5 MiB (ArtifactWireContract.MaxContentBytes).</summary>
    internal const long MaxContentBytes = 5L * 1024 * 1024;

    /// <summary>A render ticket lives 60 seconds; a ticket older than 55 seconds is replaced before it is handed out.</summary>
    internal static readonly TimeSpan TicketReuse = TimeSpan.FromSeconds(55);

    private const string RenderPathPrefix = "cowork/artifact-render/";

    private readonly ApiHttp _http;
    private readonly TimeProvider _time;
    private readonly Dictionary<(string Id, int Number), (Uri Url, DateTimeOffset Issued)> _tickets = [];

    /// <summary>Creates the client.</summary>
    /// <param name="http">The shared API client.</param>
    /// <param name="time">The clock that dates the render tickets.</param>
    internal ArtifactWebClient(ApiHttp http, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(time);
        _http = http;
        _time = time;
    }

    /// <summary>Lists the caller's artifact headers.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The headers.</returns>
    internal Task<ArtifactCall<ArtifactListRecord>> ListAsync(CancellationToken cancellationToken) =>
        GetJsonAsync("cowork/artifacts", ArtifactWebJsonContext.Default.ArtifactListRecord, cancellationToken);

    /// <summary>Reads one owned artifact header.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The header.</returns>
    internal Task<ArtifactCall<ArtifactSummaryRecord>> GetAsync(string artifactId, CancellationToken cancellationToken) =>
        GetJsonAsync("cowork/artifacts/" + Uri.EscapeDataString(artifactId), ArtifactWebJsonContext.Default.ArtifactSummaryRecord, cancellationToken);

    /// <summary>
    /// Reads the bytes of one owned version. A version whose content was never stored (metadata-only sync) answers
    /// <see cref="ArtifactCallState.SyncOff"/>, which the UI shows as "sync is off".
    /// </summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The bytes.</returns>
    internal async Task<ArtifactCall<byte[]>> GetContentAsync(string artifactId, int number, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.Client.GetAsync(new Uri(VersionPath(artifactId, number) + "/content", UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Refused<byte[]>(response.StatusCode, response.Headers.RetryAfter?.Delta, syncOffOnNotFound: true);
            }

            var bytes = await BoundedContent.ReadBytesAsync(response.Content, MaxContentBytes, cancellationToken).ConfigureAwait(false);
            return new ArtifactCall<byte[]>(ArtifactCallState.Ok, bytes, null);
        }
        catch (HttpRequestException)
        {
            return new ArtifactCall<byte[]>(ArtifactCallState.Offline, null, null);
        }
        catch (ApiClientException)
        {
            return new ArtifactCall<byte[]>(ArtifactCallState.Failed, null, null);
        }
    }

    /// <summary>
    /// Returns the ticketed render URL of one owned version. A ticket is reused while it is fresh; an expired one is replaced by a
    /// single new ticket in this call. The URL must sit under the render route of the API origin, or the call fails.
    /// </summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The absolute render URL.</returns>
    internal async Task<ArtifactCall<Uri>> RenderUrlAsync(string artifactId, int number, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_tickets.TryGetValue((artifactId, number), out var fresh) && now - fresh.Issued < TicketReuse)
        {
            return new ArtifactCall<Uri>(ArtifactCallState.Ok, fresh.Url, null);
        }

        try
        {
            using var response = await _http.Client.PostAsync(new Uri(VersionPath(artifactId, number) + "/render-tickets", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Refused<Uri>(response.StatusCode, response.Headers.RetryAfter?.Delta, syncOffOnNotFound: false);
            }

            var ticket = await BoundedContent.ReadJsonAsync(response.Content, ArtifactWebJsonContext.Default.RenderTicketRecord, BoundedContent.MaxSuccessBytes, cancellationToken).ConfigureAwait(false);
            var url = RenderUri(ticket?.Url);
            if (url is null)
            {
                return new ArtifactCall<Uri>(ArtifactCallState.Failed, null, null);
            }

            _tickets[(artifactId, number)] = (url, now);
            return new ArtifactCall<Uri>(ArtifactCallState.Ok, url, null);
        }
        catch (HttpRequestException)
        {
            return new ArtifactCall<Uri>(ArtifactCallState.Offline, null, null);
        }
        catch (ApiClientException)
        {
            return new ArtifactCall<Uri>(ArtifactCallState.Failed, null, null);
        }
    }

    private static string VersionPath(string artifactId, int number) =>
        "cowork/artifacts/" + Uri.EscapeDataString(artifactId) + "/versions/" + number.ToString(CultureInfo.InvariantCulture);

    /// <summary>Resolves a ticket URL against the API origin. Anything outside the render route is refused.</summary>
    private Uri? RenderUri(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var relative = url.TrimStart('/');
        if (!relative.StartsWith(RenderPathPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var baseAddress = _http.Client.BaseAddress ?? throw new InvalidOperationException("The API client has no base address.");
        var absolute = new Uri(baseAddress, relative);
        return string.Equals(absolute.GetLeftPart(UriPartial.Authority), baseAddress.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal) ? absolute : null;
    }

    private async Task<ArtifactCall<T>> GetJsonAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await _http.Client.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Refused<T>(response.StatusCode, response.Headers.RetryAfter?.Delta, syncOffOnNotFound: false);
            }

            var value = await BoundedContent.ReadJsonAsync(response.Content, typeInfo, BoundedContent.MaxSuccessBytes, cancellationToken).ConfigureAwait(false);
            return value is null
                ? new ArtifactCall<T>(ArtifactCallState.Failed, null, null)
                : new ArtifactCall<T>(ArtifactCallState.Ok, value, null);
        }
        catch (HttpRequestException)
        {
            return new ArtifactCall<T>(ArtifactCallState.Offline, null, null);
        }
        catch (ApiClientException)
        {
            return new ArtifactCall<T>(ArtifactCallState.Failed, null, null);
        }
    }

    private static ArtifactCall<T> Refused<T>(HttpStatusCode status, TimeSpan? retryAfter, bool syncOffOnNotFound)
    {
        var state = status switch
        {
            HttpStatusCode.Unauthorized => ArtifactCallState.SignInRequired,
            HttpStatusCode.NotFound when syncOffOnNotFound => ArtifactCallState.SyncOff,
            HttpStatusCode.NotFound => ArtifactCallState.NotFound,
            HttpStatusCode.TooManyRequests => ArtifactCallState.RateLimited,
            _ => ArtifactCallState.Failed,
        };
        return new ArtifactCall<T>(state, default, state == ArtifactCallState.RateLimited ? retryAfter : null);
    }
}
