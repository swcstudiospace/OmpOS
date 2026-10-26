using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests;

/// <summary>
/// Verifies the gate a head calls before it starts an agent run: a failed or unapproved setup surfaces its reason and
/// starts no run, and the variables of a passing setup reach the started run's environment (ADE-19).
/// </summary>
public sealed class SetupRunGateTests
{
    private const string ShellPath = "/bin/sh";
    private const string FalsePath = "/bin/false";
    private const string SetupFile = ".ompos/setup.json";

    [Fact]
    public async Task AFailedSetupSurfacesItsReasonAndStartsNoRun()
    {
        HostCommand.Require(ShellPath);
        HostCommand.Require(FalsePath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, """
            {
              "steps": [
                { "command": "/bin/sh", "arguments": ["-c", "echo first > first.txt"] },
                { "command": "/bin/false" }
              ],
              "environment": { "OMPOS_SETUP_MARKER": "ready" }
            }
            """);
        var starts = 0;

        var result = await SetupRunGate.RunAsync(
                workspace.Root,
                new MemorySetupConsentStore(),
                ScriptedSetupConsentPrompt.Approving(),
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.RunStarted.ShouldBeFalse();
        result.Environment.ShouldBeEmpty();
        var failure = result.Failure.ShouldNotBeNull();
        failure.Status.ShouldBe(SetupStatus.Failed);
        failure.FailedStepIndex.ShouldBe(1);
        failure.FailureReason.ShouldContain("setup step 2");
        failure.FailureReason.ShouldContain(FalsePath);
        starts.ShouldBe(0);
    }

    [Fact]
    public async Task ASetupTheUserDidNotApproveStartsNoRunAndRunsNoStep()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, """
            {
              "steps": [
                { "command": "/bin/sh", "arguments": ["-c", "echo ran > ran.txt"] }
              ]
            }
            """);
        var store = new MemorySetupConsentStore();
        var starts = 0;

        var result = await SetupRunGate.RunAsync(
                workspace.Root,
                store,
                ScriptedSetupConsentPrompt.Refusing(),
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.RunStarted.ShouldBeFalse();
        var failure = result.Failure.ShouldNotBeNull();
        failure.Status.ShouldBe(SetupStatus.ConsentRefused);
        failure.FailureReason.ShouldContain("the user pressed Deny");
        starts.ShouldBe(0);
        store.RecordCount.ShouldBe(0);
        File.Exists(Path.Combine(workspace.RootPath, "ran.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task SetupVariablesReachTheStartedRunEnvironment()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(SetupFile, """
            {
              "steps": [
                { "command": "/bin/sh", "arguments": ["-c", "test \"$OMPOS_SETUP_MARKER\" = ready"] }
              ],
              "environment": { "OMPOS_SETUP_MARKER": "ready", "OMPOS_SETUP_SECOND": "two" }
            }
            """);
        IReadOnlyDictionary<string, string>? runEnvironment = null;
        var starts = 0;

        var result = await SetupRunGate.RunAsync(
                workspace.Root,
                new MemorySetupConsentStore(),
                ScriptedSetupConsentPrompt.Approving(),
                (environment, _) =>
                {
                    starts++;
                    runEnvironment = environment;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.RunStarted.ShouldBeTrue();
        result.Failure.ShouldBeNull();
        starts.ShouldBe(1);
        runEnvironment.ShouldNotBeNull();
        runEnvironment["OMPOS_SETUP_MARKER"].ShouldBe("ready");
        runEnvironment["OMPOS_SETUP_SECOND"].ShouldBe("two");
        result.Environment["OMPOS_SETUP_MARKER"].ShouldBe("ready");
        result.Environment["OMPOS_SETUP_SECOND"].ShouldBe("two");
    }
}
