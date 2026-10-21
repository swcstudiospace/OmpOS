using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Workflows;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Abstract contract tests for <see cref="ISessionRunWorkflow"/> implementations.
/// </summary>
/// <remarks>
/// Tests the workflow contract: provision → run prompt → collect → teardown in order,
/// with teardown in finally semantics (happy path, prompt failure, provision failure,
/// heartbeats, cancellation). Run against <see cref="DirectSessionRunner"/> and reusable
/// for the Temporal implementation later (WFLW-03).
/// </remarks>
/// <typeparam name="TFixture">The test fixture type providing test activities.</typeparam>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public abstract class SessionRunWorkflowContractTests<TFixture> : IClassFixture<TFixture>
    where TFixture : class, ISessionRunWorkflowFixture
{
    private static readonly string[] ExpectedCallOrder =
    ["ProvisionSandbox", "RunPrompt", "CollectArtifacts", "TeardownSandbox"];

    private readonly TFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRunWorkflowContractTests{TFixture}"/> class.
    /// </summary>
    /// <param name="fixture">The test fixture.</param>
    protected SessionRunWorkflowContractTests(TFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates a workflow instance for testing.
    /// </summary>
    /// <param name="fixture">The test fixture.</param>
    /// <returns>The workflow under test.</returns>
    protected abstract ISessionRunWorkflow CreateWorkflow(TFixture fixture);

    /// <summary>
    /// Creates a workflow context for testing.
    /// </summary>
    /// <returns>The context under test.</returns>
    protected abstract IWorkflowContext CreateContext();

    /// <summary>
    /// Happy path: provisions → runs prompt → collects → tears down in order.
    /// </summary>
    [Fact]
    public async Task HappyPath_ProvisionsRunsCollectsTearsDownInOrder()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        var callOrder = new List<string>();
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        };
        activities.RunPromptOverride = (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
            hb("seq:1");
            return Task.FromResult(1L);
        };
        activities.CollectArtifactsOverride = (i, ct) =>
        {
            callOrder.Add("CollectArtifacts");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        };
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            callOrder.Add("TeardownSandbox");
            return Task.CompletedTask;
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Completed, result.Status);
        Assert.True(callOrder.SequenceEqual(ExpectedCallOrder, StringComparer.Ordinal));
        Assert.True(activities.ProvisionSandboxCalled);
        Assert.True(activities.RunPromptCalled);
        Assert.True(activities.CollectArtifactsCalled);
        Assert.True(activities.TeardownSandboxCalled);
    }

    /// <summary>
    /// Prompt failure still tears down (finally guarantee) and skips collection.
    /// </summary>
    [Fact]
    public async Task PromptFailure_StillTearsDown()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        var callOrder = new List<string>();
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        };
        activities.RunPromptOverride = (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
            return Task.FromException<long>(new InvalidOperationException("Prompt execution failed"));
        };
        activities.CollectArtifactsOverride = (i, ct) =>
        {
            callOrder.Add("CollectArtifacts");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        };
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            callOrder.Add("TeardownSandbox");
            return Task.CompletedTask;
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("Prompt execution failed", result.FailureDetail, StringComparison.Ordinal);
        Assert.Contains("TeardownSandbox", callOrder, StringComparer.Ordinal);
        Assert.True(activities.TeardownSandboxCalled);
        Assert.DoesNotContain("CollectArtifacts", callOrder, StringComparer.Ordinal);
    }

    /// <summary>
    /// Teardown failure after a completed run is recorded as a Failed status, never thrown.
    /// </summary>
    [Fact]
    public async Task TeardownFailure_RecordedAsFailedStatus()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        activities.ProvisionSandboxOverride = (i, ct) =>
            Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        activities.RunPromptOverride = (i, hb, ct) => Task.FromResult(1L);
        activities.CollectArtifactsOverride = (i, ct) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        activities.TeardownSandboxOverride = (i, ct) =>
            Task.FromException(new InvalidOperationException("Teardown blew up"));

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("Teardown failed", result.FailureDetail, StringComparison.Ordinal);
        Assert.True(activities.TeardownSandboxCalled);
    }

    /// <summary>
    /// Provision failure leaves the session not-Running and skips teardown.
    /// </summary>
    [Fact]
    public async Task ProvisionFailure_SessionNotRunning_NoTeardown()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        var callOrder = new List<string>();
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromException<OmpOS.Contracts.Sandboxes.SandboxDescriptor>(
                new InvalidOperationException("Provisioning failed: prerequisite missing"));
        };
        activities.RunPromptOverride = (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
            return Task.FromResult(1L);
        };
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            callOrder.Add("TeardownSandbox");
            return Task.CompletedTask;
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("Provisioning failed", result.FailureDetail, StringComparison.Ordinal);
        Assert.DoesNotContain("TeardownSandbox", callOrder, StringComparer.Ordinal);
        Assert.False(activities.TeardownSandboxCalled);
        Assert.False(activities.RunPromptCalled);
    }

    /// <summary>
    /// Heartbeat events are observed during prompt execution.
    /// </summary>
    [Fact]
    public async Task HeartbeatEvents_ObservedDuringPrompt()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        activities.RunPromptOverride = (i, hb, ct) =>
        {
            hb("seq:1");
            hb("seq:2");
            hb("seq:3");
            return Task.FromResult(3L);
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        var testCtx = Assert.IsType<TestWorkflowContext>(ctx);
        Assert.Contains(testCtx.Heartbeats, h => h.Contains("seq:1", StringComparison.Ordinal));
        Assert.Contains(testCtx.Heartbeats, h => h.Contains("seq:2", StringComparison.Ordinal));
        Assert.Contains(testCtx.Heartbeats, h => h.Contains("seq:3", StringComparison.Ordinal));
    }

    /// <summary>
    /// Cancellation during execution results in Cancelled status with teardown.
    /// </summary>
    [Fact]
    public async Task Cancellation_ResultsInCancelledStatusWithTeardown()
    {
        // Arrange
        var activities = _fixture.Activities;
        activities.Reset();
        var workflow = CreateWorkflow(_fixture);
        var ctx = CreateContext();
        var input = TestInputFactory.Create();

        var callOrder = new List<string>();
        using var cts = new CancellationTokenSource();
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        };
        activities.RunPromptOverride = async (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
            await cts.CancelAsync();
            await Task.Delay(100, ct);
            return 1L;
        };
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            callOrder.Add("TeardownSandbox");
            return Task.CompletedTask;
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, cts.Token);

        // Assert
        Assert.Equal(WorkflowRunStatus.Cancelled, result.Status);
        Assert.Contains("TeardownSandbox", callOrder, StringComparer.Ordinal);
        Assert.True(activities.TeardownSandboxCalled);
    }
}
