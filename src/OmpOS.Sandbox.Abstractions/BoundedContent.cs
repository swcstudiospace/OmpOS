using System.Text;

namespace OmpOS.Sandbox.Abstractions;

/// <summary>
/// Size-bounded reads of vendor HTTP response bodies. A declared Content-Length above the limit is refused before any
/// byte is read, and a streamed body is refused as soon as it passes the limit, so a hostile or broken vendor cannot
/// exhaust memory. Refusals are <see cref="InvalidDataException"/>; each provider maps it to its own vendor-error type.
/// </summary>
public static class BoundedContent
{
    /// <summary>The largest vendor response body accepted, in bytes: 4 MiB. Vendor JSON responses are far smaller.</summary>
    public const long MaxVendorBodyBytes = 4L * 1024 * 1024;

    /// <summary>Reads a bounded body as text, decoding UTF-8 and honouring a byte-order mark.</summary>
    /// <param name="content">The response content.</param>
    /// <param name="maxBytes">The largest body accepted, in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The body text.</returns>
    /// <exception cref="InvalidDataException">The body is larger than <paramref name="maxBytes"/>.</exception>
    public static async Task<string> ReadTextAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(content, buffer, maxBytes, cancellationToken).ConfigureAwait(false);
        return DecodeText(buffer);
    }

    private static async Task CopyBoundedAsync(HttpContent content, Stream destination, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength is long declared && declared > maxBytes)
        {
            throw new InvalidDataException($"The vendor response body is {declared} bytes, over the {maxBytes}-byte limit.");
        }

        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var chunk = new byte[8192];
        var received = 0L;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            received += read;
            if (received > maxBytes)
            {
                throw new InvalidDataException($"The vendor response body exceeds the {maxBytes}-byte limit.");
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    // The buffer is in memory, so the synchronous decode cannot block on I/O.
    private static string DecodeText(MemoryStream buffer)
    {
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
