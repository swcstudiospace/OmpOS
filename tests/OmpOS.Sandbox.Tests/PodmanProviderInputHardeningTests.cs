using OmpOS.Sandbox.Abstractions;
using OmpOS.Sandbox.Abstractions.Exceptions;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests for podman provider input hardening: container id validation (WR-01),
/// session id charset (WR-02) and bounded failure messages (WR-05).
/// </summary>
public sealed class PodmanProviderInputHardeningTests
{
    private const string StderrMarker = "leak-marker-xyz";
    private const string ValidContainerId = "0123456789ab";

    public static TheoryData<string> InvalidContainerIds => new()
    {
        new string('a', 11),
        new string('a', 65),
        "0123456789AB",
        "0123456789xy",
        "0123 456789ab",
        "container-id-123",
    };

    public static TheoryData<string> UnsafeSessionIds => new()
    {
        "victim,other=x",
        "label=x",
        "has space",
        "caf\u00e9-session",
        "fullwidth-\uff11",
        new string('s', 129),
    };

    [Theory]
    [InlineData(12)]
    [InlineData(64)]
    public async Task ValidContainerId_ProvisionsAndExecsAgainstLookedUpId(int length)
    {
        var containerId = new string('e', length);
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(PodmanTestData.Ok(), PodmanTestData.Ok(containerId), PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var result = await provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct);

        var events = new List<ExecEvent>();
        await foreach (var evt in provider.ExecAsync(result.Descriptor.Id, PodmanTestData.CreateExecRequest(), ct))
        {
            events.Add(evt);
        }

        events.ShouldContain(static e => e.Type == ExecEventType.Exit);
        runner.Specs[^1].Argv.Contains(containerId, StringComparer.Ordinal).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidContainerIds))]
    public async Task InvalidContainerId_FailsClosedAndTearsDown(string containerId)
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(PodmanTestData.Ok(), PodmanTestData.Ok(containerId), PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        runner.Specs.Any(static s => s.Argv.Contains("down", StringComparer.Ordinal)).ShouldBeTrue();
        runner.Specs.Any(static s => s.Argv.Contains("exec", StringComparer.Ordinal)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public async Task ValidSessionIdBoundary_ReachesLabelFilter(int length)
    {
        var sessionId = new string('s', length);
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(PodmanTestData.Ok(), PodmanTestData.Ok(ValidContainerId), PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest() with { SessionId = sessionId };

        await provider.ProvisionAsync(request, ct);

        runner.Specs.Any(s => s.Argv.Contains($"label=ompos.session={sessionId}", StringComparer.Ordinal)).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(UnsafeSessionIds))]
    public async Task UnsafeSessionId_RejectedBeforeAnyProcessRuns(string sessionId)
    {
        var runner = new FakeProcessRunner();
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;
        var request = PodmanTestData.CreateRequest() with { SessionId = sessionId };

        var exception = await Should.ThrowAsync<PolicyDeniedException>(() => provider.ProvisionAsync(request, ct));

        exception.Policy.ShouldBe("session-id");
        runner.Specs.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpFailure_ExceptionMessageOmitsRawStderr()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            new ProcessResult { ExitCode = 1, Stdout = string.Empty, Stderr = $"{StderrMarker} up failure", TimedOut = false },
            PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        exception.Message.ShouldNotContain(StderrMarker);
    }

    [Fact]
    public async Task LookupFailure_ExceptionMessageOmitsRawStderr()
    {
        var runner = new FakeProcessRunner();
        runner.EnqueueResults(
            PodmanTestData.Ok(),
            new ProcessResult { ExitCode = 2, Stdout = string.Empty, Stderr = $"{StderrMarker} lookup failure", TimedOut = false },
            PodmanTestData.Ok());
        var provider = PodmanTestData.CreateProvider(runner);
        var ct = TestContext.Current.CancellationToken;

        var exception = await Should.ThrowAsync<ProvisionTimeoutException>(() => provider.ProvisionAsync(PodmanTestData.CreateRequest(), ct));

        exception.Message.ShouldNotContain(StderrMarker);
    }
}
