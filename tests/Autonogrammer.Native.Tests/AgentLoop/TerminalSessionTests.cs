using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Tests.RunControls;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// Retired terminal creation: there is no terminal creation path on the panel, the hosts or the head. Chat sessions are the only
/// sessions created, a persisted terminal row still lists as retired, and opening it creates no starter, transport or engine.
/// </summary>
public sealed class TerminalSessionTests
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>Creating sessions creates chats only: both records are Kind Chat with no terminal in the title, the catalog holds no terminal, and both get the owner-only private directories.</summary>
    [Fact]
    public async Task CreatingASessionCreatesAChatNotATerminal()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var first = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var second = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);

        var registry = head.Host.Registry!;
        registry.TryGet(first, out var firstRecord).ShouldBeTrue();
        registry.TryGet(second, out var secondRecord).ShouldBeTrue();
        firstRecord!.Kind.ShouldBe(SessionKind.Chat);
        firstRecord.Title.ShouldNotContain("terminal", Case.Insensitive);
        secondRecord!.Kind.ShouldBe(SessionKind.Chat);
        head.Host.Catalog.Snapshot().Count(static summary => summary.Kind == SessionKind.Terminal).ShouldBe(0);
        foreach (var record in new[] { firstRecord, secondRecord })
        {
            foreach (var directory in SessionDirectories(record.StateDirectory))
            {
                Directory.Exists(directory).ShouldBeTrue(directory);
                if (!OperatingSystem.IsWindows())
                {
                    File.GetUnixFileMode(directory).ShouldBe(OwnerOnly, directory);
                }
            }
        }
    }

    /// <summary>New agent creates a chat, not a terminal, and the panel and host expose no terminal creation surface.</summary>
    [Fact]
    public async Task NewAgentCreatesAChatNotATerminal()
    {
        typeof(AgentPanelViewModel).GetMethod("NewTerminalAsync").ShouldBeNull("the panel has no terminal creation path");
        typeof(AgentPanelViewModel).GetProperty("NewTerminalCommand").ShouldBeNull("the panel has no terminal command");
        typeof(IAgentPanelHost).GetMethods().Select(static method => method.Name).ShouldNotContain("CreateTerminalSessionAsync", StringComparer.Ordinal);

        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                await head.Panel.NewSessionAsync(ct).ConfigureAwait(true);

                var selected = head.Panel.Selected.ShouldNotBeNull();
                selected.Row.IsTerminal.ShouldBeFalse();
                selected.Row.Summary.Kind.ShouldBe(SessionKind.Chat);
                head.Host.Registry!.TryGet(selected.Id, out var record).ShouldBeTrue();
                record!.Kind.ShouldBe(SessionKind.Chat);
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A persisted terminal row lists with the retired signal: terminal kind and a terminal accessible name, while a chat row does not.</summary>
    [Fact]
    public async Task APersistedTerminalRowListsAsRetired()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);

        var terminal = head.Host.Registry!.Create("omp terminal 1", tree.Workspace, SessionKind.Terminal).Id;
        var chat = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Catalog.Refresh();

        var terminalRow = head.Catalog.Rows.Single(row => string.Equals(row.Id, terminal, StringComparison.Ordinal));
        var chatRow = head.Catalog.Rows.Single(row => string.Equals(row.Id, chat, StringComparison.Ordinal));

        terminalRow.IsTerminal.ShouldBeTrue();
        terminalRow.Summary.Kind.ShouldBe(SessionKind.Terminal);
        terminalRow.AccessibleName.ShouldContain(", terminal", Case.Sensitive);
        chatRow.IsTerminal.ShouldBeFalse();
        chatRow.AccessibleName.ShouldNotContain("terminal", Case.Insensitive);
    }

    /// <summary>Opening a persisted terminal row creates no starter, transport or engine: its runtime is retired and runs nothing.</summary>
    [Fact]
    public async Task OpeningAPersistedTerminalCreatesNoStarterOrEngine()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var head = await AgentHead.CreateAsync(Options(tree, new ScriptedOmp())).ConfigureAwait(true);
            try
            {
                await head.InitializeAsync(ct).ConfigureAwait(true);
                var terminal = head.Host.Registry!.Create("omp terminal 1", tree.Workspace, SessionKind.Terminal).Id;
                head.Catalog.Refresh();

                head.Panel.SelectSession(terminal).ShouldBeTrue();
                await RunControlsHarness.UntilAsync(
                    () => string.Equals(head.Panel.Selected?.Id, terminal, StringComparison.Ordinal),
                    "the persisted terminal to be selected",
                    ct).ConfigureAwait(true);

                var runtime = AgentSessionRuntime.For(head.Panel.Selected.ShouldNotBeNull()).ShouldNotBeNull();
                runtime.IsRetiredTerminal.ShouldBeTrue();
                runtime.Kind.ShouldBe(SessionKind.Terminal, "the persisted kind is kept for listing");
                runtime.Runs.IsRunActive.ShouldBeFalse("opening a retired terminal starts no engine");
            }
            finally
            {
                await head.DisposeAsync().ConfigureAwait(true);
            }
        }, ct).ConfigureAwait(true);
    }

    private static string[] SessionDirectories(string sessionDirectory)
    {
        var steer = SessionStateLayout.SteerDirectory(sessionDirectory);
        return
        [
            sessionDirectory,
            steer,
            Path.Combine(steer, "inbox"),
            Path.Combine(steer, "acks"),
            Path.Combine(steer, "done"),
            Path.GetDirectoryName(SessionStateLayout.ConnectionFile(sessionDirectory))!,
            SessionStateLayout.SwarmDirectory(sessionDirectory),
        ];
    }
}
