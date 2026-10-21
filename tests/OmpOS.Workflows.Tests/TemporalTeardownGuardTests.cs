using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Workflows;
using OmpOS.Workflows.Worker;
using Xunit;

namespace OmpOS.Workflows.Tests;

/// <summary>
/// Unit tests for the Temporal teardown-failure mapping (CR-04).
/// </summary>
[SuppressMessage("Usage", "CA2007:Do not directly await a Task", Justification = "xUnit test methods await on the test context; ConfigureAwait(true) is the test default.")]
[SuppressMessage("Usage", "MA0004:Use Task.ConfigureAwait", Justification = "Same as CA2007: test methods await on the test context.")]
public sealed class TemporalTeardownGuardTests
{
    /// <summary>
    /// A teardown failure after a completed run demotes the status to Failed with detail.
    /// </summary>
    [Fact]
    public void TeardownFailure_AfterCompleted_RecordedAsFailed()
    {
        var (status, failureDetail) = TemporalSessionRunWorkflow.ApplyTeardownFailure(
            WorkflowRunStatus.Completed, null, "down blew up");

        Assert.Equal(WorkflowRunStatus.Failed, status);
        Assert.Contains("Teardown failed", failureDetail, StringComparison.Ordinal);
        Assert.Contains("down blew up", failureDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A teardown failure never masks an earlier failure: status and detail are preserved.
    /// </summary>
    [Fact]
    public void TeardownFailure_AfterFailed_PreservesOriginalFailure()
    {
        var (status, failureDetail) = TemporalSessionRunWorkflow.ApplyTeardownFailure(
            WorkflowRunStatus.Failed, "prompt blew up", "down blew up");

        Assert.Equal(WorkflowRunStatus.Failed, status);
        Assert.Equal("prompt blew up", failureDetail);
    }

    /// <summary>
    /// A teardown failure after cancellation preserves the Cancelled status.
    /// </summary>
    [Fact]
    public void TeardownFailure_AfterCancelled_PreservesCancelled()
    {
        var (status, failureDetail) = TemporalSessionRunWorkflow.ApplyTeardownFailure(
            WorkflowRunStatus.Cancelled, "cancelled by user", "down blew up");

        Assert.Equal(WorkflowRunStatus.Cancelled, status);
        Assert.Equal("cancelled by user", failureDetail);
    }
}
