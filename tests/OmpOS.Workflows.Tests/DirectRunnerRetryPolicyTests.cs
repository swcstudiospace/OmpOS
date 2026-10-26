using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Workflows;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Behaviour tests for the direct runner's per-activity retry policy (ADR-0003, Q-WF-RETRY).
/// </summary>
/// <remarks>
/// Attempt counts: provision 3, run prompt 1 (not idempotent),
/// collect 3, teardown 5. Backoff is requested through <see cref="IWorkflowContext.DelayAsync"/>;
/// the recording context completes each delay immediately, so the policy runs with zero wall-clock delay.
/// </remarks>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class DirectRunnerRetryPolicyTests
{
    private static readonly SessionRunInput Input = TestInputFactory.Create();

    /// <summary>
    /// An idempotent activity that fails twice then succeeds completes after exactly three attempts.
    /// </summary>
    [Fact]
    public async Task IdempotentProvision_FailsTwice_CompletesOnThirdAttempt()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Provision = (attempt, _) => attempt < 3
                ? FailAsync<SandboxDescriptor>("provision flake")
                : Task.FromResult(TestSessionRunActivities.CreateSandbox(Input)),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Completed, result.Status);
        Assert.Equal(3, activities.ProvisionAttempts);
        Assert.Equal(1, activities.PromptAttempts);
        Assert.Equal(1, activities.CollectAttempts);
        Assert.Equal(1, activities.TeardownAttempts);
    }

    /// <summary>
    /// An activity that always fails stops at its maximum attempts; the run reports Failed with the
    /// failure detail, and teardown still runs because the sandbox was already provisioned.
    /// </summary>
    [Fact]
    public async Task CollectAlwaysFailing_StopsAtThreeAttempts_RunFailedWithDetail_TeardownStillRuns()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Collect = (_, _) => FailAsync<IReadOnlyList<string>>("collect boom"),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("collect boom", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(3, activities.CollectAttempts);
        Assert.Empty(result.Artifacts);
        Assert.Equal(1, activities.TeardownAttempts);
    }

    /// <summary>
    /// Provision that always fails stops at three attempts and reports Failed without teardown,
    /// because no sandbox was provisioned to tear down.
    /// </summary>
    [Fact]
    public async Task ProvisionAlwaysFailing_StopsAtThreeAttempts_RunFailedWithoutTeardown()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Provision = (_, _) => FailAsync<SandboxDescriptor>("provision boom"),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("provision boom", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(3, activities.ProvisionAttempts);
        Assert.Equal(0, activities.PromptAttempts);
        Assert.Equal(0, activities.TeardownAttempts);
    }

    /// <summary>
    /// The run-prompt activity is non-idempotent and is attempted exactly once, even when it fails.
    /// </summary>
    [Fact]
    public async Task RunPrompt_NonIdempotent_AttemptedOnce()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Prompt = (_, _) => FailAsync<long>("prompt boom"),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("prompt boom", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(1, activities.PromptAttempts);
        Assert.Equal(0, activities.CollectAttempts);
        Assert.Equal(1, activities.TeardownAttempts);
        Assert.Empty(ctx.Delays);
    }

    /// <summary>
    /// Teardown that always fails is attempted five times and recorded as a Failed status, never thrown.
    /// </summary>
    [Fact]
    public async Task TeardownAlwaysFailing_StopsAtFiveAttempts_RecordedAsFailed()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Teardown = (_, _) => FailVoidAsync("teardown boom"),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(WorkflowRunStatus.Failed, result.Status);
        Assert.Contains("Teardown failed: teardown boom", result.FailureDetail, StringComparison.Ordinal);
        Assert.Equal(5, activities.TeardownAttempts);
    }

    /// <summary>
    /// Backoff between teardown attempts doubles from one second.
    /// </summary>
    [Fact]
    public async Task TeardownBackoff_DoublesFromOneSecondBetweenAttempts()
    {
        // Arrange
        var activities = new ScriptedActivities(Input)
        {
            Teardown = (_, _) => FailVoidAsync("teardown boom"),
        };
        var ctx = new RecordingDelayContext();
        TimeSpan[] expected =
        [
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
        ];

        // Act
        await new DirectSessionRunner(activities).RunAsync(Input, ctx, CancellationToken.None);

        // Assert
        Assert.Equal(expected, ctx.Delays);
    }

    /// <summary>
    /// Cancellation during provision is never retried: the run reports Cancelled after one attempt.
    /// </summary>
    [Fact]
    public async Task CancellationDuringProvision_NotRetried_RunCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var activities = new ScriptedActivities(Input)
        {
            Provision = (_, ct) => Task.FromCanceled<SandboxDescriptor>(ct),
        };
        var ctx = new RecordingDelayContext();

        // Act
        var result = await new DirectSessionRunner(activities).RunAsync(Input, ctx, cts.Token);

        // Assert
        Assert.Equal(WorkflowRunStatus.Cancelled, result.Status);
        Assert.Equal(1, activities.ProvisionAttempts);
        Assert.Empty(ctx.Delays);
        Assert.Equal(0, activities.TeardownAttempts);
    }

    private static Task<T> FailAsync<T>(string message) => Task.FromException<T>(new InvalidOperationException(message));

    private static Task FailVoidAsync(string message) => Task.FromException(new InvalidOperationException(message));

    /// <summary>
    /// Activities whose behaviour is scripted per attempt; each call increments its attempt counter first.
    /// </summary>
    private sealed class ScriptedActivities : ISessionRunActivities
    {
        public ScriptedActivities(SessionRunInput input)
        {
            Provision = (_, _) => Task.FromResult(TestSessionRunActivities.CreateSandbox(input));
            Prompt = (_, _) => Task.FromResult(1L);
            Collect = (_, _) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            Teardown = (_, _) => Task.CompletedTask;
        }

        public Func<int, CancellationToken, Task<SandboxDescriptor>> Provision { get; set; }

        public Func<int, CancellationToken, Task<long>> Prompt { get; set; }

        public Func<int, CancellationToken, Task<IReadOnlyList<string>>> Collect { get; set; }

        public Func<int, CancellationToken, Task> Teardown { get; set; }

        public int ProvisionAttempts { get; private set; }

        public int PromptAttempts { get; private set; }

        public int CollectAttempts { get; private set; }

        public int TeardownAttempts { get; private set; }

        public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
        {
            ProvisionAttempts++;
            return Provision(ProvisionAttempts, cancellationToken);
        }

        public Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
        {
            PromptAttempts++;
            return Prompt(PromptAttempts, cancellationToken);
        }

        public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
        {
            CollectAttempts++;
            return Collect(CollectAttempts, cancellationToken);
        }

        public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
        {
            TeardownAttempts++;
            return Teardown(TeardownAttempts, cancellationToken);
        }
    }

    /// <summary>
    /// Workflow context that records each backoff delay and completes it immediately.
    /// </summary>
    private sealed class RecordingDelayContext : IWorkflowContext
    {
        public List<TimeSpan> Delays { get; } = [];

        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

        public Random Random => Random.Shared;

        public Guid NewGuid() => Guid.Empty;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            return Task.CompletedTask;
        }

        public void Heartbeat(string details)
        {
        }
    }
}
