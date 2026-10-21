using System.Diagnostics;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Regression tests for the production process runner timeout guarantees (CR-03, WR-11).
/// </summary>
public sealed class ProcessRunnerDrainTests
{
    private static ProcessSpec Spec(IReadOnlyList<string> argv, TimeSpan timeout)
    {
        return new ProcessSpec
        {
            Argv = argv,
            WorkingDirectory = "/",
            Environment = new Dictionary<string, string>(StringComparer.Ordinal),
            Timeout = timeout,
        };
    }

    /// <summary>
    /// A grandchild that inherits the pipes cannot hang the caller: the post-exit drain is bounded (CR-03).
    /// </summary>
    [Fact]
    public async Task GrandchildHoldingPipe_DrainIsBounded()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Grandchild pipe test requires a POSIX shell.");
        var ct = TestContext.Current.CancellationToken;
        Assert.SkipUnless(await RealProcessRunner.CheckCommandExistsAsync("sh", ct), "sh not available on PATH.");

        var runner = new RealProcessRunner();
        var elapsed = Stopwatch.StartNew();
        var result = await runner.RunAsync(Spec(["/bin/sh", "-c", "sleep 30 &"], TimeSpan.FromSeconds(20)), ct);
        elapsed.Stop();

        result.TimedOut.ShouldBeTrue();

        // spec.Timeout is 20s but sh exits immediately; the drain budget (~2s) must bound the call.
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }

    /// <summary>
    /// Arguments travel intact without manual quoting: spaces, quotes, and tabs survive (WR-11).
    /// </summary>
    [Fact]
    public async Task ArgumentsWithSpecialCharacters_PassThroughIntact()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Argument passthrough test requires /bin/echo.");
        var ct = TestContext.Current.CancellationToken;

        var runner = new RealProcessRunner();
        var result = await runner.RunAsync(
            Spec(["/bin/echo", "hello world", "a\"b", "tab\there"], TimeSpan.FromSeconds(30)),
            ct);
        result.TimedOut.ShouldBeFalse();
        result.ExitCode.ShouldBe(0);
        result.Stdout.ShouldContain("hello world");
        result.Stdout.ShouldContain("a\"b");
        result.Stdout.ShouldContain("tab\there");
        result.Stdout.ShouldNotContain("\"hello world\"");
    }
}
