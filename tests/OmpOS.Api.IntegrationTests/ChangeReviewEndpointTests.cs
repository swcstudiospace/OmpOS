using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Api.Services;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// The change-review routes. The list merges the edits a session's runs reported into one net change per file; accept and reject take the
/// URL-encoded path, write only inside the configured workspace folder and only when the file still holds the expected text, and never follow a
/// path out of the folder. Every route is owner-scoped.
/// </summary>
public sealed class ChangeReviewEndpointTests : IDisposable
{
    private static readonly DateTimeOffset Stamp = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private readonly string _root = Directory.CreateTempSubdirectory("ompos-changes-").FullName;
    private int _call;

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>Several edits of one file are one change from the first before text to the last after text; an edit that undoes itself is not a change.</summary>
    [Fact]
    public async Task List_MergesEditsPerFile_AndDropsNetNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/a.txt", "one", "two", ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/a.txt", "two", "three", ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/new.txt", null, "fresh", ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/undo.txt", "x", "y", ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/undo.txt", "y", "x", ct).ConfigureAwait(true);

        using var response = await client.GetAsync(new Uri($"/sessions/{id}/changes", UriKind.Relative), ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("workspaceAvailable").GetBoolean().ShouldBeTrue();
        var changes = body.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        changes.Select(static change => change.GetProperty("path").GetString()).ShouldBe(["src/a.txt", "src/new.txt"]);
        changes[0].GetProperty("kind").GetString().ShouldBe("modified", Case.Sensitive);
        changes[0].GetProperty("status").GetString().ShouldBe("pending", Case.Sensitive);
        changes[0].GetProperty("oldText").GetString().ShouldBe("one", Case.Sensitive);
        changes[0].GetProperty("newText").GetString().ShouldBe("three", Case.Sensitive);
        changes[1].GetProperty("kind").GetString().ShouldBe("added", Case.Sensitive);
        changes[1].TryGetProperty("oldText", out _).ShouldBeFalse();
    }

    /// <summary>Accept writes the edited text into the workspace file and marks the change accepted until a later edit changes the text.</summary>
    [Fact]
    public async Task Accept_WritesTheEditedText_AndALaterEditMakesItPendingAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var file = Write("src/a.txt", "before");
        await EditAsync(factory, id, "src/a.txt", "before", "after", ct).ConfigureAwait(true);

        using (var response = await PostAsync(client, id, "src/a.txt", "accept", ct).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(true);
            body.RootElement.GetProperty("status").GetString().ShouldBe("accepted", Case.Sensitive);
            body.RootElement.GetProperty("applied").GetBoolean().ShouldBeTrue();
        }

        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("after", Case.Sensitive);
        (await StatusAsync(client, id, "src/a.txt", ct).ConfigureAwait(true)).ShouldBe("accepted");

