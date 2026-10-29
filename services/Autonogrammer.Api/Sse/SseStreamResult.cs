using System.Net.ServerSentEvents;
using System.Text;
using System.Threading.Channels;
using Autonogrammer.Api.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Autonogrammer.Api.Sse;

/// <summary>
/// Writes a server-sent event stream with the hosting guarantees: no request timeout, no buffering or proxy transformation,
/// a <c>: keepalive</c> comment whenever the stream has been idle for <c>Hosting:Sse:HeartbeatSeconds</c>, and a clean end
/// after <c>retry: 2000</c> when the host is stopping, so the client reconnects to another instance with <c>Last-Event-ID</c>.
/// Item framing (id, event, data, retry) is the framework's <see cref="SseFormatter"/>, unchanged.
/// </summary>
internal sealed class SseStreamResult : IResult
{
    /// <summary>The comment frame written while the stream is idle.</summary>
    internal static readonly byte[] KeepAliveFrame = Encoding.UTF8.GetBytes(": keepalive\n\n");

    /// <summary>The frame written when the host stops, before the stream ends.</summary>
    internal static readonly byte[] ShutdownFrame = Encoding.UTF8.GetBytes("retry: 2000\n\n");

    private readonly IAsyncEnumerable<SseItem<string>> _source;

    /// <summary>Initializes a new instance of the <see cref="SseStreamResult"/> class.</summary>
    /// <param name="source">The events to write. The source must honour its cancellation token.</param>
    public SseStreamResult(IAsyncEnumerable<SseItem<string>> source)
    {
        _source = source;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var services = httpContext.RequestServices;
        var heartbeat = TimeSpan.FromSeconds(services.GetRequiredService<IOptions<HostingOptions>>().Value.Sse.HeartbeatSeconds);
        var time = services.GetRequiredService<TimeProvider>();
        var stopping = services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        await StartResponseAsync(httpContext).ConfigureAwait(false);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, stopping);
        var token = linked.Token;
        var body = httpContext.Response.Body;
        var channel = Channel.CreateBounded<SseItem<string>>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var producer = ProduceAsync(_source, channel.Writer, token);
        try
        {
            await WriteLoopAsync(channel.Reader, body, time, heartbeat, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!httpContext.RequestAborted.IsCancellationRequested && stopping.IsCancellationRequested)
        {
            await WriteRawAsync(body, ShutdownFrame, httpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is no one to write to.
        }
        finally
        {
            await linked.CancelAsync().ConfigureAwait(false);
            try
            {
                await producer.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancelled above; the source ends with the stream.
            }
        }
    }

    private static async Task StartResponseAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.ContentType = "text/event-stream";
        response.Headers[HeaderNames.CacheControl] = "no-store, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await response.StartAsync(httpContext.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>Copies the source into the channel one item at a time, and completes the channel when the source ends or fails.</summary>
    private static async Task ProduceAsync(IAsyncEnumerable<SseItem<string>> source, ChannelWriter<SseItem<string>> writer, CancellationToken token)
    {
        try
        {
            await foreach (var item in source.WithCancellation(token).ConfigureAwait(false))
            {
                await writer.WriteAsync(item, token).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = writer.TryComplete();
        }
    }

    /// <summary>
    /// Writes every item of the channel to the response. When nothing arrives for the heartbeat period the loop writes a keep-alive comment
    /// and waits again, so an idle stream is never silent for longer than one heartbeat.
    /// </summary>
    private static async Task WriteLoopAsync(ChannelReader<SseItem<string>> reader, Stream body, TimeProvider time, TimeSpan heartbeat, CancellationToken token)
    {
        while (true)
        {
            using var idle = heartbeat > TimeSpan.Zero ? new CancellationTokenSource(heartbeat, time) : new CancellationTokenSource();
            using var either = CancellationTokenSource.CreateLinkedTokenSource(token, idle.Token);
            bool more;
            try
            {
                more = await reader.WaitToReadAsync(either.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (idle.IsCancellationRequested && !token.IsCancellationRequested)
            {
                await WriteRawAsync(body, KeepAliveFrame, token).ConfigureAwait(false);
                continue;
            }

            if (!more)
            {
                return;
            }

            while (reader.TryRead(out var item))
            {
                await SseFormatter.WriteAsync(SingleAsync(item), body, token).ConfigureAwait(false);
            }
        }
    }

    private static async Task WriteRawAsync(Stream body, byte[] frame, CancellationToken cancellationToken)
    {
        await body.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async IAsyncEnumerable<SseItem<string>> SingleAsync(SseItem<string> item)
    {
        yield return item;
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
