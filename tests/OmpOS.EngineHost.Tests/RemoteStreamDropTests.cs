using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.EngineHost.Tests;

/// <summary>
/// A transport drop on a prompt's own stream surfaces to the caller and is logged as a warning. The drop still
/// resumes through <see cref="RemoteEngineHost.ReconnectAsync"/>, which keeps its contract.
/// </summary>
public sealed class RemoteStreamDropTests
{
    /// <summary>A prompt stream that ends after one event, with no terminal, throws to its caller and logs a warning.</summary>
    [Fact]
    public async Task PromptStreamEndingWithoutTerminalThrowsAndLogsWarning()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new DropThenResumeHandler();
        using var http = ClientFor(handler);
        var logger = new CapturingLogger<RemoteEngineHost>();
        var host = new RemoteEngineHost(http, logger: logger);
        var seen = new List<HarnessEvent>();

        await Should.ThrowAsync<DisconnectedHarnessException>(async () =>
        {
            await foreach (var evt in host.SubmitPromptAsync("prompt", new PromptOptions(), ct).ConfigureAwait(false))
            {
                seen.Add(evt);
            }
        });

        seen.ShouldBe([new TextDeltaEvent("a")]);
        logger.Entries.Select(entry => entry.Level).ShouldContain(LogLevel.Warning);
    }

    /// <summary>After the surfaced drop, <c>ReconnectAsync</c> fetches the missed events and holds them for <c>TakeHeldEvents</c>.</summary>
    [Fact]
    public async Task DroppedPromptStreamStillResumesThroughReconnect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new DropThenResumeHandler();
        using var http = ClientFor(handler);
        var host = new RemoteEngineHost(http, logger: new CapturingLogger<RemoteEngineHost>());

        await Should.ThrowAsync<DisconnectedHarnessException>(async () =>
        {
            await foreach (var evt in host.SubmitPromptAsync("prompt", new PromptOptions(), ct).ConfigureAwait(false))
            {
                _ = evt;
            }
        });

        await host.ReconnectAsync(ct);

        host.TakeHeldEvents().ShouldBe([new TextDeltaEvent("b"), new CompletedEvent("end_turn")]);
        handler.ResumePositions.ShouldBe(["1"]);
    }

    private static HttpClient ClientFor(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.local/") };

    /// <summary>
    /// Creates session <c>sess-1</c> and accepts prompts. The first event stream delivers Seq 1 and ends with no terminal.
    /// A stream carrying <c>Last-Event-ID</c> delivers Seq 2 and a completion.
    /// </summary>
    private sealed class DropThenResumeHandler : HttpMessageHandler
    {
        /// <summary>Gets every Last-Event-ID value received, in arrival order.</summary>
        public List<string?> ResumePositions { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions", StringComparison.Ordinal))
            {
                return Task.FromResult(Respond(HttpStatusCode.Created, """{"id":"sess-1","ownerId":"local-dev"}"""));
            }

            if (request.Method == HttpMethod.Post && string.Equals(path, "/sessions/sess-1/prompts", StringComparison.Ordinal))
            {
                return Task.FromResult(Respond(HttpStatusCode.Accepted, """{"sessionId":"sess-1","status":"accepted"}"""));
            }

            if (request.Method == HttpMethod.Get && string.Equals(path, "/sessions/sess-1/events", StringComparison.Ordinal))
            {
                if (request.Headers.TryGetValues(RemoteEngineHost.LastEventIdHeader, out var values))
                {
                    ResumePositions.Add(values.FirstOrDefault());
                    return Task.FromResult(Sse(
                        "id: 2\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"b\"}\n\n",
                        "id: 3\nevent: completed\ndata: {\"kind\":\"completed\",\"reason\":\"end_turn\"}\n\n"));
                }

                return Task.FromResult(Sse("id: 1\nevent: text_delta\ndata: {\"kind\":\"text_delta\",\"delta\":\"a\"}\n\n"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Respond(HttpStatusCode status, string body) => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        private static HttpResponseMessage Sse(params string[] frames) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Concat(frames), Encoding.UTF8, "text/event-stream"),
        };
    }
}
