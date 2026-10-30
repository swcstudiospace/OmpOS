using Autonogrammer.Harness.AgentLoop.Substrate;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Substrate;

/// <summary>Reads the repository of a workspace from its git configuration without running a process.</summary>
public sealed class WorkspaceRepositoryResolverTests
{
    [Theory]
    [InlineData("https://github.com/swcstudiospace/OmpOS.git", "swcstudiospace/OmpOS")]
    [InlineData("https://github.com/swcstudiospace/OmpOS", "swcstudiospace/OmpOS")]
    [InlineData("git@github.com:swcstudiospace/OmpOS.git", "swcstudiospace/OmpOS")]
    [InlineData("ssh://git@github.com/swcstudiospace/OmpOS.git", "swcstudiospace/OmpOS")]
    [InlineData("https://user:ghp_SECRETTOKEN@github.com/swcstudiospace/OmpOS.git", "swcstudiospace/OmpOS")]
    [InlineData("https://host.example/group/sub/owner/name.git/", "owner/name")]
    public void ParseRepositoryReturnsOwnerSlashNameAndNeverTheCredential(string remote, string expected)
    {
        var parsed = WorkspaceRepositoryResolver.ParseRepository(remote);

        parsed.ShouldBe(expected);
        parsed.ShouldNotBeNull().ShouldNotContain("SECRET", Case.Sensitive);
    }

    [Theory]
    [InlineData("https://github.com/onlyone")]
    [InlineData("not a url")]
    [InlineData("https://github.com/o/n%22x")]
    [InlineData("https://github.com/o/na me")]
    [InlineData("")]
    public void ParseRepositoryRejectsUrlsWithoutAnOwnerAndSafeName(string remote)
    {
        WorkspaceRepositoryResolver.ParseRepository(remote).ShouldBeNull();
    }

    [Fact]
    public void ResolveReadsTheOriginRemoteAndIgnoresOtherRemotes()
    {
        using var directory = new SubstrateTempDirectory();
        directory.Write(".git/config", "[core]\n\tbare = false\n[remote \"upstream\"]\n\turl = https://github.com/other/upstream.git\n[remote \"origin\"]\n\turl = git@github.com:swcstudiospace/OmpOS.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");

        WorkspaceRepositoryResolver.Resolve(directory.Path).ShouldBe("swcstudiospace/OmpOS");
    }

    [Fact]
    public void ResolveFollowsALinkedWorktreeToTheCommonGitDirectory()
    {
        using var directory = new SubstrateTempDirectory();
        directory.Write("main/.git/config", "[remote \"origin\"]\n\turl = https://github.com/swcstudiospace/OmpOS.git\n");
        directory.Write("main/.git/worktrees/wt/commondir", "../..\n");
        var gitdir = System.IO.Path.Combine(directory.Path, "main", ".git", "worktrees", "wt");
        directory.Write("wt/.git", "gitdir: " + gitdir + "\n");

        WorkspaceRepositoryResolver.Resolve(System.IO.Path.Combine(directory.Path, "wt")).ShouldBe("swcstudiospace/OmpOS");
    }

    [Fact]
    public void ResolveReturnsNullWithoutAGitDirectoryAnOriginOrAReadableConfig()
    {
        using var directory = new SubstrateTempDirectory();
        WorkspaceRepositoryResolver.Resolve(directory.Path).ShouldBeNull();

        directory.Write(".git/config", "[remote \"upstream\"]\n\turl = https://github.com/o/n.git\n");
        WorkspaceRepositoryResolver.Resolve(directory.Path).ShouldBeNull();

        WorkspaceRepositoryResolver.Resolve(System.IO.Path.Combine(directory.Path, "missing")).ShouldBeNull();
        WorkspaceRepositoryResolver.Resolve(" ").ShouldBeNull();
    }
}
