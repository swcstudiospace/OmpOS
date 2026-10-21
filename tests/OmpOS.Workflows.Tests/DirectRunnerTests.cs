using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Workflows;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Contract tests for <see cref="DirectSessionRunner"/>.
/// </summary>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class DirectRunnerTests : SessionRunWorkflowContractTests<DirectRunnerFixture>
{
    private readonly DirectRunnerFixture _directFixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectRunnerTests"/> class.
    /// </summary>
    /// <param name="fixture">The test fixture.</param>
    public DirectRunnerTests(DirectRunnerFixture fixture)
        : base(fixture)
    {
        _directFixture = fixture;
    }

    /// <inheritdoc />
    protected override ISessionRunWorkflow CreateWorkflow(DirectRunnerFixture fixture) => fixture.CreateWorkflow();

    /// <inheritdoc />
    protected override IWorkflowContext CreateContext() => new TestWorkflowContext(randomSeed: 42);

    /// <summary>
    /// A teardown failure after a completed run is recorded as Failed, with the teardown failure as its detail.
    /// </summary>
    [Fact]
    public async Task CompletedRunWithTeardownFailure_BecomesFailedWithTeardownDetail()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var input = TestInputFactory.Create();
        var teardownAttempts = 0;

        activities.ProvisionSandboxOverride = (i, ct) => Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        activities.RunPromptOverride = (i, hb, ct) => Task.FromResult(1L);
        activities.CollectArtifactsOverride = (i, ct) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            teardownAttempts++;
            return Task.FromException(new InvalidOperationException("teardown boom"));
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Equal("Teardown failed: teardown boom", result.FailureDetail);
        Assert.Equal(5, teardownAttempts);
    }

    /// <summary>
    /// A teardown failure after a failed run leaves the Failed status and keeps the prompt failure detail.
    /// </summary>
    [Fact]
    public async Task FailedRunWithTeardownFailure_StaysFailedWithPromptDetail()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var input = TestInputFactory.Create();
        var teardownAttempts = 0;

        activities.ProvisionSandboxOverride = (i, ct) => Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        activities.RunPromptOverride = (i, hb, ct) => Task.FromException<long>(new InvalidOperationException("Prompt execution failed"));
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            teardownAttempts++;
            return Task.FromException(new InvalidOperationException("teardown boom"));
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Equal("Prompt execution failed", result.FailureDetail);
        Assert.Equal(5, teardownAttempts);
    }

    /// <summary>
    /// A teardown failure after cancellation leaves the Cancelled status and records no teardown failure.
    /// </summary>
    [Fact]
    public async Task CancelledRunWithTeardownFailure_StaysCancelled()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var input = TestInputFactory.Create();
        var teardownAttempts = 0;
        using var cts = new CancellationTokenSource();

        activities.ProvisionSandboxOverride = (i, ct) => Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        activities.RunPromptOverride = async (i, hb, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(100, ct);
            return 1L;
        };
        activities.TeardownSandboxOverride = (i, ct) =>
        {
            teardownAttempts++;
            return Task.FromException(new InvalidOperationException("teardown boom"));
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, cts.Token);

        // Assert
        Assert.Equal(WorkflowRunStatus.Cancelled, result.Status);
        Assert.DoesNotContain("Teardown failed", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(5, teardownAttempts);
    }

    /// <summary>
    /// The prompt-settled callback runs exactly once, after the prompt and before the sandbox teardown.
    /// </summary>
    [Fact]
    public async Task PromptSettledCallback_RunsOnceAfterPromptAndBeforeTeardown()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var callOrder = new List<string>();
        var input = TestInputFactory.Create() with
        {
            OnPromptSettled = _ =>
            {
                callOrder.Add("PromptSettled");
                return ValueTask.CompletedTask;
            },
        };
        RecordCalls(activities, callOrder);

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Completed, result.Status);
        string[] expected = ["ProvisionSandbox", "RunPrompt", "CollectArtifacts", "PromptSettled", "TeardownSandbox"];
        Assert.Equal<string>(expected, callOrder, StringComparer.Ordinal);
    }

    /// <summary>
    /// A successful run without a prompt-settled callback completes with no failure detail and runs no extra calls.
    /// </summary>
    [Fact]
    public async Task SuccessfulRunWithoutPromptSettledCallback_CompletesAsBefore()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var callOrder = new List<string>();
        var input = TestInputFactory.Create();
        RecordCalls(activities, callOrder);

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Completed, result.Status);
        Assert.Null(result.FailureDetail);
        Assert.Empty(result.Artifacts);
        string[] expected = ["ProvisionSandbox", "RunPrompt", "CollectArtifacts", "TeardownSandbox"];
        Assert.Equal<string>(expected, callOrder, StringComparer.Ordinal);
    }

    /// <summary>
    /// A prompt-settled callback that throws still lets the sandbox teardown run, and its exception propagates afterwards.
    /// </summary>
    [Fact]
    public async Task PromptSettledCallbackThrows_TeardownStillRunsThenExceptionPropagates()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var input = TestInputFactory.Create() with
        {
            OnPromptSettled = _ => ValueTask.FromException(new InvalidOperationException("callback boom")),
        };
        RecordCalls(activities, new List<string>());

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync(input, ctx, CancellationToken.None));

        // Assert
        Assert.Equal("callback boom", exception.Message);
        Assert.True(activities.TeardownSandboxCalled);
    }

    /// <summary>
    /// A failed prompt still invokes the prompt-settled callback exactly once, before the teardown.
    /// </summary>
    [Fact]
    public async Task PromptSettledCallback_RunsOnceWhenPromptFails()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var callOrder = new List<string>();
        var input = TestInputFactory.Create() with
        {
            OnPromptSettled = _ =>
            {
                callOrder.Add("PromptSettled");
                return ValueTask.CompletedTask;
            },
        };
        RecordCalls(activities, callOrder);
        activities.RunPromptOverride = (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
            return Task.FromException<long>(new InvalidOperationException("Prompt execution failed"));
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("Prompt execution failed", result.FailureDetail, StringComparison.Ordinal);
        string[] expected = ["ProvisionSandbox", "RunPrompt", "PromptSettled", "TeardownSandbox"];
        Assert.Equal<string>(expected, callOrder, StringComparer.Ordinal);
    }

    /// <summary>
    /// A run whose provisioning fails never invokes the prompt-settled callback.
    /// </summary>
    [Fact]
    public async Task PromptSettledCallback_NotInvokedWhenProvisionFails()
    {
        // Arrange
        var activities = _directFixture.Activities;
        activities.Reset();
        var workflow = _directFixture.CreateWorkflow();
        var ctx = CreateContext();
        var callOrder = new List<string>();
        var input = TestInputFactory.Create() with
        {
            OnPromptSettled = _ =>
            {
                callOrder.Add("PromptSettled");
                return ValueTask.CompletedTask;
            },
        };
        RecordCalls(activities, callOrder);
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromException<OmpOS.Contracts.Sandboxes.SandboxDescriptor>(
                new InvalidOperationException("Provisioning failed: prerequisite missing"));
        };

        // Act
        var result = await workflow.RunAsync(input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.DoesNotContain("PromptSettled", callOrder, StringComparer.Ordinal);
        Assert.DoesNotContain("TeardownSandbox", callOrder, StringComparer.Ordinal);
    }

    private static void RecordCalls(TestSessionRunActivities activities, List<string> callOrder)
    {
        activities.ProvisionSandboxOverride = (i, ct) =>
        {
            callOrder.Add("ProvisionSandbox");
            return Task.FromResult(TestSessionRunActivities.CreateSandbox(i));
        };
        activities.RunPromptOverride = (i, hb, ct) =>
        {
            callOrder.Add("RunPrompt");
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
    }
}
