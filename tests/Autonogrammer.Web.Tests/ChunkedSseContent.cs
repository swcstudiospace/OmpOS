using System.Net;
using System.Text;

namespace Autonogrammer.Web.Tests;

/// <summary>An <see cref="HttpContent"/> that writes fixed chunks and optionally fails mid-body.</summary>
internal sealed class ChunkedSseContent : HttpContent
{
    private readonly string[] _chunks;
    private readonly int _failAfter;

    /// <summary>Initializes a new instance of the <see cref="ChunkedSseContent"/> class.</summary>
    /// <param name="chunks">The body chunks in order.</param>
    /// <param name="failAfter">The chunk index that throws instead of writing, or -1 for success.</param>
    internal ChunkedSseContent(string[] chunks, int failAfter = -1)
    {
        _chunks = chunks;
        _failAfter = failAfter;
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
    }

    /// <inheritdoc />
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        for (var i = 0; i < _chunks.Length; i++)
        {
            if (i == _failAfter)
            {
                throw new IOException("Simulated disconnect.");
            }

            var bytes = Encoding.UTF8.GetBytes(_chunks[i]);
            await stream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
            await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        SerializeToStreamAsync(stream, context);

    /// <inheritdoc />
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
