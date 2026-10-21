using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Workflows;
using OmpOS.Harness.Abstractions;
using OmpOS.Sandbox.Abstractions;
using OmpOS.Workflows.Worker;
using Shouldly;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Tests the composed session-run activities and the startup guard that requires their dependencies (OD-02, WR-10).
/// </summary>
/// <remarks>
/// The composed activities are exercised through <see cref="DirectSessionRunner"/>, the runner the direct path uses,
/// over a recording sandbox provider and a scripted harness. No fake activities are involved.
/// </remarks>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class WorkerCompositionTests
{
    private const string Prompt = "summarize the repository";
    private const string SandboxId = "sbx-idem-1";

    private static readonly string[] ExpectedHeartbeats = ["appended:1"];

    /// <summary>Verifies startup fails and names the sandbox provider key when no provider is registered.</summary>
    [Fact]
    public void ConfigureServices_MissingSandboxProvider_FailsAtStartupNamingKey()
    {
        // Arrange — the harness factory is registered; the sandbox provider is not.
        var services = new ServiceCollection();
        services.AddSingleton<IHarnessSessionFactory>(new ScriptedHarnessFactory([]));

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true }));
        ex.Message.ShouldContain("Sandbox:Provider");
    }

    /// <summary>Verifies startup fails and names the harness adapter key when no harness factory is registered.</summary>
    [Fact]
    public void ConfigureServices_MissingHarnessFactory_FailsAtStartupNamingKey()
    {
        // Arrange — the sandbox provider is registered; the harness factory is not.
        var services = new ServiceCollection();
        services.AddSingleton<ISandboxProvider>(new RecordingSandboxProvider());

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true }));
        ex.Message.ShouldContain("Harness:Adapter");
    }

    /// <summary>Verifies one successful composed run yields the same result, provisioning and teardown as the direct path.</summary>
    [Fact]
    public async Task ComposedRun_SuccessfulPrompt_MatchesDirectPathResult()
    {
        // Arrange
        var sandbox = new RecordingSandboxProvider();
        var harness = new ScriptedHarnessFactory([new TextDeltaEvent("hello"), new CompletedEvent("end_turn")]);
        using var container = BuildContainer(sandbox, harness);
        var activities = container.GetRequiredService<ISessionRunActivities>();
        var context = new TestWorkflowContext(randomSeed: 42);

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(
            TestInputFactory.Create(prompt: Prompt), context, TestContext.Current.CancellationToken);

        // Assert — the result the direct path reports for one successful prompt.
        result.Status.ShouldBe(WorkflowRunStatus.Completed);
        result.FinalSeq.ShouldBe(1L);
        result.Artifacts.ShouldBeEmpty();
        result.FailureDetail.ShouldBeNull();
        Assert.Equal(ExpectedHeartbeats, context.Heartbeats);

        // Assert — provisioning uses the direct path's request and teardown releases the provisioned sandbox.
        sandbox.Provisions.Count.ShouldBe(1);
        var request = sandbox.Provisions[0];
        request.IdempotencyKey.ShouldBe("idem-1");
        request.SessionId.ShouldBe("session-1");
        request.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        request.Spec.Image.ShouldBe("ompos/in-process");
        Assert.Equal(new[] { SandboxId }, sandbox.Teardowns);
        Assert.Equal(new[] { Prompt }, harness.Prompts);
    }

    /// <summary>Verifies a failed prompt reports Failed and still tears down the provisioned sandbox.</summary>
    [Fact]
    public async Task ComposedRun_PromptFailure_ReportsFailedAndTearsDownSandbox()
    {
        // Arrange
        var sandbox = new RecordingSandboxProvider();
        var harness = new ScriptedHarnessFactory([], new InvalidOperationException("harness unavailable"));
        using var container = BuildContainer(sandbox, harness);
        var activities = container.GetRequiredService<ISessionRunActivities>();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(
            TestInputFactory.Create(prompt: Prompt), new TestWorkflowContext(randomSeed: 42), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(WorkflowRunStatus.Failed);
        result.FailureDetail.ShouldBe("harness unavailable");
        Assert.Equal(new[] { SandboxId }, sandbox.Teardowns);
    }

    private static ServiceProvider BuildContainer(ISandboxProvider sandbox, IHarnessSessionFactory harness)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sandbox);
        services.AddSingleton(harness);
        WorkerHostConfiguration.ConfigureServices(services, new WorkerOptions { Enabled = true });
        return services.BuildServiceProvider();
    }

    /// <summary>Sandbox provider that records provisions and teardowns; unused operations fail loudly.</summary>
    private sealed class RecordingSandboxProvider : ISandboxProvider
    {
        private readonly List<ProvisionRequest> _provisions = [];
        private readonly List<string> _teardowns = [];

        /// <summary>Gets the provision requests received, in order.</summary>
        public List<ProvisionRequest> Provisions => _provisions;

        /// <summary>Gets the sandbox identifiers torn down, in order.</summary>
        public IReadOnlyList<string> Teardowns => _teardowns;

        /// <inheritdoc />
        public SandboxCapability Capabilities { get; } = new()
        {
            Isolation = Isolation.Container,
            Attestation = Attestation.None,
            NativeBinaries = false,
            SnapshotsSupported = false,
            MaxTtlSeconds = 1800,
        };

        /// <inheritdoc />
        public Task<ProvisionResult> ProvisionAsync(ProvisionRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            _provisions.Add(request);
            return Task.FromResult(new ProvisionResult
            {
                NewlyProvisioned = true,
                Descriptor = new SandboxDescriptor
                {
                    Id = $"sbx-{request.IdempotencyKey}",
                    SessionId = request.SessionId,
                    Isolation = request.Isolation,
                    Attestation = Attestation.None,
                    NativeBinaries = false,
                    TtlSeconds = 1800,
                    Labels = new Dictionary<string, string>(StringComparer.Ordinal),
                },
            });
        }

        /// <inheritdoc />
        public Task AttachAsync(string sandboxId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The recording provider does not attach to sandboxes.");

        /// <inheritdoc />
        public IAsyncEnumerable<ExecEvent> ExecAsync(string sandboxId, ExecRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The recording provider does not execute commands.");

        /// <inheritdoc />
        public IAsyncEnumerable<string> StreamLogsAsync(string sandboxId, LogCursor? cursor = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The recording provider does not stream logs.");

        /// <inheritdoc />
        public Task<SnapshotResult> SnapshotAsync(string sandboxId, CancellationToken cancellationToken = default) =>
            Task.FromResult(SnapshotResult.NotSupportedResult("The recording provider takes no snapshots."));

        /// <inheritdoc />
        public Task TeardownAsync(string sandboxId, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(sandboxId);
            _teardowns.Add(sandboxId);
            return Task.CompletedTask;
        }
    }

    /// <summary>Harness factory that records prompts and hands out sessions replaying a fixed script.</summary>
    private sealed class ScriptedHarnessFactory : IHarnessSessionFactory
    {
        private readonly IReadOnlyList<HarnessEvent> _events;
        private readonly Exception? _submitFailure;
        private readonly List<string> _prompts = [];

        public ScriptedHarnessFactory(IReadOnlyList<HarnessEvent> events, Exception? submitFailure = null)
        {
            _events = events;
            _submitFailure = submitFailure;
        }

        /// <summary>Gets the prompts passed to <see cref="CreateSession"/>, in order.</summary>
        public IReadOnlyList<string> Prompts => _prompts;

        /// <inheritdoc />
        public IOmpSession CreateSession(string prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);
            _prompts.Add(prompt);
            return new ScriptedSession(_events, _submitFailure);
        }
    }

    /// <summary>Harness session that streams a fixed event script, or fails on submit when scripted to.</summary>
    private sealed class ScriptedSession : IOmpSession
    {
        private readonly IReadOnlyList<HarnessEvent> _events;
        private readonly Exception? _submitFailure;

        public ScriptedSession(IReadOnlyList<HarnessEvent> events, Exception? submitFailure)
        {
            _events = events;
            _submitFailure = submitFailure;
        }

        /// <inheritdoc />
        public HarnessState State { get; private set; } = HarnessState.Disconnected;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <inheritdoc />
        public Task CreateAsync(CancellationToken cancellationToken)
        {
            State = HarnessState.Ready;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The scripted session cannot resume an engine session.");

        /// <inheritdoc />
        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
            string prompt,
            PromptOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_submitFailure is { } failure)
            {
                throw failure;
            }

            foreach (var evt in _events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return evt;
            }
        }

        /// <inheritdoc />
        public Task CancelAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The scripted session has no in-flight prompt to cancel.");

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }
}
