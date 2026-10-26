using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace OmpOS.Web.Services;

/// <summary>
/// Size-bounded reads of HTTP response bodies. A declared Content-Length above the limit is refused
/// before any byte is read, and a streamed body is refused as soon as it passes the limit, so a
/// hostile or broken origin cannot exhaust browser memory.
/// </summary>
internal static class BoundedContent
{
    /// <summary>The largest API success body accepted, in bytes.</summary>
    internal const long MaxSuccessBytes = 16 * 1024 * 1024;

    /// <summary>The largest API error body read for diagnostics, in bytes.</summary>
    internal const long MaxErrorBytes = 1024 * 1024;

    /// <summary>Reads a bounded body and deserializes it as JSON with the given source-generated metadata.</summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="content">The response content.</param>
    /// <param name="typeInfo">The source-generated metadata for <typeparamref name="T"/>.</param>
    /// <param name="maxBytes">The largest body accepted, in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized value.</returns>
    /// <exception cref="ApiClientException">The body is larger than <paramref name="maxBytes"/>.</exception>
    internal static async Task<T?> ReadJsonAsync<T>(
        HttpContent content,
        JsonTypeInfo<T> typeInfo,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(content, buffer, maxBytes, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync(buffer, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a bounded body as text, decoding UTF-8 and honouring a byte-order mark.</summary>
    /// <param name="content">The response content.</param>
    /// <param name="maxBytes">The largest body accepted, in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The body text.</returns>
    /// <exception cref="ApiClientException">The body is larger than <paramref name="maxBytes"/>.</exception>
    internal static async Task<string> ReadTextAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await CopyBoundedAsync(content, buffer, maxBytes, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Copies a body into <paramref name="destination"/>, refusing it once it would exceed <paramref name="maxBytes"/>.</summary>
    /// <param name="content">The response content.</param>
    /// <param name="destination">The buffer that receives the body.</param>
    /// <param name="maxBytes">The largest body accepted, in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ApiClientException">The declared or streamed body is larger than <paramref name="maxBytes"/>.</exception>
    private static async Task CopyBoundedAsync(
        HttpContent content,
        Stream destination,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Headers.ContentLength is long declared && declared > maxBytes)
        {
            throw new ApiClientException($"The response body is {declared} bytes, over the {maxBytes}-byte limit.");
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
                throw new ApiClientException($"The response body exceeds the {maxBytes}-byte limit.");
            }

            await destination.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
