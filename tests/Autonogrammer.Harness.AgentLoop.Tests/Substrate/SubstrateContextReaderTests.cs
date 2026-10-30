using System.Net;
using Autonogrammer.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>Mapping, scope isolation, sanitizing and fault handling of <see cref="SubstrateContextReader"/> against recorded answers.</summary>
public sealed class SubstrateContextReaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SubstrateContextReader Reader(FakeSubstrateServer server, string? repository = SubstrateTestSupport.Repository) =>
        new(server.Gateway, server.Logger, _ => repository);

    [Fact]
    public async Task MapsMemoryByKindIntoSharedMemoryAndLessonsFromTheLessonsScope()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        snapshot.SharedMemory.Select(static note => note.Id).ShouldBe(["mem_g1", "mem_g2", "mem_r1", "mem_r2"]);
        snapshot.SharedMemory[0].Origin.ShouldBe("graph:ut-lq1a2b3c-0f1e2d3c (decision)");
        snapshot.SharedMemory[0].RecordedAt.ShouldBe(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
        snapshot.SharedMemory[2].Origin.ShouldBe("repo:swcstudiospace/OmpOS (fact)");
        snapshot.SharedMemory[3].Origin.ShouldBe("repo:swcstudiospace/OmpOS (handoff)");
        snapshot.Lessons.Select(static note => note.Id).ShouldBe(["les_1", "les_2"]);
        snapshot.Lessons[0].Text.ShouldBe("Leases fence on epoch, so a stale holder cannot release.");
        snapshot.Lessons[0].Origin.ShouldBe("repo:swcstudiospace/OmpOS (lesson)");
        snapshot.Lessons[1].Text.ShouldBe("Heartbeats stop on a lost refusal.");
    }

    [Fact]
    public async Task SkipsExpiredSupersededUnknownKindDigestsAndLessonFactsInSharedMemory()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        var ids = snapshot.SharedMemory.Select(static note => note.Id).Concat(snapshot.Lessons.Select(static note => note.Id)).ToList();
        ids.ShouldNotContain("mem_g3", StringComparer.Ordinal);
        ids.ShouldNotContain("mem_g4", StringComparer.Ordinal);
        ids.ShouldNotContain("mem_g6", StringComparer.Ordinal);
        ids.ShouldNotContain("mem_r3", StringComparer.Ordinal);
        ids.ShouldNotContain("les_3", StringComparer.Ordinal);
    }

    [Fact]
    public async Task NoteTextIsOneLinePlainTextWithoutControlOrBidiCharactersAndCappedAt500()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        snapshot.SharedMemory.Single(static note => string.Equals(note.Id, "mem_g2", StringComparison.Ordinal)).Text.ShouldBe("Line one Line two with a bell and a bidi override and tab.");
        snapshot.SharedMemory.ShouldAllBe(static note => note.Text.All(static c => !char.IsControl(c) && c != '\u202E'));
    }

    [Fact]
    public async Task ALongNoteIsCappedAtFiveHundredCharactersWithoutSplittingASurrogatePair()
    {
        var text = new string('x', 499) + "\U0001F600" + new string('y', 50);
        var page = $$"""[{"id":"mem_long","scope":"graph:{{SubstrateTestSupport.GraphId}}","kind":"fact","text":"{{text}}","created":"2026-10-26T08:00:00.000Z"}]""";
        var gateway = ReaderOver(page, out var rig);
        using (rig)
        {
            var snapshot = await gateway.ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

            var note = snapshot.SharedMemory.Single(static n => string.Equals(n.Id, "mem_long", StringComparison.Ordinal));
            note.Text.Length.ShouldBeLessThanOrEqualTo(SubstrateContextReader.MaxNoteLength);
            note.Text.ShouldBe(new string('x', 499));
        }
    }

    [Fact]
    public async Task EntriesOfAnotherScopeAreDiscardedWithAWarning()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        snapshot.SharedMemory.ShouldNotContain(static note => note.Id == "mem_g5");
        snapshot.Warnings.ShouldContain(static warning => warning.Contains("another scope", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnlyTheSessionsOwnGraphAndRepositoryAreEverQueriedAndEveryQueryNamesItsScope()
    {
        using var server = new FakeSubstrateServer();

        await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        var searches = server.MemorySearches;
        searches.Count.ShouldBe(3);
        searches.Select(static s => s.GetProperty("scope").GetString()).ShouldBe(
            ["graph:" + SubstrateTestSupport.GraphId, "repo:" + SubstrateTestSupport.Repository, "repo:" + SubstrateTestSupport.Repository]);
        searches[1].GetProperty("repo").GetString().ShouldBe(SubstrateTestSupport.Repository);
        searches[2].GetProperty("subject_prefix").GetString().ShouldBe("lesson:");
        searches[0].TryGetProperty("repo", out _).ShouldBeFalse();
        server.Requests.Where(static r => string.Equals(r.Uri.AbsolutePath, "/mcp", StringComparison.Ordinal)).Select(static r => SubstrateTestSupport.ToolCall(r).Arguments.GetRawText())
            .Where(static arguments => arguments.Contains("graph_id", StringComparison.Ordinal))
            .ShouldAllBe(static arguments => arguments.Contains(SubstrateTestSupport.GraphId, StringComparison.Ordinal));
        server.Requests.ShouldAllBe(static r => r.Uri.AbsolutePath == "/mcp");
    }

    [Fact]
    public async Task WithoutAGraphNoGraphScopeOrTaskIsQueried()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(graphId: null), Ct).ConfigureAwait(true);

        server.MemorySearches.Select(static s => s.GetRawText()).ShouldAllBe(static text => !text.Contains("graph:", StringComparison.Ordinal));
        server.Requests.Select(static r => SubstrateTestSupport.ToolCall(r).Name).ShouldAllBe(static name => name == "memory_search");
        snapshot.TaskId.ShouldBeNull();
        snapshot.Title.ShouldBeNull();
    }

    [Fact]
    public async Task WithoutAKnownRepositoryNoRepositoryScopeIsQueriedAndTheSnapshotSaysSo()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server, repository: null).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        server.MemorySearches.ShouldHaveSingleItem().GetRawText().ShouldContain("graph:");
        snapshot.Lessons.ShouldBeEmpty();
        snapshot.Warnings.ShouldContain(static warning => warning.Contains("repository could not be determined", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARepositoryNameThatIsNotOwnerSlashNameIsNeverUsedAsAScope()
    {
        using var server = new FakeSubstrateServer();

        await Reader(server, repository: "owner/name\",\"scope\":\"global").ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        server.MemorySearches.ShouldHaveSingleItem();
        server.MemorySearches.Select(static s => s.GetRawText()).ShouldAllBe(static text => !text.Contains("global", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AGraphIdentifierThatCouldInjectAnotherScopeIsNotQueried()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(graphId: "ut-1\",\"scope\":\"global"), Ct).ConfigureAwait(true);

        server.MemorySearches.Select(static s => s.GetRawText()).ShouldAllBe(static text => !text.Contains("global", StringComparison.Ordinal) && !text.Contains("graph:", StringComparison.Ordinal));
        snapshot.Warnings.ShouldContain(static warning => warning.Contains("Graph identifier is not valid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TaskIdTitleAndStatusComeFromTheNodeThisSessionClaims()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        snapshot.TaskId.ShouldBe("n1");
        snapshot.KanbanStatus.ShouldBe("started");
        snapshot.Title.ShouldBe("Build the agent panel");
        snapshot.Warnings.ShouldNotContain(static warning => warning.Contains("task", StringComparison.OrdinalIgnoreCase));
        var issueCall = server.Requests.Select(static r => string.Equals(r.Uri.AbsolutePath, "/mcp", StringComparison.Ordinal) ? SubstrateTestSupport.ToolCall(r) : default).Single(static c => string.Equals(c.Name, "issue_get", StringComparison.Ordinal));
        issueCall.Arguments.GetRawText().ShouldBe("""{"graph_id":"ut-lq1a2b3c-0f1e2d3c","node_id":"n1"}""");
    }

    [Fact]
    public async Task AGraphWithSeveralNodesAndNoneClaimedByThisSessionChoosesNoTask()
    {
        using var server = new FakeSubstrateServer();

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(acpSessionId: "someone-else"), Ct).ConfigureAwait(true);

        snapshot.TaskId.ShouldBeNull();
        snapshot.Title.ShouldBeNull();
        snapshot.Warnings.ShouldContain(static warning => warning.Contains("none is claimed by this session", StringComparison.Ordinal));
        server.Requests.Select(static r => string.Equals(r.Uri.AbsolutePath, "/mcp", StringComparison.Ordinal) ? SubstrateTestSupport.ToolCall(r).Name : string.Empty).ShouldNotContain("issue_get", StringComparer.Ordinal);
    }

    [Fact]
    public async Task AnUnreachableServerYieldsWarningsAndEmptyListsInsteadOfAnExceptionAndStopsAfterTheFirstFailure()
    {
        using var server = new FakeSubstrateServer { McpStatus = HttpStatusCode.ServiceUnavailable };

        var snapshot = await Reader(server).ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);

        snapshot.SharedMemory.ShouldBeEmpty();
        snapshot.Lessons.ShouldBeEmpty();
        snapshot.TaskId.ShouldBeNull();
        snapshot.Warnings.ShouldNotBeEmpty();
        server.Requests.Count.ShouldBe(1);
        snapshot.Warnings.ShouldAllBe(static warning => !warning.Contains(SubstrateTestSupport.Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AToolRefusalAndAnUnreadableAnswerBecomeWarnings()
    {
        using var refused = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("refused", isError: true)));
        var refusedSnapshot = await new SubstrateContextReader(refused.Gateway, refused.Logger, _ => SubstrateTestSupport.Repository)
            .ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);
        refusedSnapshot.SharedMemory.ShouldBeEmpty();
        refusedSnapshot.Warnings.ShouldContain(static warning => warning.Contains("refused", StringComparison.Ordinal));

        using var garbage = GatewayRig.Create(_ => ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult("not json at all")));
        var garbageSnapshot = await new SubstrateContextReader(garbage.Gateway, garbage.Logger, _ => SubstrateTestSupport.Repository)
            .ReadAsync(SubstrateTestSupport.Scope(), Ct).ConfigureAwait(true);
        garbageSnapshot.SharedMemory.ShouldBeEmpty();
        garbageSnapshot.Warnings.ShouldContain(static warning => warning.Contains("unreadable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ACancelledReadStillCancels()
    {
        using var server = new FakeSubstrateServer();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(true);

        var caught = await Record.ExceptionAsync(() => Reader(server).ReadAsync(SubstrateTestSupport.Scope(), cts.Token)).ConfigureAwait(true);

        caught.ShouldBeAssignableTo<OperationCanceledException>();
    }

    private static SubstrateContextReader ReaderOver(string graphMemoryJson, out GatewayRig rig)
    {
        rig = GatewayRig.Create(request =>
        {
            var (name, arguments) = SubstrateTestSupport.ToolCall(request);
            var scope = arguments.TryGetProperty("scope", out var value) ? value.GetString() : null;
            var text = string.Equals(name, "memory_search", StringComparison.Ordinal) && scope is not null && scope.StartsWith("graph:", StringComparison.Ordinal) ? graphMemoryJson : "[]";
            return ScriptedHandler.Text(HttpStatusCode.OK, SubstrateTestSupport.RpcResult(text));
        });
        return new SubstrateContextReader(rig.Gateway, rig.Logger, _ => SubstrateTestSupport.Repository);
    }
}
