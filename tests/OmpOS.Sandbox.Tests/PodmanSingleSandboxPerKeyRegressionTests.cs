using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests: one idempotency key maps to exactly one Podman sandbox, even under overlapping calls.
/// </summary>
public sealed class PodmanSingleSandboxPerKeyRegressionTests
{
    /// <summary>
    /// Two overlapping provisions with one key create one sandbox; only the owner reports it as newly provisioned.
    /// </summary>
    [Fact]
    public async Task OverlappingProvisionsWithOneKey_CreateExactlyOneSandbox()
    {
        var runner = new FakeProcessRunner();
        runner.SetResult(PodmanTestData.Ok("0123456789ab"));

        // Hold the owner inside `up` until the overlapping call has been issued.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runner.PauseFor = spec => PodmanTestData.HasArgument(spec, "up") ? release.Task : Task.CompletedTask;

        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest();

        var owner = provider.ProvisionAsync(request, ct);
        var overlapping = provider.ProvisionAsync(request with { SessionId = $"session-{Guid.NewGuid():N}" }, ct);
        release.SetResult();

        var results = await Task.WhenAll(owner, overlapping);

        results.Select(static r => r.Descriptor.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
        results.Count(static r => r.NewlyProvisioned).ShouldBe(1);
        runner.Specs.Count(static s => PodmanTestData.HasArgument(s, "up")).ShouldBe(1);
    }

    /// <summary>
    /// A failed provision releases its key so a retry with the same key can provision again.
    /// </summary>
    [Fact]
    public async Task FailedProvision_ReleasesKeyForRetry()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = "container failed to start", TimedOut = false },
            PodmanTestData.Ok());
        runner.SetResult(PodmanTestData.Ok("0123456789ab"));

        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest();

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(request, ct));

        var retried = await provider.ProvisionAsync(request, ct);
        retried.NewlyProvisioned.ShouldBeTrue();
    }
}
