using System.Net;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.State;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>
/// The composer returns to idle on every terminal path: a completed turn and a refused send. These ran against the old page with scripted
/// HTTP; they now run against the workbench with scripted HTTP, because the page only mounts the workbench's view models.
/// </summary>
public sealed class ComposerBusyTests
{
    private const string Id = "composer-busy-test";

    /// <summary>A completed turn flips the composer back from streaming to idle with no error shown.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CompletedTurnReleasesTheComposer()
    {
        using var handler = new RoutedHandler();
        using var gate = new GatedSseContent(
            SseFixtures.Frame(SseFixtures.Text(1, "hi"), "text_delta"),
            SseFixtures.Frame(SseFixtures.Completed(2), "completed"));
        ScriptBoot(handler);
        handler.On(HttpMethod.Post, $"/sessions/{Id}/prompts", _ => new HttpResponseMessage(HttpStatusCode.Accepted));
        handler.On(HttpMethod.Get, $"/sessions/{Id}/events", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = gate });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        var workbench = NewWorkbench(http);
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await workbench.Panel.NewSessionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();

        session.Composer.Draft = "hello";
        await session.Composer.SendAsync().ConfigureAwait(true);
        session.Composer.IsStreaming.ShouldBeTrue("the composer shows Stop while the turn streams");

        gate.Release();
        await WaitUntilAsync(session.Composer, () => !session.Composer.IsStreaming).ConfigureAwait(true);

        session.Session.State.ShouldBe(SessionViewState.Success);
        session.Session.LastError.ShouldBeNull();
    }

    /// <summary>A refused send shows the error and leaves the composer idle instead of stuck.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task RefusedSendShowsErrorAndKeepsComposerIdle()
    {
        using var handler = new RoutedHandler();
        ScriptBoot(handler);
        handler.On(HttpMethod.Post, $"/sessions/{Id}/prompts", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"title":"Prompt execution failed.","status":500}""", Encoding.UTF8, "application/problem+json"),
        });
        using var http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
        var workbench = NewWorkbench(http);
        await using var scope = workbench.ConfigureAwait(true);
        await workbench.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await workbench.Panel.NewSessionAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var session = workbench.Panel.Selected.ShouldNotBeNull();

        session.Composer.Draft = "hello";
        await session.Composer.SendAsync().ConfigureAwait(true);

        session.Session.State.ShouldBe(SessionViewState.RecoverableError);
        session.Session.LastError.ShouldNotBeNull().ShouldContain("Prompt execution failed");
        session.Composer.IsStreaming.ShouldBeFalse();
        session.Composer.Draft.ShouldBe("hello");
    }

    private static WebWorkbench NewWorkbench(HttpClient http)
    {
        var time = new ManualTimeProvider();
        return new WebWorkbench(
            new OmpApiClient(http),
            new UserEventFeed(http, new SseStreamOptions { MaxReconnects = null }, time),
            new NoJsRuntime(),
            time,
            new WebWorkbenchOptions(IsDemo: false, ApiHost: "127.0.0.1:5187"),
            []);
    }

    private static async Task WaitUntilAsync(System.ComponentModel.INotifyPropertyChanged source, Func<bool> condition)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (condition())
            {
                _ = reached.TrySetResult();
            }
        }

        source.PropertyChanged += OnChanged;
        try
        {
            if (!condition())
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            source.PropertyChanged -= OnChanged;
        }
    }

    /// <summary>Scripts everything the workbench reads while it boots and while it opens the one session this test creates.</summary>
    /// <param name="handler">The handler to script.</param>
    private static void ScriptBoot(RoutedHandler handler)
    {
        var listed = 0;
        var entry = new SessionCatalogEntry
        {
            Id = Id,
            OwnerId = "local-dev",
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            Status = SessionStatus.Idle,
            Messages = [],
            Events = [],
            Kind = CatalogSessionKind.Agent,
            RowStatus = CatalogRowStatus.Ready,
            Group = CatalogRowGroup.Idle,
            LastActivityAt = DateTimeOffset.UnixEpoch,
            Closed = false,
        };
        handler.On(HttpMethod.Get, "/sessions", _ => Json(++listed == 1 ? "[]" : JsonSerializer.Serialize(new[] { entry }, CatalogJsonContext.Default.SessionCatalogEntryArray)));
        handler.OnProblem(HttpMethod.Get, "/workspace", HttpStatusCode.NotFound, null, "Not found.", "No workspace.");
        handler.OnProblem(HttpMethod.Get, "/source", HttpStatusCode.NotFound, null, "Not found.", "No source.");
        handler.On(HttpMethod.Get, "/events", _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ChunkedSseContent([string.Empty]) });
        handler.On(HttpMethod.Post, "/sessions", _ => Json(
            $$"""{"id":"{{Id}}","ownerId":"local-dev","createdAt":"2026-10-21T00:00:00+00:00","updatedAt":"2026-10-21T00:00:00+00:00","status":"Idle","messages":[],"events":[]}""",
            HttpStatusCode.Created));
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}", $$"""{"id":"{{Id}}","ownerId":"local-dev","createdAt":"2026-10-21T00:00:00+00:00","updatedAt":"2026-10-21T00:00:00+00:00","status":"Idle","messages":[],"events":[]}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/stages", $$"""{"sessionId":"{{Id}}","available":false,"reason":"none","stages":[]}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/context", $$"""{"sessionId":"{{Id}}","available":false,"reason":"none"}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/config-options", $$"""{"sessionId":"{{Id}}","available":false,"live":false,"reason":"none","options":[]}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/commands", $$"""{"sessionId":"{{Id}}","available":false,"live":false,"reason":"none","commands":[]}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/changes", $$"""{"sessionId":"{{Id}}","workspaceAvailable":false,"changes":[]}""");
        handler.OnJson(HttpMethod.Get, $"/sessions/{Id}/tool-approvals", $$"""{"sessionId":"{{Id}}","approvals":[]}""");
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
