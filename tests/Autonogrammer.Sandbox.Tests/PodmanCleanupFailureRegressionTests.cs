using Microsoft.Extensions.Logging;
using Autonogrammer.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests: a failed best-effort cleanup after a failed provision is reported, never silently discarded.
/// </summary>
public sealed class PodmanCleanupFailureRegressionTests
{
    /// <summary>
    /// A partial teardown that exits non-zero is logged at warning level with the project name and exit code.
    /// </summary>
    [Fact]
    public async Task PartialTeardownExitFailure_IsReportedAtWarning()
    {
        var logger = new CapturingLogger<LocalPodmanComposeProvider>();
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false },
            new ProcessResult { ExitCode = 2, Stdout = string.Empty, Stderr = string.Empty, TimedOut = false });
        var provider = PodmanTestData.CreateProvider(runner, logger);

        var ct = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        var projectName = ProjectNameOf(runner);
        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains(projectName, StringComparison.Ordinal)
            && e.Message.Contains("exit 2", StringComparison.Ordinal));
    }

    /// <summary>
    /// A partial teardown that times out is logged at warning level with the project name.
    /// </summary>
    [Fact]
    public async Task PartialTeardownTimeout_IsReportedAtWarning()
    {
        var logger = new CapturingLogger<LocalPodmanComposeProvider>();
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false },
            new ProcessResult { ExitCode = 124, Stdout = string.Empty, Stderr = string.Empty, TimedOut = true });
        var provider = PodmanTestData.CreateProvider(runner, logger);

        var ct = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        var projectName = ProjectNameOf(runner);
        logger.Entries.ShouldContain(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains(projectName, StringComparison.Ordinal)
            && e.Message.Contains("timed out", StringComparison.Ordinal));
    }

    /// <summary>
    /// A partial teardown that succeeds logs no warning, so clean failures stay quiet.
    /// </summary>
    [Fact]
    public async Task SuccessfulPartialTeardown_LogsNoWarning()
    {
        var logger = new CapturingLogger<LocalPodmanComposeProvider>();
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false },
            PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner, logger);

        var ct = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        logger.Entries.Where(static e => e.Level == LogLevel.Warning).ShouldBeEmpty();
    }

    private static string ProjectNameOf(FakeProcessRunner runner)
    {
        var up = runner.Specs.First(static s => PodmanTestData.HasArgument(s, "up")).Argv.ToList();
        return up[up.IndexOf("-p") + 1];
    }
}
