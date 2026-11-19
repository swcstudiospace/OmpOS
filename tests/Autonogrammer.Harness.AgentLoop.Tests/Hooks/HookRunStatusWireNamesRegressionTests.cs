using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>Every harness status reaches a client under the wire string the contract documents, so a renamed status cannot pass unnoticed.</summary>
public sealed class HookRunStatusWireNamesRegressionTests
{
    /// <summary>Each harness status projects to its documented wire string.</summary>
    /// <param name="status">The harness status.</param>
    /// <param name="wire">The wire string the contract documents.</param>
    [Theory]
    [InlineData(HookRunStatus.Succeeded, "succeeded")]
    [InlineData(HookRunStatus.Denied, "denied")]
    [InlineData(HookRunStatus.Failed, "failed")]
    [InlineData(HookRunStatus.TimedOut, "timedOut")]
    [InlineData(HookRunStatus.Skipped, "skipped")]
    [InlineData(HookRunStatus.Faulted, "faulted")]
    [InlineData(HookRunStatus.AwaitingConsent, "awaitingConsent")]
    [InlineData(HookRunStatus.PluginQuarantined, "pluginQuarantined")]
    [InlineData(HookRunStatus.InvalidResponse, "invalidResponse")]
    public void EveryStatusProjectsToItsWireName(HookRunStatus status, string wire)
    {
        var record = new HookRunRecord(
            new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
            "s1",
            null,
            "beforeShellExecution",
            "project:hook",
            "project:plugin",
            HookScope.Project,
            null,
            status,
            "none",
            null,
            TimeSpan.Zero,
            null,
            null,
            null,
            false,
            null,
            null);

        HookRunRecordProjector.Project(record).Status.ShouldBe(wire);
    }
}
