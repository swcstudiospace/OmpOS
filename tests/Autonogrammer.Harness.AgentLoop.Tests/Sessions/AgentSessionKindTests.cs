using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Sessions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Sessions;

/// <summary>
/// Verifies the session kind in the registry: the default, persistence across a reopen, the summary, a state file written before
/// sessions had a kind, and that a kind this build does not know never loses the session.
/// </summary>
public sealed class AgentSessionKindTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void ASessionCreatedWithoutAKindIsAChat()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        var record = registry.Create("Fix login", "/work/app");

        record.Kind.ShouldBe(SessionKind.Chat);
        record.ToSummary().Kind.ShouldBe(SessionKind.Chat);
    }

    [Fact]
    public async Task BothKindsSurviveAReopenAndReachTheSummaries()
    {
        using var directory = new SessionTestDirectory();
        var clock = new SessionTestClock(Start);
        var registry = new AgentSessionRegistry(directory.StatePath, clock);
        var chat = registry.Create("Chat", "/work/app");
        clock.Advance(TimeSpan.FromSeconds(1));
        var terminal = registry.Create("Terminal", "/work/app", SessionKind.Terminal);

        var asynchronous = await AgentSessionRegistry.OpenAsync(directory.StatePath, clock, null, null, Token).ConfigureAwait(true);
        var synchronous = new AgentSessionRegistry(directory.StatePath, clock);

        terminal.Kind.ShouldBe(SessionKind.Terminal);
        foreach (var reopened in new[] { asynchronous, synchronous })
        {
            reopened.TryGet(chat.Id, out var loadedChat).ShouldBeTrue();
            reopened.TryGet(terminal.Id, out var loadedTerminal).ShouldBeTrue();
            loadedChat!.Kind.ShouldBe(SessionKind.Chat);
            loadedTerminal!.Kind.ShouldBe(SessionKind.Terminal);
            reopened.Snapshot().ToDictionary(summary => summary.Id, summary => summary.Kind, StringComparer.Ordinal)
                .ShouldBe(new Dictionary<string, SessionKind>(StringComparer.Ordinal)
                {
                    [chat.Id] = SessionKind.Chat,
                    [terminal.Id] = SessionKind.Terminal,
                }, ignoreOrder: true);
        }
    }

    [Fact]
    public async Task TheKindIsWrittenByNameAndTheSchemaVersionStaysOne()
    {
        using var directory = new SessionTestDirectory();
        _ = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start)).Create("Terminal", null, SessionKind.Terminal);

        var text = await File.ReadAllTextAsync(directory.StatePath, Token).ConfigureAwait(true);

        text.ShouldContain("\"kind\": \"Terminal\"", Case.Sensitive);
        text.ShouldContain("\"schemaVersion\": 1", Case.Sensitive);
    }

    [Fact]
    public async Task AFileWrittenBeforeSessionsHadAKindLoadsEverySessionAsAChatWithoutQuarantine()
    {
        using var directory = new SessionTestDirectory();
        await File.WriteAllTextAsync(
            directory.StatePath,
            """
            {"schemaVersion":1,"sessions":[
              {"id":"s-old","title":"Old","lifecycle":"Paused","createdAt":"2026-10-26T07:00:00+00:00","workspaceRoot":"/work/app"}
            ]}
            """,
            Token).ConfigureAwait(true);

        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        var record = registry.SnapshotRecords().ShouldHaveSingleItem();
        record.Id.ShouldBe("s-old");
        record.Kind.ShouldBe(SessionKind.Chat);
        record.Lifecycle.ShouldBe(SessionLifecycle.Paused);
        record.WorkspaceRoot.ShouldBe("/work/app");
        Directory.GetFiles(directory.Path, "*.quarantined-*").ShouldBeEmpty();

        registry.SetLifecycle("s-old", SessionLifecycle.Running).ShouldBeTrue();
        (await File.ReadAllTextAsync(directory.StatePath, Token).ConfigureAwait(true)).ShouldContain("\"kind\": \"Chat\"", Case.Sensitive);
    }

    [Theory]
    [InlineData("\"Terminal\"", SessionKind.Terminal)]
    [InlineData("\"terminal\"", SessionKind.Terminal)]
    [InlineData("\"Chat\"", SessionKind.Chat)]
    [InlineData("\"Hologram\"", SessionKind.Chat)]
    [InlineData("\"1\"", SessionKind.Chat)]
    [InlineData("\"\"", SessionKind.Chat)]
    [InlineData("null", SessionKind.Chat)]
    public async Task AKindNameIsReadWithoutRegardToCaseAndAnythingElseIsAChatThatKeepsTheSession(string json, SessionKind expected)
    {
        using var directory = new SessionTestDirectory();
        await File.WriteAllTextAsync(
            directory.StatePath,
            "{\"schemaVersion\":1,\"sessions\":[{\"id\":\"s-1\",\"title\":\"One\",\"lifecycle\":\"Paused\",\"kind\":" + json + "}]}",
            Token).ConfigureAwait(true);

        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        var record = registry.SnapshotRecords().ShouldHaveSingleItem();
        record.Kind.ShouldBe(expected);
        record.Title.ShouldBe("One");
        Directory.GetFiles(directory.Path, "*.quarantined-*").ShouldBeEmpty();
    }

    [Fact]
    public void OtherChangesKeepTheKind()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var terminal = registry.Create("Terminal", null, SessionKind.Terminal);

        registry.Rename(terminal.Id, "Renamed").ShouldBeTrue();
        registry.SetLifecycle(terminal.Id, SessionLifecycle.Running).ShouldBeTrue();
        registry.SetAcpSession(terminal.Id, "acp-1", "native-1").ShouldBeTrue();
        registry.Touch(terminal.Id).ShouldBeTrue();

        registry.TryGet(terminal.Id, out var current).ShouldBeTrue();
        current!.Kind.ShouldBe(SessionKind.Terminal);
        registry.Snapshot().ShouldHaveSingleItem().Kind.ShouldBe(SessionKind.Terminal);
    }

    [Fact]
    public void AnUndefinedKindIsRejectedAndNothingIsCreated()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));

        Should.Throw<ArgumentOutOfRangeException>(() => registry.Create("Bad", null, (SessionKind)42));

        registry.Snapshot().ShouldBeEmpty();
    }

    [Fact]
    public void TheRecordDescriptionNamesTheKindAndStillRedactsTheResumeReference()
    {
        using var directory = new SessionTestDirectory();
        var registry = new AgentSessionRegistry(directory.StatePath, new SessionTestClock(Start));
        var terminal = registry.Create("Terminal", null, SessionKind.Terminal);
        registry.SetAcpSession(terminal.Id, "acp-1", "native-ref-SECRET");
        registry.TryGet(terminal.Id, out var current).ShouldBeTrue();

        var text = current!.ToString();

        text.ShouldContain("Kind = Terminal", Case.Sensitive);
        text.ShouldNotContain("SECRET", Case.Sensitive);
    }
}
