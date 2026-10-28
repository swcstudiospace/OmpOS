using System.ComponentModel;
using System.Text;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Composition.Mentions;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Presentation.Mentions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition.Mentions;

/// <summary>The git diffs of a workspace read through the real <c>git</c> in a temporary repository, and the failures that must read as plain sentences.</summary>
public sealed class GitMentionDiffSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void NeedGit() => Assert.SkipUnless(GitCli.IsOnPath, "git is not on the PATH.");

    private static async Task<MentionWorkspaceTree> CommittedRepositoryAsync(string branch = "main")
    {
        var tree = new MentionWorkspaceTree();
        await GitCli.InitAsync(tree.Root, branch, Token).ConfigureAwait(true);
        tree.Write("a.txt", "old\n");
        tree.Write("keep.txt", "keep\n");
        await GitCli.CommitAllAsync(tree.Root, "first", Token).ConfigureAwait(true);
        return tree;
    }

    /// <summary>The working summary counts a modified tracked file and an untracked file, and names no base branch.</summary>
    [Fact]
    public async Task TheWorkingSummaryCountsTrackedAndUntrackedChanges()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync().ConfigureAwait(true);
        tree.Write("a.txt", "new\n");
        tree.Write("c.txt", "brand new\n");
        var source = new GitMentionDiffSource(tree.Root);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Working, Token).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(true, 2, null, null));
    }

    /// <summary>The working diff holds the change to the tracked file and the untracked file as a new file; an ignored file is not in it.</summary>
    [Fact]
    public async Task TheWorkingDiffHoldsTrackedChangesAndUntrackedFiles()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync().ConfigureAwait(true);
        tree.Write(".gitignore", "ignored.txt\n");
        await GitCli.CommitAllAsync(tree.Root, "ignore", Token).ConfigureAwait(true);
        tree.Write("a.txt", "new\n");
        tree.Write("c.txt", "brand new\n");
        tree.Write("ignored.txt", "never shown\n");
        var source = new GitMentionDiffSource(tree.Root);

        var content = await source.ReadAsync(MentionGitDiffKind.Working, 100_000, Token).ConfigureAwait(true);

        content.Truncated.ShouldBeFalse();
        content.Text.ShouldContain("diff --git a/a.txt b/a.txt", Case.Sensitive);
        content.Text.ShouldContain("-old\n+new\n", Case.Sensitive);
        content.Text.ShouldContain("new file mode", Case.Sensitive);
        content.Text.ShouldContain("+++ b/c.txt", Case.Sensitive);
        content.Text.ShouldContain("+brand new", Case.Sensitive);
        content.Text.ShouldNotContain("never shown", Case.Sensitive);
        content.SizeBytes.ShouldBe(Encoding.UTF8.GetByteCount(content.Text));
    }

    /// <summary>A diff longer than the cap is cut at the cap and says so.</summary>
    [Fact]
    public async Task ALongWorkingDiffIsCutAtTheCap()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync().ConfigureAwait(true);
        tree.Write("a.txt", string.Concat(Enumerable.Range(0, 500).Select(static line => "line number " + line + "\n")));
        var source = new GitMentionDiffSource(tree.Root);

        var content = await source.ReadAsync(MentionGitDiffKind.Working, 300, Token).ConfigureAwait(true);

        content.Truncated.ShouldBeTrue();
        Encoding.UTF8.GetByteCount(content.Text).ShouldBeLessThanOrEqualTo(300);
        content.SizeBytes.ShouldBeGreaterThanOrEqualTo(Encoding.UTF8.GetByteCount(content.Text));
        content.Text.ShouldContain("diff --git a/a.txt b/a.txt", Case.Sensitive);
    }

    /// <summary>A clean working tree has no changes: the summary counts none and the diff says so in a sentence.</summary>
    [Fact]
    public async Task ACleanWorkingTreeHasNoChanges()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync().ConfigureAwait(true);
        var source = new GitMentionDiffSource(tree.Root);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Working, Token).ConfigureAwait(true);
        var content = await source.ReadAsync(MentionGitDiffKind.Working, 1000, Token).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(true, 0, null, null));
        content.Text.ShouldBe("There are no uncommitted changes.", Case.Sensitive);
        content.Truncated.ShouldBeFalse();
    }

    /// <summary>The branch diff is the branch's commits against the merge base with main: uncommitted work is not in it, commits of main made after the branch point are not either.</summary>
    [Fact]
    public async Task TheBranchDiffIsTheCommitsOfTheBranchAgainstTheMergeBase()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync().ConfigureAwait(true);
        await GitCli.RunAsync(tree.Root, ["checkout", "--quiet", "-b", "feature"], Token).ConfigureAwait(true);
        tree.Write("feature.txt", "feature work\n");
        await GitCli.CommitAllAsync(tree.Root, "feature", Token).ConfigureAwait(true);
        await GitCli.RunAsync(tree.Root, ["checkout", "--quiet", "main"], Token).ConfigureAwait(true);
        tree.Write("later-on-main.txt", "main moved on\n");
        await GitCli.CommitAllAsync(tree.Root, "main moves", Token).ConfigureAwait(true);
        await GitCli.RunAsync(tree.Root, ["checkout", "--quiet", "feature"], Token).ConfigureAwait(true);
        tree.Write("uncommitted.txt", "not committed\n");
        var source = new GitMentionDiffSource(tree.Root);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Branch, Token).ConfigureAwait(true);
        var content = await source.ReadAsync(MentionGitDiffKind.Branch, 100_000, Token).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(true, 1, "main", null));
        content.Text.ShouldContain("+++ b/feature.txt", Case.Sensitive);
        content.Text.ShouldContain("+feature work", Case.Sensitive);
        content.Text.ShouldNotContain("later-on-main", Case.Sensitive);
        content.Text.ShouldNotContain("uncommitted", Case.Sensitive);
    }

    /// <summary>A repository with no main or master has no base branch: the summary is unavailable and says why, and reading throws the same sentence.</summary>
    [Fact]
    public async Task ABranchDiffWithoutABaseBranchIsUnavailable()
    {
        NeedGit();
        using var tree = await CommittedRepositoryAsync("trunk").ConfigureAwait(true);
        var source = new GitMentionDiffSource(tree.Root);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Branch, Token).ConfigureAwait(true);
        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => source.ReadAsync(MentionGitDiffKind.Branch, 1000, Token)).ConfigureAwait(true);

        summary.Available.ShouldBeFalse();
        summary.Reason.ShouldBe("Git found no base branch to compare with. It looked for origin/HEAD, main and master.", Case.Sensitive);
        refused.Message.ShouldBe(summary.Reason, Case.Sensitive);
    }

    /// <summary>A repository with no commit has no diff to show.</summary>
    [Fact]
    public async Task ARepositoryWithNoCommitIsUnavailable()
    {
        NeedGit();
        using var tree = new MentionWorkspaceTree();
        await GitCli.InitAsync(tree.Root, "main", Token).ConfigureAwait(true);
        var source = new GitMentionDiffSource(tree.Root);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Working, Token).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(false, 0, null, "This repository has no commits yet."));
    }

    /// <summary>A folder that is not a repository reads as the shared sentence, for both diffs and for both calls; a folder that does not exist does too.</summary>
    [Fact]
    public async Task AFolderThatIsNotARepositoryIsUnavailable()
    {
        NeedGit();
        using var tree = new MentionWorkspaceTree();
        tree.Write("a.txt", "x");
        var source = new GitMentionDiffSource(tree.Root);
        var absent = new GitMentionDiffSource(Path.Combine(tree.Base, "absent"));

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Branch, Token).ConfigureAwait(true);
        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => source.ReadAsync(MentionGitDiffKind.Working, 1000, Token)).ConfigureAwait(true);
        var missing = await absent.GetSummaryAsync(MentionGitDiffKind.Working, Token).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(false, 0, null, MentionCopy.NoRepository));
        refused.Message.ShouldBe(MentionCopy.NoRepository, Case.Sensitive);
        missing.Reason.ShouldBe(MentionCopy.NoRepository, Case.Sensitive);
    }

    /// <summary>When git cannot be started the summary and the read both say that git is missing, without throwing anything else.</summary>
    [Fact]
    public async Task AMissingGitReadsAsAPlainSentence()
    {
        using var tree = new MentionWorkspaceTree();
        var runner = new ScriptedProcessRunner((_, _) => Task.FromException<ProcessOutcome>(new Win32Exception(2, "No such file or directory")));
        var source = new GitMentionDiffSource(tree.Root, runner);

        var summary = await source.GetSummaryAsync(MentionGitDiffKind.Working, Token).ConfigureAwait(true);
        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => source.ReadAsync(MentionGitDiffKind.Branch, 1000, Token)).ConfigureAwait(true);

        summary.ShouldBe(new MentionGitDiffSummary(false, 0, null, "Git is not installed or is not on the PATH."));
        refused.Message.ShouldBe("Git is not installed or is not on the PATH.", Case.Sensitive);
        refused.InnerException.ShouldBeOfType<Win32Exception>();
    }

    /// <summary>A git that does not answer in time is stopped when the timeout fires, and the read says so; a caller's own cancellation is not turned into that sentence.</summary>
    [Fact]
    public async Task ASlowGitIsStoppedByTheTimeout()
    {
        using var tree = new MentionWorkspaceTree();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        var runner = new ScriptedProcessRunner(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(true);
            return new ProcessOutcome(0, string.Empty, string.Empty);
        });
        var source = new GitMentionDiffSource(tree.Root, runner, timeout: TimeSpan.FromSeconds(5), timeProvider: clock);

        var reading = source.ReadAsync(MentionGitDiffKind.Working, 1000, Token);
        clock.Timers.Count.ShouldBe(1);
        clock.Timers[0].Fire();
        var refused = await Should.ThrowAsync<MentionUnavailableException>(() => reading).ConfigureAwait(true);

        refused.Message.ShouldBe("Git took longer than 5 seconds to answer.", Case.Sensitive);

        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var again = source.ReadAsync(MentionGitDiffKind.Working, 1000, cancelled.Token);
        await cancelled.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<TaskCanceledException>(() => again).ConfigureAwait(true);
    }
}
