using System.Net.ServerSentEvents;
using System.Text.Json;
using Autonogrammer.Api.Services;
using Autonogrammer.Contracts.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// The user-level stream <c>GET /events</c>: it carries catalog, status and stage events with identifiers, resumes from <c>Last-Event-ID</c>,
/// announces a position it cannot replay with one <c>resync</c> event, shows only the caller's sessions, and does not end at a terminal session event.
/// A test scans the catalog explicitly and then replays the retained history from position 0, so no test depends on a timer.
/// </summary>
public sealed class CatalogEventStreamTests
{
    /// <summary>A new session produces a created change, a status and a stage event, in that order, with consecutive identifiers.</summary>
    [Fact]
    public async Task NewSession_PublishesChangeStatusAndStage_WithIdentifiers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await factory.Services.GetRequiredService<CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, "/events", 3, ct, lastEventId: "0").ConfigureAwait(true);

        items.Select(static item => item.EventType).ShouldBe(["catalog_changed", "session_status", "stage"]);
        items.Select(static item => item.EventId).ShouldBe(["1", "2", "3"]);
        using var changed = JsonDocument.Parse(items[0].Data);
        changed.RootElement.GetProperty("sessionId").GetString().ShouldBe(id);
        changed.RootElement.GetProperty("change").GetString().ShouldBe("created");
        using var stage = JsonDocument.Parse(items[2].Data);
        stage.RootElement.GetProperty("source").GetString().ShouldBe("Omp");
        stage.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
    }

    /// <summary>A client that resumes with the last identifier it saw gets exactly the later events.</summary>
    [Fact]
    public async Task Resume_WithLastEventId_DeliversOnlyLaterEvents()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        _ = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await factory.Services.GetRequiredService<CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, "/events", 2, ct, lastEventId: "1").ConfigureAwait(true);

        items.Select(static item => item.EventId).ShouldBe(["2", "3"]);
        items.Select(static item => item.EventType).ShouldBe(["session_status", "stage"]);
    }

    /// <summary>A position from another process, or older than the retained history, gets one resync event first.</summary>
    [Fact]
    public async Task Resume_FromAPositionThatIsNotRetained_StartsWithResync()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        _ = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await factory.Services.GetRequiredService<CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, "/events", 1, ct, lastEventId: "999999").ConfigureAwait(true);

        items.ShouldHaveSingleItem().EventType.ShouldBe("resync");
        items[0].EventId.ShouldBeNullOrEmpty();
        using var data = JsonDocument.Parse(items[0].Data);
        data.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A resume position that is not an unsigned integer is a 400.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public async Task Resume_WithAMalformedLastEventId_IsBadRequest(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/events", UriKind.Relative));
        request.Headers.TryAddWithoutValidation("Last-Event-ID", value);

        using var response = await client.SendAsync(request, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    /// <summary>The stream keeps delivering after a session reached a terminal state: a completed run, then a close, are both on it.</summary>
    [Fact]
    public async Task Stream_DoesNotEndAtATerminalSessionEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var detector = factory.Services.GetRequiredService<CatalogChangeDetector>();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await detector.ScanAsync(ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"go\"}", ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);
        await detector.ScanAsync(ct).ConfigureAwait(true);
        using var close = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/close", null, ct).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(client, "/events", 8, ct, lastEventId: "0").ConfigureAwait(true);

        var kinds = items.Select(static item => item.EventType).ToArray();
        kinds.ShouldBe(["catalog_changed", "session_status", "stage", "catalog_changed", "session_status", "stage", "catalog_changed", "session_status"]);
        Change(items[3]).ShouldBe("updated");
        Status(items[4]).ShouldBe("Completed");
        Change(items[6]).ShouldBe("closed");
    }

    /// <summary>The stream of one owner never carries another owner's session.</summary>
    [Fact]
    public async Task Stream_ShowsOnlyTheCallersSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        _ = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);
        var bobSession = await SessionClient.CreateSessionAsync(bob, ct).ConfigureAwait(true);
        await factory.Services.GetRequiredService<CatalogChangeDetector>().ScanAsync(ct).ConfigureAwait(true);

        var items = await SessionClient.ReadStreamAsync(bob, "/events", 3, ct, lastEventId: "0").ConfigureAwait(true);

        foreach (var item in items.Where(static item => string.Equals(item.EventType, "catalog_changed", StringComparison.Ordinal) || string.Equals(item.EventType, "session_status", StringComparison.Ordinal)))
        {
            using var data = JsonDocument.Parse(item.Data);
            data.RootElement.GetProperty("sessionId").GetString().ShouldBe(bobSession);
        }
    }

    /// <summary>A stage a plugin source publishes reaches the stream and the session's own log as a stage event.</summary>
    [Fact]
    public async Task PublishedStage_ReachesTheUserStream()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var published = await factory.Services.GetRequiredService<StageHub>().PublishAsync(StageFixtures.Planner(id, "graph-1"), ct).ConfigureAwait(true);
        published.ShouldBeTrue();

        var items = await SessionClient.ReadStreamAsync(client, "/events", 1, ct, lastEventId: "0").ConfigureAwait(true);

        items.ShouldHaveSingleItem().EventType.ShouldBe("stage");
        using var data = JsonDocument.Parse(items[0].Data);
        data.RootElement.GetProperty("graphId").GetString().ShouldBe("graph-1");
    }

    private static string? Change(SseItem<string> item)
    {
        using var data = JsonDocument.Parse(item.Data);
        return data.RootElement.GetProperty("change").GetString();
    }

    private static string? Status(SseItem<string> item)
    {
        using var data = JsonDocument.Parse(item.Data);
        return data.RootElement.GetProperty("status").GetString();
    }
}
