using OmpOS.Workspace.Sets;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Sets;

/// <summary>
/// Verifies the set invariants: the primary is first, names and identifiers stay unique when folders are added, and every change
/// returns a new set.
/// </summary>
public sealed class WorkspaceSetTests
{
    private static WorkspaceSetRoot Root(string id, string path, bool primary = false) => new(id, path, id, EnvironmentId: null, primary);

    [Fact]
    public void TheFlaggedRootMovesToTheFrontAndTheRestKeepTheirOrder()
    {
        var set = new WorkspaceSet("s", "Set", [Root("a", "/a"), Root("b", "/b", primary: true), Root("c", "/c")]);

        set.Roots.Select(root => root.Id).ShouldBe(["b", "a", "c"]);
        set.Roots.Select(root => root.IsPrimary).ShouldBe([true, false, false]);
        set.PrimaryPath.ShouldBe("/b");
        set.AdditionalDirectoryPaths.ShouldBe(["/a", "/c"]);
    }

    [Fact]
    public void WithoutAFlagTheFirstRootIsThePrimary()
    {
        var set = new WorkspaceSet("s", "Set", [Root("a", "/a"), Root("b", "/b")]);

        set.Primary.Id.ShouldBe("a");
        set.Roots[1].IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void TwoFlaggedRootsOrNoRootAreRejected()
    {
        Should.Throw<ArgumentException>(() => new WorkspaceSet("s", "Set", [Root("a", "/a", primary: true), Root("b", "/b", primary: true)]));
        Should.Throw<ArgumentException>(() => new WorkspaceSet("s", "Set", []));
    }

    [Fact]
    public void FromPathsNamesRootsAfterTheirFoldersAndSuffixesRepeatedNames()
    {
        var set = WorkspaceSet.FromPaths("My Project", ["/work/a/src", "/work/b/src", "/work/c/Src", "/work/api"]);

        set.Id.ShouldBe("my-project");
        set.Roots.Select(root => root.DisplayName).ShouldBe(["src", "src-2", "Src-3", "api"]);
        set.Roots.Select(root => root.Id).ShouldBe(["src", "src-2", "src-3", "api"]);
        set.Primary.Path.ShouldBe("/work/a/src");
    }

    [Fact]
    public void AddFolderAppendsANonPrimaryRootInTheEnvironmentOfTheSetAndLeavesTheOriginalAlone()
    {
        var original = WorkspaceSet.FromPaths("S", ["/work/a"], environmentId: "ssh-dev");

        var grown = original.AddFolder("/work/b", "Backend");

        original.Roots.Count.ShouldBe(1);
        grown.Roots.Select(root => root.DisplayName).ShouldBe(["a", "Backend"]);
        grown.Roots[1].EnvironmentId.ShouldBe("ssh-dev");
        grown.Roots[1].Kind.ShouldBe(WorkspaceRootKind.Remote);
        grown.Roots[1].IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void RemovingThePrimaryPromotesTheNextRoot()
    {
        var set = WorkspaceSet.FromPaths("S", ["/work/a", "/work/b", "/work/c"]);

        var smaller = set.Remove("a");

        smaller.Roots.Select(root => root.Id).ShouldBe(["b", "c"]);
        smaller.Primary.Id.ShouldBe("b");
        smaller.Primary.IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void RemovingAnUnknownRootReturnsTheSameSetAndRemovingTheLastRootThrows()
    {
        var set = WorkspaceSet.FromPaths("S", ["/work/a"]);

        set.Remove("nope").ShouldBeSameAs(set);
        Should.Throw<InvalidOperationException>(() => set.Remove("a"));
    }

    [Fact]
    public void WithPrimaryMovesOnlyThePrimary()
    {
        var set = WorkspaceSet.FromPaths("S", ["/work/a", "/work/b", "/work/c"]);

        var changed = set.WithPrimary("c");

        changed.Roots.Select(root => root.Id).ShouldBe(["c", "a", "b"]);
        changed.PrimaryPath.ShouldBe("/work/c");
        set.WithPrimary("nope").ShouldBeSameAs(set);
    }

    [Fact]
    public void ARootKnowsWhetherItIsLocalOrRemote()
    {
        new WorkspaceSetRoot("a", "/a", "a").Kind.ShouldBe(WorkspaceRootKind.Local);
        new WorkspaceSetRoot("a", "/a", "a", "cloud-1").Kind.ShouldBe(WorkspaceRootKind.Remote);
    }
}
