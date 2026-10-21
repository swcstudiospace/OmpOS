using System.Net;
using System.Text;
using OmpOS.Contracts.Streaming;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>
/// Regression test for the conversation-list crash: after a session has
/// streamed events, listing sessions threw NoMetadataForType for
/// TextDeltaPayload because the web head decoded Session[] through
/// WebJsonContext, which lacks the derived payload metadata.
/// </summary>
public sealed class SessionListEventsTests
{
    /// <summary>Listing sessions decodes text_delta event payloads.</summary>
    [Fact]
    public async Task ListSessionsDecodesTextDeltaEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "sessions-with-events.json");
        var fixture = await File.ReadAllTextAsync(fixturePath, ct).ConfigureAwait(true);
        using var handler = new ScriptedHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        var client = new ApiSessionClient(http);
        using var queued = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(fixture, Encoding.UTF8, "application/json"),
        };
        handler.Enqueue(queued);

        var sessions = await client.ListSessionsAsync(ct).ConfigureAwait(true);

        sessions.Count.ShouldBe(2);
        var streamed = sessions[1];
        var delta = streamed.Events.Select(static e => e.Payload).OfType<TextDeltaPayload>().Single();
        delta.Delta.ShouldBe("echo: Smoke prompt: list the repo layers");
    }
}
