using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// The commands <see cref="AcpAgentSession"/> learns from <c>available_commands_update</c> notifications: the typed list, replace semantics, the
/// tolerant parse of odd shapes, the change notification and the updates that are ignored.
/// </summary>
public sealed class AcpAvailableCommandsTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private static readonly AcpClientOptions Options = new() { WorkingDirectory = AcpFixtures.CapturedCwd, AuthMethodId = "agent" };

    /// <summary>The list omp 18.12.6 sends right after session/new is parsed in wire order with each description and input hint; a command without input has no hint.</summary>
    [Fact]
    public async Task TheListOmpSendsAfterSessionNewIsParsedWithDescriptionsAndHints()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        session.CommandsChanged += (_, _) => changed.Release();

        transport.Deliver(Update(
            ScriptedOmpPeer.SessionId,
            """
            [{"name":"security","description":"Plan, run, inspect, import, and compare OMP-native security scans","input":{"hint":"<plan|scan|status>"}},
             {"name":"model","description":"Show current model selection"},
             {"name":"fast","description":"Toggle fast mode","input":{"hint":"[on|ultra|off|status]"}},
             {"name":"skill:review","description":"Review the change","input":{"hint":"arguments"}}]
            """));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        session.AvailableCommands.ShouldBe(
        [
            new AcpAvailableCommand("security", "Plan, run, inspect, import, and compare OMP-native security scans", "<plan|scan|status>"),
            new AcpAvailableCommand("model", "Show current model selection", null),
            new AcpAvailableCommand("fast", "Toggle fast mode", "[on|ultra|off|status]"),
            new AcpAvailableCommand("skill:review", "Review the change", "arguments"),
        ]);
        var commands = ((IOmpSessionCommands)session).Commands;
        commands.Select(static command => command.Name).ShouldBe(["security", "model", "fast", "skill:review"]);
        commands[2].ShouldBe(new OmpCommand("fast", "Toggle fast mode", "[on|ultra|off|status]"));
    }

    /// <summary>Every update replaces the list as a whole, an empty array empties it, and each real change is announced once.</summary>
    [Fact]
    public async Task EveryUpdateReplacesTheListAndOnlyARealChangeIsAnnounced()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        var changes = 0;
        session.CommandsChanged += (_, _) =>
        {
            Interlocked.Increment(ref changes);
            changed.Release();
        };
        var first = Update(ScriptedOmpPeer.SessionId, """[{"name":"model","description":"Show current model selection"},{"name":"fast"}]""");

        transport.Deliver(first);
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        session.AvailableCommands.Select(static command => command.Name).ShouldBe(["model", "fast"]);

        // Updates are handled in wire order: when the replacement below was seen, the repeat before it was handled too and announced nothing.
        transport.Deliver(first);
        transport.Deliver(Update(ScriptedOmpPeer.SessionId, """[{"name":"switch","input":{"hint":"[model]"}}]"""));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);
        session.AvailableCommands.ShouldBe([new AcpAvailableCommand("switch", null, "[model]")]);

        transport.Deliver(Update(ScriptedOmpPeer.SessionId, "[]"));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        session.AvailableCommands.ShouldBeEmpty();
        changes.ShouldBe(3);
    }

    /// <summary>An entry of the wrong shape, an entry without a name and a repeated name never fail the update: the readable entries are kept, the repeat keeps its first entry.</summary>
    [Fact]
    public async Task OddEntriesAreSkippedAndNeverThrow()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        session.CommandsChanged += (_, _) => changed.Release();

        transport.Deliver(Update(
            ScriptedOmpPeer.SessionId,
            """
            ["text", 7, null, {}, {"name":""}, {"name":3}, {"description":"no name"},
             {"name":"model","description":7,"input":"not an object"},
             {"name":"fast","description":"first","input":{"hint":""}},
             {"name":"fast","description":"second","input":{"hint":"[on]"}},
             {"name":"slow","input":{"hint":7}}]
            """));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        session.AvailableCommands.ShouldBe(
        [
            new AcpAvailableCommand("model", null, null),
            new AcpAvailableCommand("fast", "first", null),
            new AcpAvailableCommand("slow", null, null),
        ]);
    }

    /// <summary>An update without an availableCommands array, one of another session and one whose availableCommands is not an array leave the list as it was.</summary>
    [Fact]
    public async Task UpdatesWithoutAUsableListOrForAnotherSessionAreIgnored()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        session.CommandsChanged += (_, _) => changed.Release();
        transport.Deliver(Update(ScriptedOmpPeer.SessionId, """[{"name":"model"}]"""));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        transport.Deliver(Update("another-session", """[{"name":"foreign"}]"""));
        transport.Deliver(Frame(ScriptedOmpPeer.SessionId, """{"sessionUpdate":"available_commands_update"}"""));
        transport.Deliver(Frame(ScriptedOmpPeer.SessionId, """{"sessionUpdate":"available_commands_update","availableCommands":"model"}"""));
        transport.Deliver(Frame(ScriptedOmpPeer.SessionId, """{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"hi"},"availableCommands":[{"name":"leak"}]}"""));

        // The next real change proves that the four frames above were handled first, and that they changed nothing.
        transport.Deliver(Update(ScriptedOmpPeer.SessionId, """[{"name":"fast"}]"""));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        session.AvailableCommands.ShouldBe([new AcpAvailableCommand("fast", null, null)]);
        changed.CurrentCount.ShouldBe(0);
    }

    /// <summary>A subscriber that throws neither fails the session nor hides the change from the subscribers after it.</summary>
    [Fact]
    public async Task AThrowingSubscriberDoesNotHideTheChangeFromTheOthers()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        await session.CreateAsync(ct).ConfigureAwait(true);
        using var changed = new SemaphoreSlim(0);
        session.CommandsChanged += static (_, _) => throw new InvalidOperationException("A subscriber failed.");
        session.CommandsChanged += (_, _) => changed.Release();

        transport.Deliver(Update(ScriptedOmpPeer.SessionId, """[{"name":"model"}]"""));
        await changed.WaitAsync(WaitLimit, ct).ConfigureAwait(true);

        session.AvailableCommands.Select(static command => command.Name).ShouldBe(["model"]);
    }

    /// <summary>Before the session is open and before omp advertised anything, the list is empty.</summary>
    [Fact]
    public async Task TheListIsEmptyUntilOmpAdvertisesIt()
    {
        var ct = TestContext.Current.CancellationToken;
        var transport = new ScriptedAcpTransport(new ScriptedOmpPeer().Respond);
        await using var transportScope = transport.ConfigureAwait(true);
        var session = new AcpAgentSession(transport, Options);
        await using var sessionScope = session.ConfigureAwait(true);
        ((IOmpSessionCommands)session).Commands.ShouldBeEmpty();

        await session.CreateAsync(ct).ConfigureAwait(true);

        ((IOmpSessionCommands)session).Commands.ShouldBeEmpty();
        session.AvailableCommands.ShouldBeEmpty();
    }

    private static byte[] Update(string sessionId, string availableCommands) =>
        Frame(sessionId, $$"""{"sessionUpdate":"available_commands_update","availableCommands":{{availableCommands}}}""");

    private static byte[] Frame(string sessionId, string update) =>
        AcpFixtures.Utf8($$$"""{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"{{{sessionId}}}","update":{{{update}}}}}""");
}
