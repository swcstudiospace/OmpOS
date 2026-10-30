using System.Runtime.Versioning;
using System.Text;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the production hook command runner against real <c>/bin/sh</c> processes: payload on standard input, shell and no
/// shell forms, the exact environment, the working directory, bounded output with truncation reported, tree kill and start
/// failure classification. These tests need a POSIX shell and are skipped on Windows.
/// </summary>
public sealed class SystemHookCommandRunnerTests
{
    private static readonly IReadOnlyDictionary<string, string> BasicEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PATH"] = "/usr/local/bin:/usr/bin:/bin",
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static HookInvocation Shell(
        string command,
        string workingDirectory,
        string stdin = "",
        IReadOnlyDictionary<string, string>? environment = null,
        int maxOut = 1024 * 1024,
        int maxErr = 64 * 1024) =>
        new(command, null, HookShell.PosixSh, workingDirectory, environment ?? BasicEnvironment, Encoding.UTF8.GetBytes(stdin), maxOut, maxErr);

    private static void RequirePosix() => Assert.SkipWhen(OperatingSystem.IsWindows(), "These tests start /bin/sh.");

    [Fact]
    public async Task ThePayloadIsWrittenToStandardInputAndClosed()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("cat", directory.Path, """{ "hook_event_name": "stop" }"""), Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutput.ShouldBe("""{ "hook_event_name": "stop" }""");
        outcome.StandardInputDelivered.ShouldBeTrue();
        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.None);
    }

    [Fact]
    public async Task AShellStringMayUsePipesAndRedirection()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("echo hello | tr a-z A-Z; echo oops 1>&2", directory.Path), Token);

        outcome.StandardOutput.ShouldBe("HELLO\n");
        outcome.StandardError.ShouldBe("oops\n");
    }

    [Fact]
    public async Task TheArgsFormReachesTheProgramVerbatimWithoutAShell()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var invocation = new HookInvocation(
            null,
            ["/bin/sh", "-c", "printf %s \"$1\"", "sh", "two words; $(touch pwned) `id`"],
            HookShell.None,
            directory.Path,
            BasicEnvironment,
            ReadOnlyMemory<byte>.Empty,
            4096,
            4096);

        var outcome = await new SystemHookCommandRunner().RunAsync(invocation, Token);

        outcome.StandardOutput.ShouldBe("two words; $(touch pwned) `id`");
        File.Exists(directory.PathOf("pwned")).ShouldBeFalse();
    }

    [Fact]
    public async Task ExitCodeIsReportedAndBothStreamsAreCaptured()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("echo out; echo err 1>&2; exit 3", directory.Path), Token);

        outcome.ExitCode.ShouldBe(3);
        outcome.StandardOutput.ShouldBe("out\n");
        outcome.StandardError.ShouldBe("err\n");
        outcome.StandardOutputTruncated.ShouldBeFalse();
        outcome.StandardErrorTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task TheProcessStartsInTheWorkingDirectory()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var working = directory.PathOf("hook-cwd");
        Directory.CreateDirectory(working);

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("basename \"$(pwd -P)\"", working), Token);

        outcome.StandardOutput.Trim().ShouldBe("hook-cwd");
    }

    [Fact]
    public async Task TheChildSeesOnlyTheInvocationEnvironmentAndNeverTheParentSecret()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var canary = "AUTONOGRAMMER_HOOKS_TEST_CANARY_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(canary, "super-secret-value");
        try
        {
            var environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin", ["FOO"] = "bar" };

            var secret = await new SystemHookCommandRunner().RunAsync(Shell($"printenv {canary}", directory.Path, environment: environment), Token);
            var wanted = await new SystemHookCommandRunner().RunAsync(Shell("printenv FOO", directory.Path, environment: environment), Token);
            var everything = await new SystemHookCommandRunner().RunAsync(Shell("env", directory.Path, environment: environment), Token);

            secret.ExitCode.ShouldBe(1);
            secret.StandardOutput.ShouldBeEmpty();
            wanted.StandardOutput.ShouldBe("bar\n");
            everything.StandardOutput.ShouldNotContain("super-secret-value", Case.Sensitive);
            everything.StandardOutput.ShouldNotContain(canary, Case.Sensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable(canary, null);
        }
    }

    [Fact]
    public async Task TheDispatcherEnvironmentBuiltFromTheRealParentScrubsASecretEndToEnd()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var canary = "AUTONOGRAMMER_HOOKS_TEST_SECRET_" + Guid.NewGuid().ToString("N");
        var plain = "HOOKS_TEST_PLAIN_SECRET_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(canary, "reserved-secret");
        Environment.SetEnvironmentVariable(plain, "plain-secret");
        try
        {
            var hook = HookFixtures.Hook(workingDirectory: directory.Path);
            var environment = HookEnvironment.Build(HookEnvironment.CaptureParent(), false, hook, HookFixtures.Session(directory.Path), "preToolUse");

            var outcome = await new SystemHookCommandRunner().RunAsync(Shell("env", directory.Path, environment: environment), Token);

            outcome.StandardOutput.ShouldNotContain("reserved-secret", Case.Sensitive);
            outcome.StandardOutput.ShouldNotContain("plain-secret", Case.Sensitive);
            outcome.StandardOutput.ShouldContain("CURSOR_PROJECT_DIR=" + directory.Path, Case.Sensitive);
        }
        finally
        {
            Environment.SetEnvironmentVariable(canary, null);
            Environment.SetEnvironmentVariable(plain, null);
        }
    }

    [Fact]
    public async Task AHookThatNeverReadsItsInputDoesNotHangTheHost()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var payload = new string('x', 4 * 1024 * 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("exit 0", directory.Path, payload), Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardInputDelivered.ShouldBeFalse();
    }

    [Fact]
    public async Task ALargePayloadIsDeliveredWithoutDeadlockWhileTheHookReadsIt()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var payload = new string('y', 3 * 1024 * 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("wc -c", directory.Path, payload), Token);

        outcome.StandardOutput.Trim().ShouldBe((3 * 1024 * 1024).ToString(System.Globalization.CultureInfo.InvariantCulture));
        outcome.StandardInputDelivered.ShouldBeTrue();
    }

    [Fact]
    public async Task OutputBeyondTheCapIsDrainedAndReportedAsTruncatedNotAsAFailure()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var payload = new string('z', 2 * 1024 * 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("printf '{\"permission\":\"allow\"}'; cat", directory.Path, payload, maxOut: 64 * 1024), Token);

        outcome.ExitCode.ShouldBe(0);
        outcome.StandardOutputTruncated.ShouldBeTrue();
        outcome.StandardOutput.Length.ShouldBe(64 * 1024);
        outcome.StandardOutput.ShouldStartWith("""{"permission":"allow"}""", Case.Sensitive);
        outcome.StandardErrorTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task AValidResponseThatFitsTheCapIsNeverMarkedTruncated()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("printf '{\"permission\":\"deny\"}'", directory.Path, maxOut: 64), Token);

        outcome.StandardOutputTruncated.ShouldBeFalse();
        HookOutputParser.Parse(outcome.StandardOutput, HookEvent.PreToolUse).Verdict.ShouldBe(HookVerdict.Deny);
    }

    [Fact]
    public async Task ACutInTheMiddleOfACharacterDropsTheCharacterInsteadOfShowingAReplacement()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("printf 'ab\\303\\251cd'", directory.Path, maxOut: 3), Token);

        outcome.StandardOutputTruncated.ShouldBeTrue();
        outcome.StandardOutput.ShouldBe("ab");
    }

    [Fact]
    public async Task CancellationKillsTheWholeProcessTreeAndThrows()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var ready = directory.PathOf("grandchild-ready");
        var finished = directory.PathOf("grandchild-finished");
        var invocation = Shell($"(touch '{ready}'; sleep 3; touch '{finished}') & wait", directory.Path);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var running = new SystemHookCommandRunner().RunAsync(invocation, cancellation.Token);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(ready) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), Token);
        }

        File.Exists(ready).ShouldBeTrue();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () => await running.WaitAsync(CancellationToken.None).ConfigureAwait(true));
        await Task.Delay(TimeSpan.FromSeconds(4), Token);
        File.Exists(finished).ShouldBeFalse();
    }

    [Fact]
    public async Task AMissingExecutableOfTheArgsFormIsClassifiedAsNotFound()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var invocation = new HookInvocation(null, [directory.PathOf("no-such-hook")], HookShell.None, directory.Path, BasicEnvironment, ReadOnlyMemory<byte>.Empty, 1024, 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(invocation, Token);

        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.ExecutableNotFound);
        outcome.ExitCode.ShouldBe(-1);
        outcome.SpawnMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [SupportedOSPlatform("linux")]
    [Fact]
    public async Task ANonExecutableFileOfTheArgsFormIsClassifiedAsAccessDenied()
    {
        RequirePosix();
        using var directory = new TempDirectory();
        var script = directory.PathOf("not-executable.sh");
        await File.WriteAllTextAsync(script, "#!/bin/sh\nexit 0\n", Token);
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var invocation = new HookInvocation(null, [script], HookShell.None, directory.Path, BasicEnvironment, ReadOnlyMemory<byte>.Empty, 1024, 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(invocation, Token);

        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.AccessDenied);
    }

    [Fact]
    public async Task AMissingWorkingDirectoryIsReportedWithoutStartingAnything()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("touch started", directory.PathOf("missing")), Token);

        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.WorkingDirectoryMissing);
        File.Exists(directory.PathOf("started")).ShouldBeFalse();
    }

    [Fact]
    public async Task ACommandThatIsNotFoundByTheShellIsAnOrdinaryExitCode()
    {
        RequirePosix();
        using var directory = new TempDirectory();

        var outcome = await new SystemHookCommandRunner().RunAsync(Shell("./definitely-missing-script.sh", directory.Path), Token);

        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.None);
        outcome.ExitCode.ShouldBe(127);
    }

    [Fact]
    public async Task AnInvocationWithoutACommandOrArgumentsIsRejectedAsAStartFailure()
    {
        using var directory = new TempDirectory();
        var invocation = new HookInvocation(null, null, HookShell.None, directory.Path, BasicEnvironment, ReadOnlyMemory<byte>.Empty, 1024, 1024);

        var outcome = await new SystemHookCommandRunner().RunAsync(invocation, Token);

        outcome.SpawnFailure.ShouldBe(HookSpawnFailure.Other);
    }
}
