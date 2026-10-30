using Autonogrammer.Workspace.Sets;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Sets;

/// <summary>
/// Verifies every rule of the validator against a scripted probe, and the real-path rule against symbolic links on disk.
/// </summary>
public sealed class WorkspaceSetValidatorTests
{
    private static WorkspaceSetValidation Validate(FakeDirectoryProbe probe, WorkspaceSet set) => new WorkspaceSetValidator(probe).Validate(set);

    private static FakeDirectoryProbe Repos(params string[] paths)
    {
        var probe = new FakeDirectoryProbe();
        foreach (var path in paths)
        {
            probe.WithDirectory(path);
        }

        return probe;
    }

    private static WorkspaceSetIssueCode[] Codes(WorkspaceSetValidation validation) => validation.Issues.Select(issue => issue.Code).ToArray();

    [Fact]
    public void ADistinctExistingGitSetHasNoFinding()
    {
        var result = Validate(Repos("/repos/web", "/repos/api"), WorkspaceSet.FromPaths("S", ["/repos/web", "/repos/api"]));

        result.IsValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void AMissingDirectoryIsAnErrorThatNamesTheRoot()
    {
        var result = Validate(Repos("/repos/web"), WorkspaceSet.FromPaths("S", ["/repos/web", "/repos/gone"]));

        result.IsValid.ShouldBeFalse();
        var issue = result.Errors.ShouldHaveSingleItem();
        issue.Code.ShouldBe(WorkspaceSetIssueCode.MissingDirectory);
        issue.RootId.ShouldBe("gone");
        issue.Message.ShouldContain("gone", Case.Sensitive);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("../up")]
    [InlineData("")]
    [InlineData("/repos/a\0b")]
    public void ABlankRelativeOrNulPathIsInvalid(string path)
    {
        var set = new WorkspaceSet("s", "S", [new WorkspaceSetRoot("a", path, "a", null, IsPrimary: true)]);

        var result = Validate(new FakeDirectoryProbe(), set);

        Codes(result).ShouldBe([WorkspaceSetIssueCode.InvalidPath]);
    }

    [Fact]
    public void TheSameDirectoryTwiceIsADuplicateEvenWithATrailingSeparatorOrDotSegment()
    {
        var set = new WorkspaceSet(
            "s",
            "S",
            [new WorkspaceSetRoot("a", "/repos/web", "web", null, true), new WorkspaceSetRoot("b", "/repos/other/../web/", "web two")]);

        var result = Validate(Repos("/repos/web"), set);

        var issue = result.Errors.ShouldHaveSingleItem();
        issue.Code.ShouldBe(WorkspaceSetIssueCode.DuplicateRoot);
        issue.RootId.ShouldBe("b");
    }

    [Fact]
    public void TwoLinksToOneDirectoryAreOneRoot()
    {
        var probe = Repos("/repos/web").WithLink("/links/one", "/repos/web").WithLink("/links/two", "/repos/web");

        var result = Validate(probe, WorkspaceSet.FromPaths("S", ["/links/one", "/links/two"]));

        Codes(result).ShouldContain(WorkspaceSetIssueCode.DuplicateRoot);
    }

    [Fact]
    public void ARootInsideAnotherIsRejectedWhicheverComesFirst()
    {
        var probe = Repos("/repos/mono", "/repos/mono/packages/ui");

        var inner = Validate(probe, WorkspaceSet.FromPaths("S", ["/repos/mono", "/repos/mono/packages/ui"]));
        var outer = Validate(probe, WorkspaceSet.FromPaths("S", ["/repos/mono/packages/ui", "/repos/mono"]));

        var innerIssue = inner.Errors.ShouldHaveSingleItem();
        innerIssue.Code.ShouldBe(WorkspaceSetIssueCode.NestedRoot);
        innerIssue.RootId.ShouldBe("ui");
        var outerIssue = outer.Errors.ShouldHaveSingleItem();
        outerIssue.Code.ShouldBe(WorkspaceSetIssueCode.NestedRoot);
        outerIssue.RootId.ShouldBe("ui");
    }

    [Fact]
    public void ASiblingWhoseNameStartsWithTheOtherNameIsNotNested()
    {
        var result = Validate(Repos("/repos/app", "/repos/app-admin"), WorkspaceSet.FromPaths("S", ["/repos/app", "/repos/app-admin"]));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void MoreThanSixteenRootsIsAnErrorAndSixteenIsNot()
    {
        var paths = Enumerable.Range(0, 17).Select(index => $"/repos/r{index}").ToArray();
        var probe = Repos(paths);

        Codes(Validate(probe, WorkspaceSet.FromPaths("S", paths.Take(16)))).ShouldBeEmpty();
        Codes(Validate(probe, WorkspaceSet.FromPaths("S", paths))).ShouldBe([WorkspaceSetIssueCode.TooManyRoots]);
    }

    [Fact]
    public void RootsOfDifferentEnvironmentsAreRejectedWithAReasonThatNamesThem()
    {
        var set = new WorkspaceSet(
            "s",
            "S",
            [new WorkspaceSetRoot("a", "/repos/web", "web", null, true), new WorkspaceSetRoot("b", "/srv/api", "api", "ssh-dev")]);

        var result = Validate(Repos("/repos/web"), set);

        var issue = result.Errors.ShouldHaveSingleItem();
        issue.Code.ShouldBe(WorkspaceSetIssueCode.MixedEnvironments);
        issue.RootId.ShouldBe("b");
        issue.Message.ShouldContain("local", Case.Sensitive);
        issue.Message.ShouldContain("ssh-dev", Case.Sensitive);
    }

    [Fact]
    public void RemoteRootsAreNeverProbedAndAreComparedByNormalisedPosixPaths()
    {
        var probe = new FakeDirectoryProbe();
        var set = WorkspaceSet.FromPaths("S", ["/srv/mono", "/srv/mono/pkg/../pkg/ui", "/srv/./mono/"], environmentId: "ssh-dev");

        var result = Validate(probe, set);

        probe.Asked.ShouldBeEmpty();
        Codes(result).ShouldBe([WorkspaceSetIssueCode.NestedRoot, WorkspaceSetIssueCode.DuplicateRoot, WorkspaceSetIssueCode.NestedRoot], ignoreOrder: true);
    }

    [Fact]
    public void ARemoteRootMustBeAbsolute()
    {
        var set = new WorkspaceSet("s", "S", [new WorkspaceSetRoot("a", "projects/web", "web", "ssh-dev", true)]);

        Codes(Validate(new FakeDirectoryProbe(), set)).ShouldBe([WorkspaceSetIssueCode.InvalidPath]);
    }

    [Fact]
    public void ANonGitRootIsAWarningNotAnError()
    {
        var probe = Repos("/repos/web").WithDirectory("/repos/notes", git: false);

        var result = Validate(probe, WorkspaceSet.FromPaths("S", ["/repos/web", "/repos/notes"]));

        result.IsValid.ShouldBeTrue();
        var warning = result.Warnings.ShouldHaveSingleItem();
        warning.Code.ShouldBe(WorkspaceSetIssueCode.NotGitRepository);
        warning.RootId.ShouldBe("notes");
    }

    [Fact]
    public void ABlankNameAndARepeatedNameAreErrorsAndARepeatedIdIsReported()
    {
        var set = new WorkspaceSet(
            "s",
            "S",
            [
                new WorkspaceSetRoot("a", "/repos/a", "Core", null, true),
                new WorkspaceSetRoot("a", "/repos/b", " core ", null),
                new WorkspaceSetRoot("c", "/repos/c", "  ", null),
            ]);

        var result = Validate(Repos("/repos/a", "/repos/b", "/repos/c"), set);

        Codes(result).ShouldBe(
            [WorkspaceSetIssueCode.InvalidName, WorkspaceSetIssueCode.DuplicateId, WorkspaceSetIssueCode.InvalidName],
            ignoreOrder: true);
        result.Errors.Count().ShouldBe(3);
    }

    [Fact]
    public void RealLinksOnDiskAreResolvedByTheLocalProbe()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Creating symbolic links needs a privilege on Windows.");
        }

        using var workspace = new TempWorkspace();
        var real = Path.Combine(workspace.RootPath, "real");
        Directory.CreateDirectory(Path.Combine(real, ".git"));
        var link = Path.Combine(workspace.RootPath, "link");
        Directory.CreateSymbolicLink(link, real);
        var plain = Path.Combine(workspace.RootPath, "plain");
        Directory.CreateDirectory(plain);

        var result = new WorkspaceSetValidator(new LocalDirectoryProbe()).Validate(WorkspaceSet.FromPaths("S", [real, link, plain]));

        Codes(result).ShouldBe([WorkspaceSetIssueCode.DuplicateRoot, WorkspaceSetIssueCode.NotGitRepository], ignoreOrder: true);
        result.Errors.ShouldHaveSingleItem().RootId.ShouldBe("link");
        result.Warnings.ShouldHaveSingleItem().RootId.ShouldBe("plain");
    }
}
