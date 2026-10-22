using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests;

/// <summary>
/// Verifies that the setup file runs before an agent run, that its variables reach the run, and that a failing
/// step stops the run before the agent starts (ADE-19).
/// </summary>
public sealed class SetupRunnerTests
{
    private const string ShellPath = "/bin/sh";
    private const string FalsePath = "/bin/false";

    [Fact]
    public async Task PassingSetupExposesItsVariableToTheRun()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write("tools/marker.txt", "ok\n");
        workspace.Write(".ompos/setup.json", """
            {
              "steps": [
                { "command": "/bin/sh", "arguments": ["-c", "test \"$OMPOS_SETUP_MARKER\" = ready && test -f marker.txt"], "workingDirectory": "tools" }
              ],
              "environment": { "OMPOS_SETUP_MARKER": "ready" }
            }
            """);
        IReadOnlyDictionary<string, string>? runEnvironment = null;
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (environment, _) =>
                {
                    starts++;
                    runEnvironment = environment;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Passed);
        result.FailedStepIndex.ShouldBeNull();
        starts.ShouldBe(1);
        runEnvironment.ShouldNotBeNull();
        runEnvironment["OMPOS_SETUP_MARKER"].ShouldBe("ready");
        result.Environment["OMPOS_SETUP_MARKER"].ShouldBe("ready");
    }

    [Fact]
    public async Task FailingStepMarksRunFailedAndAgentNeverStarts()
    {
        HostCommand.Require(ShellPath);
        HostCommand.Require(FalsePath);
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/setup.json", """
            {
              "steps": [
                { "command": "/bin/sh", "arguments": ["-c", "echo first > first.txt"] },
                { "command": "/bin/false" },
                { "command": "/bin/sh", "arguments": ["-c", "echo third > third.txt"] }
              ]
            }
            """);
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Failed);
        result.FailedStepIndex.ShouldBe(1);
        result.Environment.ShouldBeEmpty();
        starts.ShouldBe(0);
        File.Exists(Path.Combine(workspace.RootPath, "first.txt")).ShouldBeTrue();
        File.Exists(Path.Combine(workspace.RootPath, "third.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task MissingCommandMarksRunFailedAndAgentNeverStarts()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/setup.json", """
            { "steps": [ { "command": "/nonexistent/ompos-missing-command" } ] }
            """);
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Failed);
        result.FailedStepIndex.ShouldBe(0);
        starts.ShouldBe(0);
    }

    [Fact]
    public async Task MalformedSetupFileFailsRunWithoutStartingAgent()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/setup.json", "{ \"steps\": \"not-a-list\" }");
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Failed);
        result.FailedStepIndex.ShouldBeNull();
        starts.ShouldBe(0);
    }

    [Fact]
    public async Task StepWorkingDirectoryThatEscapesRootFailsRunWithoutStartingAgent()
    {
        HostCommand.Require(ShellPath);
        using var workspace = new TempWorkspace();
        workspace.Write(".ompos/setup.json", """
            { "steps": [ { "command": "/bin/sh", "arguments": ["-c", "true"], "workingDirectory": "../.." } ] }
            """);
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Failed);
        starts.ShouldBe(0);
    }

    [Fact]
    public async Task WorkspaceWithoutSetupFileRunsAgentWithEmptyEnvironment()
    {
        using var workspace = new TempWorkspace();
        var starts = 0;

        var result = await SetupRunner.RunAsync(
                workspace.Root,
                (_, _) =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                TestContext.Current.CancellationToken)
            .ConfigureAwait(true);

        result.Status.ShouldBe(SetupStatus.Passed);
        result.Environment.ShouldBeEmpty();
        starts.ShouldBe(1);
    }
}
