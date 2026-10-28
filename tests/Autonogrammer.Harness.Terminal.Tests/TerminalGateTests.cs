using System.ComponentModel;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Terminal.Tests;

/// <summary>
/// Verifies the ADE-14 approval rules through consumer-visible outcomes: spawn counts, arguments, working directory,
/// final status and captured results.
/// </summary>
public sealed class TerminalGateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void DefaultSettingsHaveAutoRunOff()
    {
        new TerminalSettings().AutoRunEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task DefaultSettingsKeepProposalPendingAndSpawnNothing()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);

        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["build"], string.Empty), Token);

        proposed.Status.ShouldBe(CommandStatus.Pending);
        proposed.ExitCode.ShouldBeNull();
        gate.Commands.Select(command => command.Id).ShouldBe([proposed.Id]);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task ApproveSpawnsOnceWithDeclaredArgumentsAndWorkingDirectory()
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSubdirectory("src");
        var runner = new FakeProcessRunner { Outcome = new ProcessOutcome(0, "ok", string.Empty) };
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(
            new CommandProposal("git", ["status", "--short", "path with spaces", string.Empty], "src"),
            Token);

        var approved = await gate.ApproveAsync(proposed.Id, Token);

        runner.SpawnCount.ShouldBe(1);
        var request = runner.Requests.Single();
        request.FileName.ShouldBe("git");
        request.Arguments.ShouldBe(["status", "--short", "path with spaces", string.Empty]);
        request.WorkingDirectory.ShouldBe(Path.Combine(gate.WorkspaceRoot, "src"));
        approved.Status.ShouldBe(CommandStatus.Completed);
        approved.StandardOutput.ShouldBe("ok");
    }

    [Fact]
    public async Task CommandWithoutWorkingDirectoryStartsInTheWorkspaceRoot()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"]), Token);

        await gate.ApproveAsync(proposed.Id, Token);

        runner.Requests.Single().WorkingDirectory.ShouldBe(gate.WorkspaceRoot);
    }

    [Fact]
    public async Task ApprovingTheSameProposalTwiceSpawnsOnce()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], string.Empty), Token);
        await gate.ApproveAsync(proposed.Id, Token);

        await Should.ThrowAsync<InvalidOperationException>(() => gate.ApproveAsync(proposed.Id, Token));

        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task DeclineEndsDeclinedAndNeverSpawns()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("rm", ["-rf", "build"], string.Empty), Token);

        var declined = gate.Decline(proposed.Id);

        declined.Status.ShouldBe(CommandStatus.Declined);
        gate.Commands.Single().Status.ShouldBe(CommandStatus.Declined);
        await Should.ThrowAsync<InvalidOperationException>(() => gate.ApproveAsync(proposed.Id, Token));
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task DeclinedProposalCannotBeDeclinedAgainOrRun()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], string.Empty), Token);
        gate.Decline(proposed.Id);

        Should.Throw<InvalidOperationException>(() => gate.Decline(proposed.Id));
        await Should.ThrowAsync<InvalidOperationException>(() => gate.ApproveAsync(proposed.Id, Token));
        runner.SpawnCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("src/../..")]
    [InlineData("nested/../../")]
    public async Task WorkingDirectoryEscapingTheRootIsRejectedWithZeroSpawns(string workingDirectory)
    {
        using var workspace = new TempWorkspace();
        workspace.CreateSubdirectory("nested");
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);

        await Should.ThrowAsync<CommandProposalRejectedException>(() =>
            gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], workingDirectory), Token));

        gate.Commands.ShouldBeEmpty();
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task AbsoluteWorkingDirectoryOutsideTheRootIsRejectedWithZeroSpawns()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var outside = Path.GetDirectoryName(gate.WorkspaceRoot)!;

        await Should.ThrowAsync<CommandProposalRejectedException>(() =>
            gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], outside), Token));

        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task SymbolicLinkInsideTheRootThatPointsOutsideIsRejectedWithZeroSpawns()
    {
        using var workspace = new TempWorkspace();
        var outside = Path.GetDirectoryName(workspace.Path)!;
        Directory.CreateSymbolicLink(Path.Combine(workspace.Path, "escape"), outside);
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);

        await Should.ThrowAsync<CommandProposalRejectedException>(() =>
            gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], "escape"), Token));

        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task SymbolicLinkInsideTheRootThatPointsInsideRunsInItsPhysicalTarget()
    {
        using var workspace = new TempWorkspace();
        var target = workspace.CreateSubdirectory("real");
        Directory.CreateSymbolicLink(Path.Combine(workspace.Path, "alias"), target);
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], "alias"), Token);

        await gate.ApproveAsync(proposed.Id, Token);

        runner.Requests.Single().WorkingDirectory.ShouldBe(Path.Combine(gate.WorkspaceRoot, "real"));
    }

    [Fact]
    public async Task MissingWorkingDirectoryIsRejectedWithZeroSpawns()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);

        await Should.ThrowAsync<CommandProposalRejectedException>(() =>
            gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], "does-not-exist"), Token));

        gate.Commands.ShouldBeEmpty();
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task AutoRunEnabledRunsWithoutAPrompt()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner { Outcome = new ProcessOutcome(0, "auto", string.Empty) };
        var gate = new TerminalGate(workspace.Path, new TerminalSettings { AutoRunEnabled = true }, runner);

        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], string.Empty), Token);

        runner.SpawnCount.ShouldBe(1);
        proposed.Status.ShouldBe(CommandStatus.Completed);
        proposed.StandardOutput.ShouldBe("auto");
        gate.Commands.Single().Status.ShouldBe(CommandStatus.Completed);
    }

    [Fact]
    public async Task RunnerExitCodeAndOutputAreReported()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner { Outcome = new ProcessOutcome(3, "hello\n", "boom\n") };
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["build"], string.Empty), Token);

        var finished = await gate.ApproveAsync(proposed.Id, Token);

        finished.Status.ShouldBe(CommandStatus.Failed);
        finished.ExitCode.ShouldBe(3);
        finished.StandardOutput.ShouldBe("hello\n");
        finished.StandardError.ShouldBe("boom\n");
        finished.StandardOutputTruncated.ShouldBeFalse();
        finished.StandardErrorTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task OutputIsBoundedToTheConfiguredSize()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner { Outcome = new ProcessOutcome(0, new string('x', 25), "e") };
        var gate = new TerminalGate(
            workspace.Path,
            new TerminalSettings { MaxOutputCharacters = 10 },
            runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("dotnet", ["--version"], string.Empty), Token);

        var finished = await gate.ApproveAsync(proposed.Id, Token);

        finished.StandardOutput.ShouldBe(new string('x', 10));
        finished.StandardOutputTruncated.ShouldBeTrue();
        finished.StandardError.ShouldBe("e");
        finished.StandardErrorTruncated.ShouldBeFalse();
    }

    [Fact]
    public async Task RunnerFailureIsRecordedAsFailedWithoutExitCode()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner { Failure = new Win32Exception(2, "no such file") };
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);
        var proposed = await gate.ProposeAsync(new CommandProposal("missing-tool", [], string.Empty), Token);

        var finished = await gate.ApproveAsync(proposed.Id, Token);

        finished.Status.ShouldBe(CommandStatus.Failed);
        finished.ExitCode.ShouldBeNull();
        finished.StandardError.ShouldContain("no such file");
        runner.SpawnCount.ShouldBe(1);
        Should.Throw<InvalidOperationException>(() => gate.Decline(proposed.Id));
    }

    [Fact]
    public async Task BlankCommandIsRejectedWithZeroSpawns()
    {
        using var workspace = new TempWorkspace();
        var runner = new FakeProcessRunner();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), runner);

        await Should.ThrowAsync<ArgumentException>(() =>
            gate.ProposeAsync(new CommandProposal("  ", [], string.Empty), Token));

        gate.Commands.ShouldBeEmpty();
        runner.SpawnCount.ShouldBe(0);
    }
}
