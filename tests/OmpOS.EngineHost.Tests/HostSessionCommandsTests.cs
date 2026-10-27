using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.EngineHost.Tests;

/// <summary>
/// The slash commands of <see cref="InProcessEngineHost"/>: they stay valid while the host binds and replaces sessions, are empty before a
/// session is bound and for a session that advertises none, forward to the bound session, and follow a session that replaces a faulted one.
/// </summary>
public sealed class HostSessionCommandsTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    /// <summary>Before a session is bound the host advertises no command.</summary>
    [Fact]
    public async Task BeforeASessionIsBoundThereAreNoCommands()
    {
        var host = new InProcessEngineHost(() => new CommandSession(), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);

        host.SessionCommands.Commands.ShouldBeEmpty();
    }

    /// <summary>Once connected the host reports what the session advertised (and raises the change event for the binding), and follows the session's later lists.</summary>
    [Fact]
    public async Task AConnectedHostReportsAndFollowsTheSessionsCommands()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new CommandSession();
        await using var sessionScope = session.ConfigureAwait(true);
        session.Set(new OmpCommand("model", "Show current model selection", null));
        var host = new InProcessEngineHost(() => session, Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        var changes = 0;
        host.SessionCommands.CommandsChanged += (_, _) => changes++;

        await host.ConnectAsync(ct).ConfigureAwait(true);

        changes.ShouldBe(1, "binding the session announces its commands");
        host.SessionCommands.Commands.ShouldBe([new OmpCommand("model", "Show current model selection", null)]);

        session.Set(new OmpCommand("fast", "Toggle fast mode", "[on|off]"), new OmpCommand("slow", null, null));

        changes.ShouldBe(2, "the session's own report reaches the host's subscribers");
        host.SessionCommands.Commands.Select(static command => command.Name).ShouldBe(["fast", "slow"]);
    }

    /// <summary>A session the host replaces after a fault takes over: the host's commands follow the new session, and the old one is no longer followed.</summary>
    [Fact]
    public async Task AReplacedSessionIsFollowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<CommandSession>();
        var host = new InProcessEngineHost(
            () =>
            {
                var next = new CommandSession();
                next.Set(new OmpCommand($"from-session-{sessions.Count + 1}", null, null));
                sessions.Add(next);
                return next;
            },
            Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        host.SessionCommands.Commands.Single().Name.ShouldBe("from-session-1");
        var changes = 0;
        host.SessionCommands.CommandsChanged += (_, _) => changes++;

        sessions[0].State = HarnessState.Faulted;
        await host.ConnectAsync(ct).ConfigureAwait(true);

        sessions.Count.ShouldBe(2);
        host.SessionCommands.Commands.Single().Name.ShouldBe("from-session-2");
        var afterReplacement = changes;

        sessions[0].Set(new OmpCommand("stale", null, null));

        changes.ShouldBe(afterReplacement, "the replaced session is no longer followed");
        host.SessionCommands.Commands.Single().Name.ShouldBe("from-session-2");
    }

    /// <summary>A session that advertises no commands leaves the host with an empty list.</summary>
    [Fact]
    public async Task ASessionWithoutCommandsOffersNone()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new PlainSession(), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);

        await host.ConnectAsync(ct).ConfigureAwait(true);

        host.SessionCommands.Commands.ShouldBeEmpty();
    }

    /// <summary>A disposed host no longer follows its session.</summary>
    [Fact]
    public async Task ADisposedHostFollowsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new CommandSession();
        await using var sessionScope = session.ConfigureAwait(true);
        session.Set(new OmpCommand("model", null, null));
        var host = new InProcessEngineHost(() => session, Descriptor);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        var changes = 0;
        host.SessionCommands.CommandsChanged += (_, _) => changes++;

        await host.DisposeAsync().ConfigureAwait(true);
        session.Set(new OmpCommand("late", null, null));

        host.SessionCommands.Commands.ShouldBeEmpty();
        changes.ShouldBe(0);
    }

    /// <summary>A session that advertises commands, scripted by the test.</summary>
    private sealed class CommandSession : IOmpSession, IOmpSessionCommands
    {
        public event EventHandler? CommandsChanged;

        public IReadOnlyList<OmpCommand> Commands { get; private set; } = [];

        public HarnessState State { get; set; } = HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Set(params OmpCommand[] commands)
        {
            Commands = commands;
            CommandsChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>A session that advertises nothing.</summary>
    private sealed class PlainSession : IOmpSession
    {
        public HarnessState State => HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new CompletedEvent("end_turn");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
