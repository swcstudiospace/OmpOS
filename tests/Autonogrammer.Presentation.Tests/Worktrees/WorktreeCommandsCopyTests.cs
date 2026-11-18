using Autonogrammer.Presentation.Worktrees;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Worktrees;

/// <summary>Verifies the sentence a worktree deletion asks, so a run whose changes are unknown is never counted as having none.</summary>
public sealed class WorktreeCommandsCopyTests
{
    [Fact]
    public void ADeletionOfKnownRunsCountsTheirChangedFiles()
    {
        var text = WorktreeCommandsCopy.DeleteConfirmation([new WorktreeRunInfo("a", "/wt/a", "b/a", 3, null), new WorktreeRunInfo("b", "/wt/b", "b/b", 1, null)]);

        text.ShouldBe("Delete worktrees a, b? 4 changed files are lost unless you applied them first.");
    }

    [Fact]
    public void ARunWhoseChangesAreUnknownIsNamedAndNotCountedAsZeroChangedFiles()
    {
        var text = WorktreeCommandsCopy.DeleteConfirmation(
        [
            new WorktreeRunInfo("a", "/wt/a", "b/a", 2, null),
            new WorktreeRunInfo("b", "/wt/b", "b/b", 0, "git status failed"),
        ]);

        text.ShouldBe("Delete worktrees a, b? 2 changed files are lost unless you applied it first. b: changes unknown. Their changes cannot be verified as applied.");
    }

    [Fact]
    public void ADeletionOfOnlyUnknownRunsSaysTheirChangesCannotBeVerified()
    {
        var text = WorktreeCommandsCopy.DeleteConfirmation([new WorktreeRunInfo("a", "/wt/a", "b/a", 0, "git status failed")]);

        text.ShouldBe("Delete worktree a? a: changes unknown. Their changes cannot be verified as applied.");
    }
}
