using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>
/// Verifies that workspace paths cannot leave the root, lexically or through symbolic links (ADR-0005).
/// </summary>
public sealed class WorkspaceRootTests
{
    [Fact]
    public void ResolvesPathInsideRoot()
    {
        using var workspace = new TempWorkspace();

        var resolved = workspace.Root.Resolve(Path.Combine("src", "app.txt"));

        resolved.ShouldBe(Path.Combine(workspace.Root.FullPath, "src", "app.txt"));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("src/../../outside.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public void RejectsPathsThatLeaveTheRoot(string relativePath)
    {
        using var workspace = new TempWorkspace();

        Assert.Throws<WorkspacePathException>(() => workspace.Root.Resolve(relativePath));
    }

    [Fact]
    public void RejectsFileSymbolicLinkThatEscapesRoot()
    {
        using var workspace = new TempWorkspace();
        using var outside = new TempWorkspace();
        outside.Write("secret.txt", "outside\n");
        File.CreateSymbolicLink(
            Path.Combine(workspace.RootPath, "link.txt"),
            Path.Combine(outside.RootPath, "secret.txt"));

        Assert.Throws<WorkspacePathException>(() => workspace.Root.Resolve("link.txt"));
    }

    [Fact]
    public void RejectsPathThroughDirectorySymbolicLinkThatEscapesRoot()
    {
        using var workspace = new TempWorkspace();
        using var outside = new TempWorkspace();
        outside.Write("secret.txt", "outside\n");
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "linked"), outside.RootPath);

        Assert.Throws<WorkspacePathException>(() => workspace.Root.Resolve("linked/secret.txt"));
    }

    [Fact]
    public void RejectsWriteThroughDanglingSymbolicLinkThatEscapesRoot()
    {
        using var workspace = new TempWorkspace();
        using var outside = new TempWorkspace();
        File.CreateSymbolicLink(
            Path.Combine(workspace.RootPath, "dangling"),
            Path.Combine(outside.RootPath, "not-yet-created"));

        Assert.Throws<WorkspacePathException>(() => workspace.Root.Resolve("dangling/new.txt"));
    }

    [Fact]
    public void AcceptsSymbolicLinkThatStaysInsideRoot()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("src/app.txt", "inside\n");
        File.CreateSymbolicLink(Path.Combine(workspace.RootPath, "alias"), Path.Combine(workspace.RootPath, "src"));

        var resolved = workspace.Root.Resolve("alias/app.txt");

        resolved.ShouldBe(Path.Combine(workspace.Root.FullPath, "src", "app.txt"));
    }

    [Fact]
    public void ConstructorRejectsMissingRoot()
    {
        var missing = Path.Combine(Path.GetTempPath(), "autonogrammer-missing-" + Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => new WorkspaceRoot(missing));
    }
}
