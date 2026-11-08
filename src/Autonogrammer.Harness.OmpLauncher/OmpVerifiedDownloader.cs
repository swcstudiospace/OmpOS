using System.Security.Cryptography;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Streams a download to a partial file while it hashes the bytes, then checks the declared length and the digest. A
/// download that does not verify leaves no partial file behind.
/// </summary>
internal sealed class OmpVerifiedDownloader
{
    private const int BufferBytes = 81920;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    /// <summary>Creates a downloader.</summary>
    /// <param name="http">The client to fetch with.</param>
    /// <param name="timeout">The time allowed for one download, from request to last byte.</param>
    public OmpVerifiedDownloader(HttpClient http, TimeSpan timeout)
    {
        _http = http;
        _timeout = timeout;
    }

    /// <summary>Downloads and verifies one body.</summary>
    /// <param name="spec">What to fetch and what it must match.</param>
    /// <param name="cancellationToken">Cancels the download. A cancelled download throws and leaves no partial file.</param>
    /// <returns>How the download ended. Anything but <see cref="OmpDownloadStatus.Verified"/> has deleted the partial file.</returns>
    public async Task<OmpDownloadStatus> DownloadAsync(OmpDownloadSpec spec, CancellationToken cancellationToken)
    {
        File.Delete(spec.PartPath);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_timeout);
        var status = await FetchAsync(spec, cancellationToken, limit.Token).ConfigureAwait(false);
        if (status != OmpDownloadStatus.Verified)
        {
            OmpCleanup.DeleteFile(spec.PartPath);
        }

        return status;
    }

    private async Task<OmpDownloadStatus> FetchAsync(OmpDownloadSpec spec, CancellationToken userToken, CancellationToken linkedToken)
    {
        try
        {
            using var response = await _http.GetAsync(spec.Uri, HttpCompletionOption.ResponseHeadersRead, linkedToken).ConfigureAwait(false);
            return await ReceiveAsync(response, spec, userToken, linkedToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (OmpFailureMapper.IsTrustFailure(exception))
        {
            return OmpDownloadStatus.TrustFailure;
        }
        catch (HttpRequestException)
        {
            return OmpDownloadStatus.Offline;
        }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested)
        {
            return OmpDownloadStatus.Offline;
        }
    }

    private static async Task<OmpDownloadStatus> ReceiveAsync(HttpResponseMessage response, OmpDownloadSpec spec, CancellationToken userToken, CancellationToken linkedToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return OmpDownloadStatus.HttpStatus;
        }

        using var body = await response.Content.ReadAsStreamAsync(linkedToken).ConfigureAwait(false);
        long? expectedLength = response.Content.Headers.ContentEncoding.Count == 0 ? response.Content.Headers.ContentLength : null;
        return await CopyAsync(body, spec, expectedLength, userToken, linkedToken).ConfigureAwait(false);
    }

    private static async Task<OmpDownloadStatus> CopyAsync(Stream body, OmpDownloadSpec spec, long? expectedLength, CancellationToken userToken, CancellationToken linkedToken)
    {
        using var hash = IncrementalHash.CreateHash(spec.Algorithm);
        using var file = new FileStream(
            spec.PartPath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            });
        var buffer = new byte[BufferBytes];
        long received = 0;
        while (true)
        {
            int read;
            try
            {
                read = await body.ReadAsync(buffer, linkedToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (IsBodyFailure(exception, userToken))
            {
                return OmpDownloadStatus.Truncated;
            }

            if (read == 0)
            {
                break;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), linkedToken).ConfigureAwait(false);
            hash.AppendData(buffer, 0, read);
            received += read;
        }

        if (expectedLength is { } length && length != received)
        {
            return OmpDownloadStatus.Truncated;
        }

        var digest = Convert.ToHexString(hash.GetHashAndReset());
        return string.Equals(digest, spec.ExpectedHex, StringComparison.OrdinalIgnoreCase)
            ? OmpDownloadStatus.Verified
            : OmpDownloadStatus.IntegrityMismatch;
    }

    private static bool IsBodyFailure(Exception exception, CancellationToken userToken) =>
        exception is HttpRequestException or IOException
        || (exception is OperationCanceledException && !userToken.IsCancellationRequested);
}
