using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.EngineHost.Tests;

/// <summary>
/// The session options and the live steering of <see cref="InProcessEngineHost"/>: both stay valid while the host binds and replaces
/// sessions, report nothing before a session is bound, forward to the bound session, and follow a session that replaces a faulted one.
/// </summary>
public sealed class HostSessionControlTests
{
    private static readonly EngineHostDescriptor Descriptor = new() { Kind = EngineHostKind.InProcess, Isolation = Isolation.Process, Attestation = Attestation.None };

    /// <summary>Before a session is bound the options are empty and refuse a change, steering is unsupported, and a steer finds no running turn.</summary>
    [Fact]
    public async Task BeforeASessionIsBoundNothingIsOfferedAndNothingIsSent()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new ControlSession(), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);

        host.SessionOptions.Options.ShouldBeEmpty();
        (await host.SessionOptions.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.NotOpen);
        host.LiveSteering.IsSupported.ShouldBeFalse();
        (await host.LiveSteering.SteerAsync("hi", OmpSteerQueue.FollowUp, ct).ConfigureAwait(true)).ShouldBe(new OmpSteerResult(OmpSteerOutcome.NotRunning, OmpSteerQueue.FollowUp, "No turn is running."));
    }

    /// <summary>Once connected the host reports the session's options (and raises the change event for them), and forwards changes and steers to it.</summary>
    [Fact]
    public async Task AConnectedHostForwardsToTheSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var session = new ControlSession();
        var host = new InProcessEngineHost(() => session, Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        var changes = 0;
        host.SessionOptions.OptionsChanged += (_, _) => changes++;

        await host.ConnectAsync(ct).ConfigureAwait(true);

        changes.ShouldBe(1, "binding the session announces its options");
        host.SessionOptions.Options.Select(static option => option.Id).ShouldBe(["mode"]);
        host.LiveSteering.IsSupported.ShouldBeTrue();

        var set = await host.SessionOptions.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);
        var steered = await host.LiveSteering.SteerAsync("careful", OmpSteerQueue.Steering, ct).ConfigureAwait(true);

        set.IsSet.ShouldBeTrue();
        session.Changes.ShouldBe([("mode", "plan")]);
        steered.Outcome.ShouldBe(OmpSteerOutcome.Injected);
        session.Steers.ShouldBe([("careful", OmpSteerQueue.Steering)]);

        session.Raise();
        changes.ShouldBe(3, "the session's own report (after the change) reaches the host's subscribers");
    }

    /// <summary>A session the host replaces after a fault takes over: the host's options and steering follow the new session, and the old one is no longer asked.</summary>
    [Fact]
    public async Task AReplacedSessionIsFollowed()
    {
        var ct = TestContext.Current.CancellationToken;
        var sessions = new List<ControlSession>();
        var host = new InProcessEngineHost(
            () =>
            {
                var session = new ControlSession { SteeringSupported = sessions.Count > 0 };
                sessions.Add(session);
                return session;
            },
            Descriptor);
        await using var hostScope = host.ConfigureAwait(true);
        await host.ConnectAsync(ct).ConfigureAwait(true);
        host.LiveSteering.IsSupported.ShouldBeFalse();
        sessions[0].State = HarnessState.Faulted;

        await host.ConnectAsync(ct).ConfigureAwait(true);

        sessions.Count.ShouldBe(2);
        host.LiveSteering.IsSupported.ShouldBeTrue();
        await host.SessionOptions.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true);
        sessions[0].Changes.ShouldBeEmpty();
        sessions[1].Changes.ShouldBe([("mode", "plan")]);
    }

    /// <summary>A session that offers neither options nor steering leaves the host with empty options, unsupported steering, and a refused change.</summary>
    [Fact]
    public async Task ASessionWithoutTheseSurfacesOffersNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new PlainSession(), Descriptor);
        await using var hostScope = host.ConfigureAwait(true);

        await host.ConnectAsync(ct).ConfigureAwait(true);

        host.SessionOptions.Options.ShouldBeEmpty();
        host.LiveSteering.IsSupported.ShouldBeFalse();
        (await host.SessionOptions.SetOptionAsync("mode", "plan", ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpOptionSetOutcome.NotOpen);
        (await host.LiveSteering.SteerAsync("hi", OmpSteerQueue.Steering, ct).ConfigureAwait(true)).Outcome.ShouldBe(OmpSteerOutcome.NotRunning);
    }

    /// <summary>A disposed host no longer follows its session.</summary>
    [Fact]
    public async Task ADisposedHostFollowsNothing()
    {
        var ct = TestContext.Current.CancellationToken;
        var host = new InProcessEngineHost(() => new ControlSession(), Descriptor);
        await host.ConnectAsync(ct).ConfigureAwait(true);

        await host.DisposeAsync().ConfigureAwait(true);

        host.SessionOptions.Options.ShouldBeEmpty();
        host.LiveSteering.IsSupported.ShouldBeFalse();
    }

    /// <summary>A session with a mode option and live steering, scripted by the test.</summary>
    private sealed class ControlSession : IOmpSession, IOmpSessionOptions, IOmpLiveSteering
    {
        private OmpSessionOption _mode = new("mode", "Mode", "mode", "default", [new OmpSessionOptionValue("default", "Default", null), new OmpSessionOptionValue("plan", "Plan", "Planning")]);

        public event EventHandler? OptionsChanged;

        public IReadOnlyList<OmpSessionOption> Options => [_mode];

        public bool SteeringSupported { get; init; } = true;

        public bool IsSupported => SteeringSupported;

        public List<(string ConfigId, string Value)> Changes { get; } = [];

        public List<(string Text, OmpSteerQueue Queue)> Steers { get; } = [];

        public HarnessState State { get; set; } = HarnessState.Ready;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public void Raise() => OptionsChanged?.Invoke(this, EventArgs.Empty);

        public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
        {
            Changes.Add((configId, value));
            _mode = _mode with { CurrentValue = value };
            Raise();
            return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.Set, value, null));
        }

        public Task<OmpSteerResult> SteerAsync(string text, OmpSteerQueue queue, CancellationToken cancellationToken)
        {
            Steers.Add((text, queue));
            return Task.FromResult(new OmpSteerResult(OmpSteerOutcome.Injected, queue, null));
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

    /// <summary>A session that implements neither surface.</summary>
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
