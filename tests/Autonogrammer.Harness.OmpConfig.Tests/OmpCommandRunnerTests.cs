using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpConfig.Tests;

public sealed class OmpCommandRunnerTests
{
    [Fact]
    public async Task PassesEveryArgumentVerbatimWithoutAShell()
    {
        using var fake = new FakeOmpEnvironment();
        var marker = Path.Combine(fake.Root, "shell-ran");
        var hostile = $"a b; touch {marker} $(touch {marker}) `touch {marker}` \"quoted\" 'single'";
        var runner = new OmpCommandRunner(fake.Options());

        var result = await runner.RunAsync(new OmpCommandRequest(["config", "get", hostile]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        fake.Calls().Single().Arguments.ShouldBe(["config", "get", hostile]);
        File.Exists(marker).ShouldBeFalse();
    }

    [Fact]
    public async Task ForcesTheAgentDirectoryAndHomeInTheChildEnvironment()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.Options());

        _ = await runner.RunAsync(new OmpCommandRequest(["models"]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        fake.Calls().Single().Environment.ShouldBe([$"HOME={fake.Home}", $"PI_CODING_AGENT_DIR={fake.AgentDirectory}"]);
    }

    [Fact]
    public async Task WritesStandardInputThenClosesIt()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.Options());
        var input = Encoding.UTF8.GetBytes("{\"line\":\"one\"}\nsecond line without a final newline");

        var result = await runner.RunAsync(
            new OmpCommandRequest(["stdin"]) { StandardInput = input },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        fake.Calls().Single().Stdin.ShouldBe("{\"line\":\"one\"}\nsecond line without a final newline");
    }

    [Fact]
    public async Task ClosesStandardInputAtOnceWhenThereIsNone()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.Options());

        var result = await runner.RunAsync(new OmpCommandRequest(["stdin"]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        fake.Calls().Single().Stdin.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task DiscardsStandardOutputWhenCaptureIsOff()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetTokenExit("acme", 0);
        var runner = new OmpCommandRunner(fake.Options());

        var kept = await runner.RunAsync(new OmpCommandRequest(["token", "acme"]), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var discarded = await runner.RunAsync(
            new OmpCommandRequest(["token", "acme"]) { CaptureStandardOutput = false },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        kept.StandardOutput.ShouldContain("sk-token-output-must-be-discarded", Case.Sensitive);
        discarded.StandardOutput.ShouldBeEmpty();
        discarded.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task KeepsAtMostTheOutputLimitAndSaysSo()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson(new string('x', 100_000));
        var runner = new OmpCommandRunner(fake.Options(maxOutputBytes: 1000));

        var result = await runner.RunAsync(new OmpCommandRequest(["models"]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.OutputTruncated.ShouldBeTrue();
        result.StandardOutput.Length.ShouldBe(1000);
        result.ExitCode.ShouldBe(0);
    }

    [Fact]
    public async Task ReportsANonZeroExitCodeAndStandardError()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.Options());

        var result = await runner.RunAsync(new OmpCommandRequest(["unknown-subcommand"]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.ExitCode.ShouldBe(2);
        result.Succeeded.ShouldBeFalse();
        result.TimedOut.ShouldBeFalse();
    }

    [Fact]
    public async Task ReportsAMissingOmpWithoutStartingAnything()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.OptionsWithoutOmp());

        var result = await runner.RunAsync(new OmpCommandRequest(["models"]), TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.StartFailure.ShouldNotBeNull();
        result.Succeeded.ShouldBeFalse();
        result.ExitCode.ShouldBe(-1);
        fake.Calls().ShouldBeEmpty();
    }

    [Fact]
    public async Task KillsTheProcessWhenTheTimeoutTimerFires()
    {
        using var fake = new FakeOmpEnvironment();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero));
        var runner = new OmpCommandRunner(fake.Options(clock, timeout: TimeSpan.FromSeconds(5)));

        var run = runner.RunAsync(new OmpCommandRequest(["hang"]), TestContext.Current.CancellationToken);
        while (clock.Timers.Count == 0)
        {
            await Task.Yield();
        }

        clock.Timers.Single().Fire();
        var result = await run.ConfigureAwait(true);

        result.TimedOut.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
        result.ExitCode.ShouldBe(-1);
    }

    [Fact]
    public async Task CancellationKillsTheProcessAndThrows()
    {
        using var fake = new FakeOmpEnvironment();
        var runner = new OmpCommandRunner(fake.Options());
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = runner.RunAsync(new OmpCommandRequest(["hang"]), cancellation.Token);
        while (!fake.HasStarted())
        {
            await Task.Yield();
        }

        await cancellation.CancelAsync().ConfigureAwait(true);

        OperationCanceledException? thrown = null;
        try
        {
            _ = await run.ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task ToStringWithholdsOutputWhenTheInvocationCarriedInput()
    {
        using var fake = new FakeOmpEnvironment();
        fake.SetModelsJson("{\"models\":[]} echoed-secret-value");
        var runner = new OmpCommandRunner(fake.Options());

        var plain = await runner.RunAsync(new OmpCommandRequest(["models"]), TestContext.Current.CancellationToken).ConfigureAwait(true);
        var withInput = await runner.RunAsync(
            new OmpCommandRequest(["models"]) { StandardInput = Encoding.UTF8.GetBytes("secret-input") },
            TestContext.Current.CancellationToken).ConfigureAwait(true);

        plain.ToString().ShouldContain("echoed-secret-value", Case.Sensitive);
        withInput.ToString().ShouldNotContain("echoed-secret-value", Case.Sensitive);
        withInput.ToString().ShouldNotContain("secret-input", Case.Sensitive);
    }

    [Fact]
    public void RequestToStringNeverShowsStandardInput()
    {
        var request = new OmpCommandRequest(["auth", "import", "-"]) { StandardInput = Encoding.UTF8.GetBytes("sk-never-shown") };

        request.ToString().ShouldBe("omp auth import -");
    }

    [Fact]
    public void RejectsANonPositiveOutputLimit()
    {
        using var fake = new FakeOmpEnvironment();

        Should.Throw<ArgumentOutOfRangeException>(() => new OmpCommandRunner(fake.Options(maxOutputBytes: 0)));
    }

    [Fact]
    public void RejectsANonPositiveTimeout()
    {
        using var fake = new FakeOmpEnvironment();

        Should.Throw<ArgumentOutOfRangeException>(() => new OmpCommandRunner(fake.Options(timeout: TimeSpan.Zero)));
    }
}
