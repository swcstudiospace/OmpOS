using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// An in-process OTLP/HTTP collector on a loopback port. It records the path, the request headers and the (small) body of every export request, answers
/// <c>GET /health</c> with 200 and answers every export with the configured status. Header values are kept for assertions on the Greptime routing headers only;
/// the tests never print them.
/// </summary>
internal sealed class RecordingOtlpCollector : IAsyncDisposable
{
    private const int MaxBodyBytes = 1 << 20;

    private readonly WebApplication _app;
    private readonly ConcurrentQueue<RecordedExport> _exports = new();

    private RecordingOtlpCollector(WebApplication app, Uri endpoint)
    {
        _app = app;
        Endpoint = endpoint;
    }

    /// <summary>Gets the base address of the collector, with a trailing slash.</summary>
    internal Uri Endpoint { get; }

    /// <summary>Gets or sets the HTTP status the collector answers exports with.</summary>
    internal int ExportStatus { get; set; } = StatusCodes.Status200OK;

    /// <summary>Starts a collector on an ephemeral loopback port.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running collector.</returns>
    internal static async Task<RecordingOtlpCollector> StartAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        RecordingOtlpCollector? collector = null;
        _ = app.MapGet("/health", static () => Results.Ok());
        _ = app.MapPost("/v1/{signal}", async (HttpContext context) =>
        {
            using var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body, context.RequestAborted).ConfigureAwait(false);
            var bytes = body.Length > MaxBodyBytes ? body.GetBuffer().AsSpan(0, MaxBodyBytes).ToArray() : body.ToArray();
            var headers = context.Request.Headers.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
            collector!._exports.Enqueue(new RecordedExport(context.Request.Path.Value ?? string.Empty, headers, bytes));
            return Results.StatusCode(collector.ExportStatus);
        });
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        collector = new RecordingOtlpCollector(app, new Uri(address.EndsWith('/') ? address : address + "/"));
        return collector;
    }

    /// <summary>Returns the exports received so far, in arrival order.</summary>
    /// <returns>A snapshot.</returns>
    internal RecordedExport[] Exports() => [.. _exports];

    /// <summary>Waits until an export that satisfies <paramref name="match"/> arrived.</summary>
    /// <param name="match">The condition.</param>
    /// <param name="timeout">How long to wait on the wall clock; the exporters of the host run on real timers.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns><see langword="true"/> when an export matched in time.</returns>
    internal async Task<bool> WaitForAsync(Func<RecordedExport, bool> match, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (!_exports.Any(match))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _app.DisposeAsync().ConfigureAwait(false);

    /// <summary>One export request: the path, the headers and the body.</summary>
    /// <param name="Path">The request path.</param>
    /// <param name="Headers">The request headers by name.</param>
    /// <param name="Body">The first megabyte of the body.</param>
    internal sealed record RecordedExport(string Path, IReadOnlyDictionary<string, string> Headers, byte[] Body)
    {
        /// <summary>Returns whether the body contains <paramref name="text"/> as UTF-8.</summary>
        /// <param name="text">The text to look for.</param>
        /// <returns><see langword="true"/> when it is present.</returns>
        internal bool BodyContains(string text) => Body.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0;
    }
}
