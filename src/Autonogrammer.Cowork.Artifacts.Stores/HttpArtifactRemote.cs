using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ArtifactMetadataDocument = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.SyncMetadata;
using ArtifactRemoteResult = Autonogrammer.Cowork.Artifacts.Stores.HttpArtifactRemote.RemoteResult;
using ArtifactRemoteStatus = Autonogrammer.Cowork.Artifacts.Stores.HttpArtifactRemote.RemoteStatus;
using ArtifactWireJsonContext = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.JsonContext;

namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// The only network path sync uses. It sends raw version bytes when sync is on, and a metadata document with no body bytes when sync is off.
/// </summary>
public sealed class HttpArtifactRemote
{
    /// <summary>What the account store answered, without a token, a body or a title.</summary>
    /// <param name="Status">The mapped status.</param>
    /// <param name="RetryAfter">The delay from <c>Retry-After</c>, when the status is rate limited.</param>
    internal sealed record RemoteResult(RemoteStatus Status, TimeSpan? RetryAfter);

    /// <summary>Mapped HTTP statuses for artifact sync.</summary>
    internal enum RemoteStatus
    {
        /// <summary>The version was stored.</summary>
        Accepted,

        /// <summary>The same hash already existed. No new write.</summary>
        SameHash,

        /// <summary>Different bytes for an existing number.</summary>
        Divergence,

        /// <summary>The body was over the cap.</summary>
        TooLarge,

        /// <summary>The account asked the client to wait.</summary>
        RateLimited,

        /// <summary>The bearer was missing or rejected.</summary>
        SignedOut,

        /// <summary>The network call failed. The caller queues and retries.</summary>
        Offline,
    }

    private readonly HttpClient _http;
    private readonly Func<CancellationToken, ValueTask<string?>> _bearer;

    /// <summary>Initializes a new instance of the <see cref="HttpArtifactRemote"/> class.</summary>
    /// <param name="http">The host HTTP client. This type does not dispose it.</param>
    /// <param name="bearer">Supplies the account bearer, or null when the desktop is signed out.</param>
    public HttpArtifactRemote(HttpClient http, Func<CancellationToken, ValueTask<string?>> bearer)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(bearer);
        _http = http;
        _bearer = bearer;
    }

    /// <summary>Sends metadata only. The request body is the metadata document, never version bytes.</summary>
    /// <param name="metadata">The metadata document.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped result.</returns>
    internal async Task<ArtifactRemoteResult> PushMetadataAsync(ArtifactMetadataDocument metadata, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var json = JsonSerializer.SerializeToUtf8Bytes(metadata, ArtifactWireJsonContext.Default.SyncMetadata);
        HttpRequestMessage? request = null;
        try
        {
            request = new HttpRequestMessage(HttpMethod.Put, ArtifactWireContract.VersionPath(metadata.Id, metadata.VersionCount))
            {
                Content = new ByteArrayContent(json),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ArtifactWireContract.MetadataMediaType);
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.ArtifactIdHeader, metadata.Id);
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.KindHeader, metadata.Kind);
            if (metadata.Hashes.Count > 0)
            {
                request.Headers.TryAddWithoutValidation(ArtifactWireContract.ContentSha256Header, metadata.Hashes[^1]);
            }

            var result = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            request = null;
            return result;
        }
        finally
        {
            request?.Dispose();
        }
    }

    /// <summary>Puts raw version bytes. Same hash is idempotent; different bytes for an existing number are a divergence.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="content">The version bytes.</param>
    /// <param name="contentSha256">The lowercase hex hash.</param>
    /// <param name="kind">The kind name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The mapped result.</returns>
    internal async Task<ArtifactRemoteResult> PushVersionAsync(
        string artifactId,
        int number,
        ReadOnlyMemory<byte> content,
        string contentSha256,
        string kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(contentSha256);
        ArgumentNullException.ThrowIfNull(kind);
        HttpRequestMessage? request = null;
        try
        {
            request = new HttpRequestMessage(HttpMethod.Put, ArtifactWireContract.VersionPath(artifactId, number))
            {
                Content = new ByteArrayContent(content.ToArray()),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(ArtifactWireContract.MediaType);
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.ArtifactIdHeader, artifactId);
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.VersionHeader, number.ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.ContentSha256Header, contentSha256);
            request.Headers.TryAddWithoutValidation(ArtifactWireContract.KindHeader, kind);
            var result = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            request = null;
            return result;
        }
        finally
        {
            request?.Dispose();
        }
    }

    private async Task<ArtifactRemoteResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _bearer(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {

            return new ArtifactRemoteResult(ArtifactRemoteStatus.SignedOut, null);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return Map(response);
        }
        catch (HttpRequestException)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.Offline, null);
        }
    }

    private static ArtifactRemoteResult Map(HttpResponseMessage response)
    {
        var code = (int)response.StatusCode;
        if (code == ArtifactWireContract.StatusSignedOut)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.SignedOut, null);
        }

        if (code == ArtifactWireContract.StatusTooLarge)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.TooLarge, null);
        }

        if (code == ArtifactWireContract.StatusDivergence)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.Divergence, null);
        }

        if (code == ArtifactWireContract.StatusRateLimited)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.RateLimited, ParseRetryAfter(response.Headers.RetryAfter));
        }

        if (code == ArtifactWireContract.StatusSameHash)
        {
            var idempotent = response.Headers.Contains(ArtifactWireContract.SameHashHeader);
            return new ArtifactRemoteResult(idempotent ? ArtifactRemoteStatus.SameHash : ArtifactRemoteStatus.Accepted, null);
        }

        if (response.StatusCode == HttpStatusCode.Created)
        {
            return new ArtifactRemoteResult(ArtifactRemoteStatus.Accepted, null);
        }

        return new ArtifactRemoteResult(ArtifactRemoteStatus.Offline, null);
    }

    private static TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? header)
    {
        if (header?.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (header?.Date is DateTimeOffset date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }
}
