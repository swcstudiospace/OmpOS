using System.Net;
using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Sessions;
using OmpOS.Web.Services;
using Shouldly;
using Xunit;

namespace OmpOS.Web.Tests;

/// <summary>The user-level event stream: parsing, resume with <c>Last-Event-ID</c>, duplicate suppression and resync.</summary>
public sealed class UserEventFeedTests
{
    private static readonly SseStreamOptions NoDelay = new() { InitialRetryDelay = TimeSpan.Zero, MaxRetryDelay = TimeSpan.Zero, MaxReconnects = null };

    /// <summary>The three event kinds are read into their typed payloads and the id is kept.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ParsesEveryKindWithItsPayload()
    {
        using var handler = new RoutedHandler();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var frames = Frame(1, CatalogEventKinds.CatalogChanged, Changed("s-1", CatalogChangeKind.Created))
            + Frame(2, CatalogEventKinds.SessionStatus, JsonSerializer.Serialize(
                new SessionStatusEvent { SessionId = "s-1", Status = SessionStatus.Running, RowStatus = CatalogRowStatus.Running, Group = CatalogRowGroup.Running, LastActivityAt = DateTimeOffset.UnixEpoch },
                CatalogJsonContext.Default.SessionStatusEvent))
            + Frame(3, CatalogEventKinds.Stage, JsonSerializer.Serialize(
                new StageEvent { SessionId = "s-1", Source = StageSource.Gsd, StageId = "g", Title = "Plan", Status = StageStatus.Running, Timestamp = DateTimeOffset.UnixEpoch },
                CatalogJsonContext.Default.StageEvent));
        handler.On(HttpMethod.Get, "/events", _ => Stream(frames));
        handler.On(HttpMethod.Get, "/events?since=3", _ =>
        {
            cancellation.Cancel();
            return Stream(string.Empty);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var events = await ReadAsync(new UserEventFeed(http, NoDelay), null, cancellation.Token).ConfigureAwait(true);

        events.Select(static e => e.Kind).ShouldBe(["catalog_changed", "session_status", "stage"]);
        events.Select(static e => e.Id).ShouldBe(new long?[] { 1, 2, 3 });
        events[0].Catalog!.Change.ShouldBe(CatalogChangeKind.Created);
        events[1].Status!.RowStatus.ShouldBe(CatalogRowStatus.Running);
        events[2].Stage!.Title.ShouldBe("Plan");
    }

    /// <summary>A dropped connection reconnects with the last id in both the header and the query, and an id seen twice is delivered once.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ReconnectResumesFromTheLastIdWithoutRepeats()
    {
        using var handler = new RoutedHandler();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        handler.On(HttpMethod.Get, "/events", _ => Stream(Frame(1, "catalog_changed", Changed("a", CatalogChangeKind.Updated)) + Frame(2, "catalog_changed", Changed("b", CatalogChangeKind.Updated))));
        handler.On(HttpMethod.Get, "/events?since=2", _ => Stream(Frame(2, "catalog_changed", Changed("b", CatalogChangeKind.Updated)) + Frame(3, "catalog_changed", Changed("c", CatalogChangeKind.Updated))));
        handler.On(HttpMethod.Get, "/events?since=3", _ =>
        {
            cancellation.Cancel();
            return Stream(string.Empty);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var events = await ReadAsync(new UserEventFeed(http, NoDelay), null, cancellation.Token).ConfigureAwait(true);

        events.Select(static e => e.Catalog!.SessionId).ShouldBe(["a", "b", "c"]);
        handler.Headers[0].ContainsKey("Last-Event-ID").ShouldBeFalse("the first connection has no position: it is the live tail");
        handler.Headers[1]["Last-Event-ID"].ShouldBe("2");
        handler.Headers[2]["Last-Event-ID"].ShouldBe("3");
    }

    /// <summary>A starting position is sent on the first connection.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task StartingPositionIsSentOnTheFirstConnection()
    {
        using var handler = new RoutedHandler();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        handler.On(HttpMethod.Get, "/events?since=41", _ =>
        {
            cancellation.Cancel();
            return Stream(string.Empty);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        _ = await ReadAsync(new UserEventFeed(http, NoDelay), 41, cancellation.Token).ConfigureAwait(true);

        handler.Headers[0]["Last-Event-ID"].ShouldBe("41");
    }

    /// <summary>After a resync the old high-water mark is dropped: the lower ids of the new server process are delivered, and the next reconnect resumes from them.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ResyncStartsThePositionOver()
    {
        using var handler = new RoutedHandler();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var resync = "event: resync\ndata: {\"reason\":\"history gap\"}\n\n";
        handler.On(HttpMethod.Get, "/events", _ => Stream(Frame(5, "catalog_changed", Changed("old", CatalogChangeKind.Updated))));
        handler.On(HttpMethod.Get, "/events?since=5", _ => Stream(resync + Frame(1, "catalog_changed", Changed("new", CatalogChangeKind.Created))));
        handler.On(HttpMethod.Get, "/events?since=1", _ =>
        {
            cancellation.Cancel();
            return Stream(string.Empty);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var events = await ReadAsync(new UserEventFeed(http, NoDelay), null, cancellation.Token).ConfigureAwait(true);

        events.Select(static e => e.Kind).ShouldBe(["catalog_changed", "resync", "catalog_changed"]);
        events[1].Id.ShouldBeNull();
        events[1].Resync!.Reason.ShouldBe("history gap");
        events[2].Catalog!.SessionId.ShouldBe("new");
    }

    /// <summary>A frame that is not valid for its kind, and a kind the client does not know, are skipped; their ids still advance the position.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task MalformedAndUnknownFramesAreSkipped()
    {
        using var handler = new RoutedHandler();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        handler.On(HttpMethod.Get, "/events", _ => Stream(
            Frame(1, "catalog_changed", "{not json")
            + Frame(2, "future_kind", "{}")
            + Frame(3, "catalog_changed", Changed("ok", CatalogChangeKind.Closed))));
        handler.On(HttpMethod.Get, "/events?since=3", _ =>
        {
            cancellation.Cancel();
            return Stream(string.Empty);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var events = await ReadAsync(new UserEventFeed(http, NoDelay), null, cancellation.Token).ConfigureAwait(true);

        events.Single().Catalog!.SessionId.ShouldBe("ok");
    }

    /// <summary>A refused connection is a problem exception, not a silent retry.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task UnauthorizedConnectionThrows()
    {
        using var handler = new RoutedHandler();
        handler.OnProblem(HttpMethod.Get, "/events", HttpStatusCode.Unauthorized, null, "Unauthorized.", "Sign in.");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };

        var problem = await Should.ThrowAsync<ApiProblemException>(() => ReadAsync(new UserEventFeed(http, NoDelay), null, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        problem.Kind.ShouldBe(ApiProblemKind.Unauthorized);
    }

    /// <summary>With a reconnect bound, a stream that never delivers gives up with a client exception.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ReconnectBoundEndsAStreamThatDeliversNothing()
    {
        using var handler = new RoutedHandler();
        handler.On(HttpMethod.Get, "/events", _ => Stream(string.Empty));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.invalid/", UriKind.Absolute) };
        var feed = new UserEventFeed(http, new SseStreamOptions { InitialRetryDelay = TimeSpan.Zero, MaxRetryDelay = TimeSpan.Zero, MaxReconnects = 2 });

        await Should.ThrowAsync<ApiClientException>(() => ReadAsync(feed, null, TestContext.Current.CancellationToken)).ConfigureAwait(true);

        handler.Requests.Count.ShouldBe(3);
    }

    private static string Changed(string session, CatalogChangeKind kind) =>
        JsonSerializer.Serialize(
            new CatalogChangedEvent { SessionId = session, Change = kind, RowStatus = CatalogRowStatus.Ready, Group = CatalogRowGroup.Idle },
            CatalogJsonContext.Default.CatalogChangedEvent);

    private static string Frame(long id, string kind, string json) => $"id: {id}\nevent: {kind}\ndata: {json}\n\n";

    private static HttpResponseMessage Stream(string frames) => new(HttpStatusCode.OK)
    {
        Content = new ChunkedSseContent([frames]),
    };

    private static async Task<List<UserStreamEvent>> ReadAsync(UserEventFeed feed, long? after, CancellationToken cancellationToken)
    {
        var events = new List<UserStreamEvent>();
        try
        {
            await foreach (var evt in feed.StreamAsync(after, cancellationToken).ConfigureAwait(true))
            {
                events.Add(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // The test cancels the stream once the scripted connections ran.
        }

        return events;
    }
}
