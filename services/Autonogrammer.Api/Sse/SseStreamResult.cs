using System.Net.ServerSentEvents;
using System.Text;
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
        var enumerator = _source.GetAsyncEnumerator(token);
        using var timer = heartbeat > TimeSpan.Zero ? new PeriodicTimer(heartbeat, time) : null;
        var lastWrite = time.GetTimestamp();
        Task<bool> pending = Task.FromResult(false);
        var pendingStarted = false;
        try
        {
            pending = enumerator.MoveNextAsync().AsTask();
            pendingStarted = true;
            Task<bool>? tick = null;
            while (true)
            {
                if (timer is not null)
                {
                    tick ??= timer.WaitForNextTickAsync(token).AsTask();
                    var done = await Task.WhenAny(pending, tick).ConfigureAwait(false);
                    if (ReferenceEquals(done, tick))
                    {
                        _ = await tick.ConfigureAwait(false);
                        tick = null;
                        if (time.GetElapsedTime(lastWrite) >= heartbeat)
                        {
                            lastWrite = time.GetTimestamp();
                            await WriteRawAsync(body, KeepAliveFrame, token).ConfigureAwait(false);
                        }

                        continue;
                    }
                }

                if (!await pending.ConfigureAwait(false))
                {
                    return;
                }

                lastWrite = time.GetTimestamp();
                await SseFormatter.WriteAsync(SingleAsync(enumerator.Current), body, token).ConfigureAwait(false);
                pending = enumerator.MoveNextAsync().AsTask();
            }
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
            await DrainAsync(pending, pendingStarted).ConfigureAwait(false);
            await enumerator.DisposeAsync().ConfigureAwait(false);
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

    private static async Task DrainAsync(Task<bool> pending, bool started)
    {
        if (!started || pending.IsCompleted)
        {
            return;
        }

        try
        {
            _ = await pending.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by the caller; the enumerator is idle and can be disposed.
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
