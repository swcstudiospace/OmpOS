using Autonogrammer.Cowork.Chat;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>Trusted folders cannot be secrets, omp, ssh, account folders, or an escape into them.</summary>
public sealed class CoworkPathBoundaryTests
{
    [Fact]
    public void SecretsOmpSshAndAccountFoldersAreRefused()
    {
        using var fixture = new Tree();
        var boundary = fixture.Boundary();
        boundary.IsForbiddenTrustedFolder(fixture.At("secrets")).ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder(fixture.At("secrets", "token")).ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder(Path.Combine(fixture.Home, ".omp")).ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder(Path.Combine(fixture.Home, ".ssh")).ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder("~/.ssh").ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder("~/.omp").ShouldBeTrue();
        var account = CoworkAccountFolder.AccountDirectory(fixture.State, CoworkAccountFolder.KeyFromSubject("subject-one"));
        boundary.IsForbiddenTrustedFolder(account).ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder(fixture.At("work")).ShouldBeFalse();
    }

    [Fact]
    public void DotDotAndASymlinkCannotEscapeIntoAForbiddenFolder()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var fixture = new Tree();
        var boundary = fixture.Boundary();
        Directory.CreateDirectory(fixture.At("secrets"));
        Directory.CreateDirectory(fixture.At("work"));
        var escaped = Path.Combine(fixture.At("work"), "..", "secrets");
        boundary.IsForbiddenTrustedFolder(escaped).ShouldBeTrue();
        boundary.Canonicalize(escaped).ShouldBe(Path.GetFullPath(fixture.At("secrets")));

        var link = Path.Combine(fixture.At("work"), "via-link");
        Directory.CreateSymbolicLink(link, fixture.At("secrets"));
        boundary.IsForbiddenTrustedFolder(link).ShouldBeTrue();
        boundary.IsAllowed(link, [fixture.At("work")], storageFolder: null, fixture.At("work")).ShouldBeFalse();
        boundary.IsAllowed(fixture.At("work", "notes.txt"), [fixture.At("work")], null, fixture.At("work")).ShouldBeTrue();
    }

    [Fact]
    public void UncAndDriveRootAreRefusedOnLinuxToo()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        CoworkPathBoundary.IsUncPath(@"\\server\share\folder").ShouldBeTrue();
        CoworkPathBoundary.IsUncPath("//server/share/folder").ShouldBeTrue();
        CoworkPathBoundary.IsDriveRoot(@"C:\").ShouldBeTrue();
        CoworkPathBoundary.IsDriveRoot("D:").ShouldBeTrue();
        CoworkPathBoundary.IsDriveRoot("/tmp/work").ShouldBeFalse();
        using var fixture = new Tree();
        var boundary = fixture.Boundary();
        boundary.IsForbiddenTrustedFolder(@"\\server\share\folder").ShouldBeTrue();
        boundary.IsForbiddenTrustedFolder(@"C:\").ShouldBeTrue();
    }

    [Fact]
    public void TheStorageFolderIsAllowedWhenItIsNotForbidden()
    {
        using var fixture = new Tree();
        var boundary = fixture.Boundary();
        var storage = fixture.At("Storage");
        Directory.CreateDirectory(storage);
        boundary.IsAllowed(Path.Combine(storage, "out.txt"), [fixture.At("work")], storage, fixture.At("work")).ShouldBeTrue();
        boundary.IsAllowed(fixture.At("elsewhere", "out.txt"), [fixture.At("work")], storage, fixture.At("work")).ShouldBeFalse();
    }

    private sealed class Tree : IDisposable
    {
        public Tree()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-boundary-" + Guid.NewGuid().ToString("N"));
            State = Path.Combine(Root, "state");
            Home = Path.Combine(Root, "home");
            Directory.CreateDirectory(State);
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(Path.Combine(Home, ".omp"));
            Directory.CreateDirectory(Path.Combine(Home, ".ssh"));
            Directory.CreateDirectory(Path.Combine(State, "secrets"));
            Directory.CreateDirectory(Path.Combine(State, "work"));
            Directory.CreateDirectory(CoworkAccountFolder.AccountDirectory(State, CoworkAccountFolder.KeyFromSubject("subject-one")));
        }

        public string Root { get; }

        public string State { get; }

        public string Home { get; }

        public CoworkPathBoundary Boundary() => new(State, Home);

        public string At(params string[] parts) => System.IO.Path.Combine([State, .. parts]);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