        await EditAsync(factory, id, "src/a.txt", "after", "again", ct).ConfigureAwait(true);
        (await StatusAsync(client, id, "src/a.txt", ct).ConfigureAwait(true)).ShouldBe("pending");
    }

    /// <summary>Reject restores the text before the edit, and deletes a file the edit created.</summary>
    [Fact]
    public async Task Reject_RestoresTheOldText_AndDeletesACreatedFile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var edited = Write("src/a.txt", "after");
        var created = Write("src/new.txt", "fresh");
        await EditAsync(factory, id, "src/a.txt", "before", "after", ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/new.txt", null, "fresh", ct).ConfigureAwait(true);

        using (var response = await PostAsync(client, id, "src/a.txt", "reject", ct).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var response = await PostAsync(client, id, "src/new.txt", "reject", ct).ConfigureAwait(true))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await File.ReadAllTextAsync(edited, ct).ConfigureAwait(true)).ShouldBe("before", Case.Sensitive);
        File.Exists(created).ShouldBeFalse();
        (await StatusAsync(client, id, "src/a.txt", ct).ConfigureAwait(true)).ShouldBe("rejected");
    }

    /// <summary>A file that holds neither the before nor the after text is left alone and the change stays pending.</summary>
    [Fact]
    public async Task Decision_OnAFileThatChangedMeanwhile_IsAConflict_AndWritesNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var file = Write("src/a.txt", "the user edited this");
        await EditAsync(factory, id, "src/a.txt", "before", "after", ct).ConfigureAwait(true);

        using var accept = await PostAsync(client, id, "src/a.txt", "accept", ct).ConfigureAwait(true);
        using var reject = await PostAsync(client, id, "src/a.txt", "reject", ct).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(accept, HttpStatusCode.Conflict, "workspace_conflict", ct).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(reject, HttpStatusCode.Conflict, "workspace_conflict", ct).ConfigureAwait(true);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("the user edited this", Case.Sensitive);
        (await StatusAsync(client, id, "src/a.txt", ct).ConfigureAwait(true)).ShouldBe("pending");
    }

    /// <summary>A request path that climbs out of the folder, or holds a backslash, is 400 before any file is touched.</summary>
    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("src/../../escape.txt")]
    [InlineData("src\\a.txt")]
    public async Task RequestPath_Traversal_IsBadRequest(string path)
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);

        using var accept = await PostAsync(client, id, path, "accept", ct).ConfigureAwait(true);
        using var reject = await PostAsync(client, id, path, "reject", ct).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(accept, HttpStatusCode.BadRequest, "invalid_change_path", ct).ConfigureAwait(true);
        await WebAppTestHost.ShouldBeProblemAsync(reject, HttpStatusCode.BadRequest, "invalid_change_path", ct).ConfigureAwait(true);
    }

    /// <summary>A path the harness reported that resolves outside the workspace folder is refused and nothing is created there.</summary>
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("src/../../escape.txt")]
    public async Task ReportedPath_OutsideTheFolder_IsRefusedByTheApplier(string reported)
    {
        var ct = TestContext.Current.CancellationToken;
        var outside = Path.Combine(Path.GetDirectoryName(_root)!, "escape.txt");

        var accept = await WorkspaceChangeApplier.ApplyAsync(_root, new ReportedChange(reported, null, "pwned"), accept: true, ct).ConfigureAwait(true);
        var reject = await WorkspaceChangeApplier.ApplyAsync(_root, new ReportedChange(reported, "old", "pwned"), accept: false, ct).ConfigureAwait(true);

        accept.Kind.ShouldBe(ApplyKind.Outside);
        reject.Kind.ShouldBe(ApplyKind.Outside);
        File.Exists(outside).ShouldBeFalse();
    }

    /// <summary>An absolute reported path outside the folder answers 422 through the API and writes nothing.</summary>
    [Fact]
    public async Task ReportedAbsolutePath_OutsideTheFolder_Is422()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = Factory();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        var outside = Path.Combine(Path.GetTempPath(), $"ompos-outside-{Guid.NewGuid():N}.txt");
        await EditAsync(factory, id, outside, null, "pwned", ct).ConfigureAwait(true);

        using var response = await PostAsync(client, id, outside, "accept", ct).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.UnprocessableEntity, "path_outside_workspace", ct).ConfigureAwait(true);
        File.Exists(outside).ShouldBeFalse();
    }

    /// <summary>A directory link inside the folder that points outside it is not followed.</summary>
    [Fact]
    public async Task ReportedPath_ThroughALink_IsRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = Directory.CreateTempSubdirectory("ompos-link-target-").FullName;
        try
        {
            try
            {
                _ = Directory.CreateSymbolicLink(Path.Combine(_root, "link"), target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                Assert.Skip("This host cannot create symbolic links.");
            }

            var outcome = await WorkspaceChangeApplier.ApplyAsync(_root, new ReportedChange("link/x.txt", null, "pwned"), accept: true, ct).ConfigureAwait(true);

            outcome.Kind.ShouldBe(ApplyKind.Outside);
            File.Exists(Path.Combine(target, "x.txt")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(target, recursive: true);
        }
    }

    /// <summary>A path with no reported edit is 404, and without a workspace folder a decision is recorded and no file is written.</summary>
    [Fact]
    public async Task UnknownChange_Is404_AndWithoutAFolderADecisionIsOnlyRecorded()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Dev();
        using var client = factory.CreateClient();
        var id = await SessionClient.CreateSessionAsync(client, ct).ConfigureAwait(true);
        await EditAsync(factory, id, "src/a.txt", "before", "after", ct).ConfigureAwait(true);

        using (var unknown = await PostAsync(client, id, "src/other.txt", "accept", ct).ConfigureAwait(true))
        {
            await WebAppTestHost.ShouldBeProblemAsync(unknown, HttpStatusCode.NotFound, "change_not_found", ct).ConfigureAwait(true);
        }

        using var recorded = await PostAsync(client, id, "src/a.txt", "accept", ct).ConfigureAwait(true);
        recorded.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(recorded, ct).ConfigureAwait(true);
        body.RootElement.GetProperty("applied").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("reason").GetString().ShouldNotBeNullOrWhiteSpace();
        (await StatusAsync(client, id, "src/a.txt", ct).ConfigureAwait(true)).ShouldBe("accepted");
        using var list = await client.GetAsync(new Uri($"/sessions/{id}/changes", UriKind.Relative), ct).ConfigureAwait(true);
        using var changes = await WebAppTestHost.ReadJsonAsync(list, ct).ConfigureAwait(true);
        changes.RootElement.GetProperty("workspaceAvailable").GetBoolean().ShouldBeFalse();
    }

    /// <summary>Another owner's session and an absent session answer 404 on the list and on both decisions, and the file is untouched.</summary>
    [Fact]
    public async Task ChangeRoutes_AreOwnerScoped()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = WebAppTestHost.Jwt(config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = _root });
        using var alice = WebAppTestHost.AsPrincipal(factory, "alice");
        using var bob = WebAppTestHost.AsPrincipal(factory, "bob");
        var id = await SessionClient.CreateSessionAsync(alice, ct).ConfigureAwait(true);
        var file = Write("src/a.txt", "before");
        await EditAsync(factory, id, "src/a.txt", "before", "after", ct).ConfigureAwait(true);

        using var list = await bob.GetAsync(new Uri($"/sessions/{id}/changes", UriKind.Relative), ct).ConfigureAwait(true);
        using var accept = await PostAsync(bob, id, "src/a.txt", "accept", ct).ConfigureAwait(true);
        using var reject = await PostAsync(bob, id, "src/a.txt", "reject", ct).ConfigureAwait(true);
        using var absent = await PostAsync(bob, "absent", "src/a.txt", "accept", ct).ConfigureAwait(true);

        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        accept.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        reject.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        absent.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await File.ReadAllTextAsync(file, ct).ConfigureAwait(true)).ShouldBe("before", Case.Sensitive);
    }

    private OmpApiFactory Factory() =>
        WebAppTestHost.Dev(config: new Dictionary<string, string?>(StringComparer.Ordinal) { ["Api:Workspace:Root"] = _root });

    private string Write(string relative, string text)
    {
        var full = Path.Combine(_root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    private async Task EditAsync(OmpApiFactory factory, string sessionId, string path, string? oldText, string newText, CancellationToken ct)
    {
        var callId = $"call-{Interlocked.Increment(ref _call)}";
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = sessionId,
            CorrelationId = $"{sessionId}/turn-1",
            Timestamp = Stamp,
            Payload = new UiBlockPayload { Block = new FileEditBlock { Id = $"{callId}-edit-0", CallId = callId, Path = path, OldText = oldText, NewText = newText } },
        };
        await factory.Services.GetRequiredService<ISessionRepository>().AppendAsync(sessionId, evt, ct).ConfigureAwait(false);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string sessionId, string path, string action, CancellationToken ct) =>
        WebAppTestHost.SendAsync(client, HttpMethod.Post, $"/sessions/{sessionId}/changes/{Uri.EscapeDataString(path)}/{action}", null, ct);

    private static async Task<string?> StatusAsync(HttpClient client, string sessionId, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(new Uri($"/sessions/{sessionId}/changes", UriKind.Relative), ct).ConfigureAwait(false);
        using var body = await WebAppTestHost.ReadJsonAsync(response, ct).ConfigureAwait(false);
        return body.RootElement.GetProperty("changes").EnumerateArray()
            .Single(change => string.Equals(change.GetProperty("path").GetString(), path, StringComparison.Ordinal))
            .GetProperty("status").GetString();
    }
}
