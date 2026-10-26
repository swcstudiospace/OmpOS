using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.InMemory;
using OmpOS.Sandbox;
using OmpOS.Sandbox.Abstractions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Session-run workflow (WFLW-01): each prompt is one run through <c>DirectSessionRunner</c> —
/// exactly one provision and one teardown per session, with teardown in a finally even when the
/// prompt run fails. The counting decorator replaces the sandbox registration through the same
/// test factory pattern the harness replacements use.
/// </summary>
public sealed class SessionRunWorkflowTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies one prompt produces exactly one provision and one teardown for its session.</summary>
    [Fact]
    public async Task Prompt_CompletesWithExactlyOneProvisionAndTeardown()
    {
        var counting = new CountingSandboxProvider(new FakeSandboxProvider());
        var script = ScriptedHarnessFactory.DeltaScript(1);
        using var factory = new OmpApiFactory(configureServices: services =>
        {
            services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script));
            services.AddSingleton<ISandboxProvider>(counting);
        });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
        await WaitForCountAsync(() => counting.TeardownsFor(id), 1, testToken).ConfigureAwait(true);

        counting.ProvisionsFor(id).ShouldBe(1, "one prompt is one run with one provision");
        counting.TeardownsFor(id).ShouldBe(1, "the run tears down exactly once");
    }

    /// <summary>
    /// Verifies the teardown still happens when the prompt run fails: a scripted mid-stream harness
    /// failure settles the session as failed (with an error event) and the sandbox is torn down.
    /// </summary>
    [Fact]
    public async Task Prompt_Failure_StillTearsDown()
    {
        var counting = new CountingSandboxProvider(new FakeSandboxProvider());
        var script = new InMemoryScript { Steps = [new FailStep(new MalformedFrameHarnessException("boom"))] };
        using var factory = new OmpApiFactory(configureServices: services =>
        {
            services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory(script));
            services.AddSingleton<ISandboxProvider>(counting);
        });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken).ConfigureAwait(true);
        settled.Events.ShouldContain(static evt => evt.Payload is ErrorPayload, "the failed run appends an error event");
        await WaitForCountAsync(() => counting.TeardownsFor(id), 1, testToken).ConfigureAwait(true);

        counting.ProvisionsFor(id).ShouldBe(1, "the failed run still provisioned exactly once");
        counting.TeardownsFor(id).ShouldBe(1, "teardown runs in a finally even when the prompt fails");
    }

    /// <summary>
    /// Verifies a provision failure settles the session as failed: the workflow reports the failure without
    /// throwing, so the runner must settle the session that the endpoint left running.
    /// </summary>
    [Fact]
    public async Task ProvisionFailure_SettlesSessionAsFailed()
    {
        var sandbox = new FakeSandboxProvider();
        sandbox.FailProvision(3);
        using var factory = new OmpApiFactory(configureServices: services => services.AddSingleton<ISandboxProvider>(sandbox));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var settled = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Failed, SettleTimeout, testToken).ConfigureAwait(true);
        settled.Events.ShouldContain(static evt => evt.Payload is ErrorPayload, "the failed run appends an error event");
    }

    /// <summary>
    /// Verifies a teardown failure that lands after the next prompt was admitted does not fail that
    /// reply: the stale run skips settlement (its cleanup failure is logged), the next turn completes
    /// with its own reply, and the session stays usable for a prompt after that.
    /// </summary>
    [Fact]
    public async Task TeardownFailure_AfterNextPrompt_DoesNotFailNextReply()
    {
        var sandboxGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var harnessGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logs = new CapturingLoggerProvider();
        using var factory = new OmpApiFactory(
            configureServices: services =>
            {
                services.AddSingleton<IHarnessSessionFactory>(
                    new GatedHarnessFactory(ScriptedHarnessFactory.DeltaScript(1), harnessGate.Task));
                services.AddSingleton<ISandboxProvider>(
                    new GatedTeardownSandboxProvider(new FakeSandboxProvider(), sandboxGate.Task));
            },
            loggerProvider: logs);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using (var first = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"first\"}", testToken).ConfigureAwait(true))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);

        using (var second = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"second\"}", testToken).ConfigureAwait(true))
        {
            second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        // The second run streams its delta, then parks mid-turn: the stale teardown failure below
        // must land while the next turn is still Running.
        await WaitForDeltaCountAsync(client, id, 2, testToken).ConfigureAwait(true);
        sandboxGate.TrySetResult();
        await Task.Delay(TimeSpan.FromMilliseconds(500), testToken).ConfigureAwait(true);
        harnessGate.TrySetResult();

        var settled = await WaitForTerminalAsync(client, id, testToken).ConfigureAwait(true);
        settled.Status.ShouldBe(SessionStatus.Completed);
        settled.Events.Count(static evt => evt.Payload is TextDeltaPayload).ShouldBe(2, "the next reply streams fully");
        settled.Events.Any(static evt => evt.Payload is ErrorPayload).ShouldBeFalse("a stale cleanup must not fail the next reply");

        using (var third = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"third\"}", testToken).ConfigureAwait(true))
        {
            third.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);
    }

    /// <summary>Polls a session until its log holds at least <paramref name="expected"/> text deltas.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="expected">The minimum delta count.</param>
    /// <param name="testToken">The test cancellation token.</param>
    private static async Task WaitForDeltaCountAsync(HttpClient client, string sessionId, int expected, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        while (true)
        {
            var session = await SessionClient.ReadSessionAsync(client, sessionId, linked.Token).ConfigureAwait(false);
            if (session.Events.Count(static evt => evt.Payload is TextDeltaPayload) >= expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), linked.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Polls a session until it reaches a terminal status.</summary>
    /// <param name="client">The test client.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The session at its terminal status.</returns>
    private static async Task<Session> WaitForTerminalAsync(HttpClient client, string sessionId, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var timeout = new CancellationTokenSource(SettleTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        while (true)
        {
            var session = await SessionClient.ReadSessionAsync(client, sessionId, linked.Token).ConfigureAwait(false);
            if (session.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled)
            {
                return session;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), linked.Token).ConfigureAwait(false);
        }
    }

    /// <summary>Sandbox decorator failing the first provisioned sandbox's teardown once the gate opens.</summary>
    private sealed class GatedTeardownSandboxProvider : ISandboxProvider
    {
        private readonly ISandboxProvider _inner;
        private readonly Task _gate;
        private readonly Lock _sync = new();
        private string? _failId;

        /// <summary>Initializes a new instance of the <see cref="GatedTeardownSandboxProvider"/> class.</summary>
        /// <param name="inner">The provider to delegate to.</param>
        /// <param name="gate">The gate the doomed teardown waits on.</param>
        public GatedTeardownSandboxProvider(ISandboxProvider inner, Task gate)
        {
            _inner = inner;
            _gate = gate;
        }

        /// <inheritdoc />
        public SandboxCapability Capabilities => _inner.Capabilities;

        /// <inheritdoc />
        public async Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            var result = await _inner.ProvisionAsync(request, cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                _failId ??= result.Descriptor.Id;
            }

            return result;
        }

        /// <inheritdoc />
        public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default)
        {
            return _inner.AttachAsync(sandboxId, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default)
        {
            return _inner.ExecAsync(sandboxId, request, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default)
        {
            return _inner.StreamLogsAsync(sandboxId, cursor, cancellationToken);
        }

        /// <inheritdoc />
        public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default)
        {
            return _inner.SnapshotAsync(sandboxId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
        {
            string? failId;
            lock (_sync)
            {
                failId = _failId;
            }

            if (string.Equals(sandboxId, failId, StringComparison.Ordinal))
            {
                await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("teardown boom");
            }

            await _inner.TeardownAsync(sandboxId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Replays a fast script for the first prompt; parks later prompts mid-stream until released.</summary>
    private sealed class GatedHarnessFactory : IHarnessSessionFactory
    {
        private readonly InMemoryScript _first;
        private readonly Task _release;
        private int _calls;

        /// <summary>Initializes a new instance of the <see cref="GatedHarnessFactory"/> class.</summary>
        /// <param name="first">The script the first prompt replays.</param>
        /// <param name="release">The gate parked prompts wait on.</param>
        public GatedHarnessFactory(InMemoryScript first, Task release)
        {
            _first = first;
            _release = release;
        }

        /// <inheritdoc />
        public IOmpSession CreateSession(string prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            return Interlocked.Increment(ref _calls) == 1
                ? new InMemoryOmpAdapter(_first).CreateSession()
                : new GatedSession(_release);
        }

        /// <summary>A session streaming one delta, then parking until released before completing.</summary>
        private sealed class GatedSession : IOmpSession
        {
            private readonly Task _release;

            /// <summary>Initializes a new instance of the <see cref="GatedSession"/> class.</summary>
            /// <param name="release">The gate the stream waits on.</param>
            public GatedSession(Task release)
            {
                _release = release;
            }

            /// <inheritdoc />
            public HarnessState State => HarnessState.Ready;

            /// <inheritdoc />
            public IReadOnlyDictionary<string, string> AdvertisedCapabilities => new Dictionary<string, string>(StringComparer.Ordinal);

            /// <inheritdoc />
            public Task CreateAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            /// <inheritdoc />
            public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) => Task.CompletedTask;

            /// <inheritdoc />
            public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            /// <inheritdoc />
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            /// <inheritdoc />
            public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
                string prompt,
                PromptOptions options,
                [EnumeratorCancellation] CancellationToken cancellationToken)
            {
                yield return new TextDeltaEvent("delta-gated");
                await _release.WaitAsync(cancellationToken).ConfigureAwait(false);
                yield return new CompletedEvent("end_turn");
            }
        }
    }

    /// <summary>Polls a count until it reaches the expected value or the test times out.</summary>
    /// <param name="read">The count reader.</param>
    /// <param name="expected">The expected count.</param>
    /// <param name="testToken">The test cancellation token.</param>
    private static async Task WaitForCountAsync(Func<int> read, int expected, CancellationToken testToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, testToken);
        while (read() != expected)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), linked.Token).ConfigureAwait(false);
        }
    }
}
