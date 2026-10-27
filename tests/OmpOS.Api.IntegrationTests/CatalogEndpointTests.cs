using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Sessions;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The catalog routes: the session list with its additive catalog fields, the board, card details, close, reopen, resume and the workspace line.
/// Every session route answers 404 for a session another owner holds, exactly as it does for an absent session.
/// </summary>
public sealed class CatalogEndpointTests
{
    private const string Alice = "alice";
    private const string Bob = "bob";
    private const string PromptCanary = "board-title-canary-4471";

    /// <summary>The list keeps every field of the stored session under its name and adds the catalog fields.</summary>
    [Fact]
    public async Task List_KeepsSessionFields_AndAddsCatalogFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        var row = document.RootElement.EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("id").GetString().ShouldBe(id, Case.Sensitive);
        row.GetProperty("ownerId").GetString().ShouldBe("local-dev", Case.Sensitive);
        row.GetProperty("status").GetString().ShouldBe("Idle", Case.Sensitive);
        row.GetProperty("messages").GetArrayLength().ShouldBe(0);
        row.GetProperty("events").GetArrayLength().ShouldBe(0);
        row.GetProperty("kind").GetString().ShouldBe("agent", Case.Sensitive);
        row.GetProperty("rowStatus").GetString().ShouldBe("ready", Case.Sensitive);
        row.GetProperty("group").GetString().ShouldBe("idle", Case.Sensitive);
        row.GetProperty("closed").GetBoolean().ShouldBeFalse();
        row.TryGetProperty("lastActivityAt", out _).ShouldBeTrue();
        row.TryGetProperty("graphId", out _).ShouldBeFalse("a null Graph identifier is omitted");
    }

    /// <summary>A finished run moves the row to Completed in the Finished group.</summary>
    [Fact]
    public async Task List_ShowsCompletedRunAsFinished()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", ct).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);

        var row = document.RootElement.EnumerateArray().ShouldHaveSingleItem();
        row.GetProperty("rowStatus").GetString().ShouldBe("completed", Case.Sensitive);
        row.GetProperty("group").GetString().ShouldBe("finished", Case.Sensitive);
    }

    /// <summary>One owner's list and board never hold another owner's session.</summary>
    [Fact]
    public async Task ListAndBoard_ExcludeAnotherOwnersSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, Alice);
        using var bob = WebAppTestHost.AsPrincipal(factory, Bob);
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using var bobList = await bob.GetAsync(new Uri("/sessions", UriKind.Relative), ct).ConfigureAwait(true);
        using var bobBoard = await bob.GetAsync(new Uri("/board", UriKind.Relative), ct).ConfigureAwait(true);
        using var aliceBoard = await alice.GetAsync(new Uri("/board", UriKind.Relative), ct).ConfigureAwait(true);

        using (var list = await WebAppTestHost.ReadJsonAsync(bobList, ct).ConfigureAwait(true))
        {
            list.RootElement.GetArrayLength().ShouldBe(0);
        }

        using (var board = await WebAppTestHost.ReadJsonAsync(bobBoard, ct).ConfigureAwait(true))
        {
            board.RootElement.GetProperty("state").GetString().ShouldBe("empty", Case.Sensitive);
        }

        using var own = await WebAppTestHost.ReadJsonAsync(aliceBoard, ct).ConfigureAwait(true);
        own.RootElement.GetProperty("state").GetString().ShouldBe("ready", Case.Sensitive);
        CardIds(own.RootElement).ShouldBe([id]);
    }

    /// <summary>The board has the ten columns of the desktop board in order; a new session sits in Backlog and a finished one in Completed, titled from its identifier and never from its prompt.</summary>
    [Fact]
    public async Task Board_PlacesCardsByStatus_AndNeverTitlesFromAPrompt()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var idle = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var done = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        using var accepted = await SessionClient.PostPromptAsync(client, done, $"{{\"prompt\":\"{PromptCanary}\"}}", ct).ConfigureAwait(true);
        _ = await SessionClient.WaitForStatusAsync(client, done, SessionStatus.Completed, WebAppTestHost.SettleTimeout, ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri("/board", UriKind.Relative), ct).ConfigureAwait(true);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        using var document = JsonDocument.Parse(text);

        text.ShouldNotContain(PromptCanary, Case.Sensitive);
        var columns = document.RootElement.GetProperty("columns").EnumerateArray().ToArray();
        columns.Select(static column => column.GetProperty("key").GetString()).ShouldBe(
            ["backlog", "queued", "running", "awaiting-input", "needs-attention", "paused", "completed", "failed", "unreachable", "archived"]);
        Column(columns, "backlog").GetProperty("acceptsDrop").GetBoolean().ShouldBeTrue();
        Column(columns, "running").GetProperty("acceptsDrop").GetBoolean().ShouldBeFalse();
        CardIds(Column(columns, "backlog")).ShouldBe([idle]);
        CardIds(Column(columns, "completed")).ShouldBe([done]);
    }

    /// <summary>Closing a session archives its card and marks its row closed; reopening undoes both. Another owner and an absent session read as 404.</summary>
    [Fact]
    public async Task CloseAndReopen_MoveTheCardAndOwnerScope()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, Alice);
        using var bob = WebAppTestHost.AsPrincipal(factory, Bob);
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using (var foreign = await WebAppTestHost.SendAsync(bob, HttpMethod.Post, $"/sessions/{id}/close", null, ct).ConfigureAwait(true))
        {
            foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var absent = await WebAppTestHost.SendAsync(alice, HttpMethod.Post, "/sessions/absent-id/close", null, ct).ConfigureAwait(true))
        {
            absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        using (var closed = await WebAppTestHost.SendAsync(alice, HttpMethod.Post, $"/sessions/{id}/close", null, ct).ConfigureAwait(true))
        {
            closed.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await WebAppTestHost.ReadJsonAsync(closed, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("closed").GetBoolean().ShouldBeTrue();
            body.RootElement.GetProperty("rowStatus").GetString().ShouldBe("closed", Case.Sensitive);
            body.RootElement.GetProperty("group").GetString().ShouldBe("idle", Case.Sensitive);
        }

        using (var board = await alice.GetAsync(new Uri("/board", UriKind.Relative), ct).ConfigureAwait(true))
        {
            using var document = await WebAppTestHost.ReadJsonAsync(board, ct).ConfigureAwait(true);
            CardIds(Column(document.RootElement.GetProperty("columns").EnumerateArray().ToArray(), "archived")).ShouldBe([id]);
        }

        using (var reopened = await WebAppTestHost.SendAsync(alice, HttpMethod.Post, $"/sessions/{id}/reopen", null, ct).ConfigureAwait(true))
        {
            reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await WebAppTestHost.ReadJsonAsync(reopened, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("closed").GetBoolean().ShouldBeFalse();
            body.RootElement.GetProperty("rowStatus").GetString().ShouldBe("ready", Case.Sensitive);
        }
    }

    /// <summary>A session with no unfinished run cannot be resumed; a closed one must be reopened first.</summary>
    [Fact]
    public async Task Resume_RefusesASessionThatIsNotLost_OrIsClosed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using (var idle = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/resume", null, ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(idle, HttpStatusCode.Conflict, "session_not_lost", ct).ConfigureAwait(true);
        }

        using (var close = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/close", null, ct).ConfigureAwait(true))
        {
            close.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var closed = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/resume", null, ct).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(closed, HttpStatusCode.Conflict, "session_closed", ct).ConfigureAwait(true);
    }

    /// <summary>A session left Running with no run holding its lease is settled as cancelled and then takes a new prompt.</summary>
    [Fact]
    public async Task Resume_SettlesAnOrphanedRun()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var repository = factory.Services.GetRequiredService<ISessionRepository>();
        var session = await repository.GetAsync(id, ct).ConfigureAwait(true) ?? throw new InvalidOperationException("The session was not created.");
        await repository.SaveAsync(session with { Status = SessionStatus.Running, UpdatedAt = TimeProvider.System.GetUtcNow() }, ct).ConfigureAwait(true);

        using var resumed = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/resume", null, ct).ConfigureAwait(true);

        resumed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(resumed, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("rowStatus").GetString().ShouldBe("paused", Case.Sensitive);
        (await SessionClient.ReadSessionAsync(client, id, ct).ConfigureAwait(true)).Status.ShouldBe(SessionStatus.Cancelled);
        using var again = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"again\"}", ct).ConfigureAwait(true);
        again.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    /// <summary>A run that is live holds the lease, so its session is not lost and resume answers 409.</summary>
    [Fact]
    public async Task Resume_RefusesASessionWhoseRunIsLive()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev(WebAppTestHost.HeldAtApproval());
        using var client = factory.CreateClient();
        var id = await WebAppTestHost.StartHeldRunAsync(client, "hold", ct).ConfigureAwait(true);

        using (var response = await WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{id}/resume", null, ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.Conflict, "session_running", ct).ConfigureAwait(true);
        }

        await WebAppTestHost.ReleaseHeldRunAsync(client, id, ct).ConfigureAwait(true);
    }

    /// <summary>Without a task-context source, card details answer available:false with a reason; another owner reads 404.</summary>
    [Fact]
    public async Task CardDetails_WithoutASource_AreUnavailable_AndOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt();
        using var alice = WebAppTestHost.AsPrincipal(factory, Alice);
        using var bob = WebAppTestHost.AsPrincipal(factory, Bob);
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);

        using (var own = await alice.GetAsync(new Uri($"/sessions/{id}/card-details", UriKind.Relative), ct).ConfigureAwait(true))
        {
            own.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await WebAppTestHost.ReadJsonAsync(own, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
            body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
            body.RootElement.TryGetProperty("taskTitle", out _).ShouldBeFalse();
        }

        using var foreign = await bob.GetAsync(new Uri($"/sessions/{id}/card-details", UriKind.Relative), ct).ConfigureAwait(true);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>With a task-context source and a workspace folder, card details carry the task title, Graph identifier and lesson count.</summary>
    [Fact]
    public async Task CardDetails_WithASource_CarryTheSubstrateFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("ompos-card-").FullName;
        try
        {
            var source = new FakeTaskContextSource();
            using var factory = WebAppTestHost.Dev(
                config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = root },
                more: services => services.AddSingleton<ITaskContextSource>(source));
            using var client = factory.CreateClient();
            var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

            using var response = await client.GetAsync(new Uri($"/sessions/{id}/card-details", UriKind.Relative), ct).ConfigureAwait(true);

            using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("available").GetBoolean().ShouldBeTrue();
            body.RootElement.GetProperty("taskTitle").GetString().ShouldBe("Ship the web head", Case.Sensitive);
            body.RootElement.GetProperty("graphId").GetString().ShouldBe("graph-7", Case.Sensitive);
            body.RootElement.GetProperty("lessonsCount").GetInt32().ShouldBe(2);
            source.LastScope.ShouldNotBeNull().SessionId.ShouldBe(id, Case.Sensitive);
            source.LastScope.WorkspaceRoot.ShouldBe(Path.TrimEndingDirectorySeparator(root), Case.Sensitive);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The workspace line names the folder only (never a full path) and reports the plugin check as unavailable when the API has none.</summary>
    [Fact]
    public async Task Workspace_NamesTheFolderOnly_AndReportsTheUnavailablePluginCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var root = Directory.CreateTempSubdirectory("ompos-ws-").FullName;
        try
        {
            using var factory = WebAppTestHost.Dev(config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = root });
            using var client = factory.CreateClient();

            using var response = await client.GetAsync(new Uri("/workspace", UriKind.Relative), ct).ConfigureAwait(true);

            var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
            using var document = JsonDocument.Parse(text);
            var folder = Path.GetFileName(root);
            document.RootElement.GetProperty("name").GetString().ShouldBe(folder, Case.Sensitive);
            document.RootElement.GetProperty("folderLabel").GetString().ShouldBe(folder, Case.Sensitive);
            document.RootElement.GetProperty("pluginCheck").GetProperty("status").GetString().ShouldBe("unavailable", Case.Sensitive);
            text.ShouldNotContain(Path.GetDirectoryName(root)!, Case.Sensitive);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Without a workspace folder the line still answers, with the default name and no folder label.</summary>
    [Fact]
    public async Task Workspace_WithoutAFolder_HasNoFolderLabel()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/workspace", UriKind.Relative), ct).ConfigureAwait(true);

        using var document = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        document.RootElement.GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        document.RootElement.TryGetProperty("folderLabel", out _).ShouldBeFalse();
    }

    private static JsonElement Column(JsonElement[] columns, string key) =>
        columns.Single(column => string.Equals(column.GetProperty("key").GetString(), key, StringComparison.Ordinal));

    private static string[] CardIds(JsonElement element)
    {
        IEnumerable<JsonElement> cards = element.TryGetProperty("columns", out var columns)
            ? columns.EnumerateArray().SelectMany(static column => column.GetProperty("cards").EnumerateArray())
            : element.GetProperty("cards").EnumerateArray();
        return [.. cards.Select(static card => card.GetProperty("id").GetString()!)];
    }
}
