using Shouldly;
using Xunit;

namespace OmpOS.Harness.Terminal.Tests;

/// <summary>
/// Verifies the approval gate with the pseudo-terminal runner: nothing spawns before approval, a declined proposal never
/// reaches the terminal, and an approved command runs in the workspace root and reports its result.
/// </summary>
public sealed class PtyTerminalGateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApprovedCommandRunsInThePseudoTerminalAndRecordsItsResult()
    {
        using var workspace = new TempWorkspace();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), new PtyProcessRunner());

        var proposed = await gate.ProposeAsync(new CommandProposal("/bin/sh", ["-c", "printf hello"]), Token);
        var finished = await gate.ApproveAsync(proposed.Id, Token);

        proposed.Status.ShouldBe(CommandStatus.Pending);
        finished.Status.ShouldBe(CommandStatus.Completed);
        finished.ExitCode.ShouldBe(0);
        finished.StandardOutput.ShouldBe("hello");
    }

    [Fact]
    public async Task PendingProposalSpawnsNothingUntilItIsApproved()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), new PtyProcessRunner(sink));

        var proposed = await gate.ProposeAsync(new CommandProposal("/bin/sh", ["-c", "printf hello"]), Token);
        sink.Text.ShouldBeEmpty();

        await gate.ApproveAsync(proposed.Id, Token);
        sink.Text.ShouldBe("hello");
    }

    [Fact]
    public async Task ADeclinedProposalNeverReachesTheTerminal()
    {
        using var workspace = new TempWorkspace();
        var sink = new RecordingSink();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), new PtyProcessRunner(sink));

        var proposed = await gate.ProposeAsync(new CommandProposal("/bin/sh", ["-c", "printf hello"]), Token);
        var declined = gate.Decline(proposed.Id);

        declined.Status.ShouldBe(CommandStatus.Declined);
        sink.Text.ShouldBeEmpty();
        gate.Commands.Single().Status.ShouldBe(CommandStatus.Declined);
    }

    [Fact]
    public async Task AnApprovedCommandWithNoWorkingDirectoryRunsInTheWorkspaceRoot()
    {
        using var workspace = new TempWorkspace();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), new PtyProcessRunner());

        var proposed = await gate.ProposeAsync(new CommandProposal("/bin/sh", ["-c", "pwd -P"]), Token);
        var finished = await gate.ApproveAsync(proposed.Id, Token);

        finished.WorkingDirectory.ShouldBe(gate.WorkspaceRoot);
        finished.StandardOutput.Trim().ShouldBe(gate.WorkspaceRoot);
    }

    [Fact]
    public async Task AnApprovedCommandThatExitsNonZeroIsRecordedAsFailed()
    {
        using var workspace = new TempWorkspace();
        var gate = new TerminalGate(workspace.Path, new TerminalSettings(), new PtyProcessRunner());

        var proposed = await gate.ProposeAsync(new CommandProposal("/bin/sh", ["-c", "printf oops; exit 4"]), Token);
        var finished = await gate.ApproveAsync(proposed.Id, Token);

        finished.Status.ShouldBe(CommandStatus.Failed);
        finished.ExitCode.ShouldBe(4);
        finished.StandardOutput.ShouldBe("oops");
    }
}
