using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// The on-disk workspace listing against real symbolic links: nothing outside the root is listed or entered, and links that stay
/// inside the root behave like any entry (ADE-10).
/// </summary>
public sealed class LocalWorkspaceFileSystemTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "ompos-ws-" + Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly string _outside;

    /// <summary>Builds a root with inside content, plus links that point inside it and out of it.</summary>
    public LocalWorkspaceFileSystemTests()
    {
        _root = Path.Combine(_sandbox, "root");
        _outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(Path.Combine(_root, "inside"));
        Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_root, "inside", "notes.txt"), "inside");
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "outside");

        Directory.CreateSymbolicLink(Path.Combine(_root, "linked-inside"), Path.Combine(_root, "inside"));
        Directory.CreateSymbolicLink(Path.Combine(_root, "escape"), _outside);
        File.CreateSymbolicLink(Path.Combine(_root, "file-escape.txt"), Path.Combine(_outside, "secret.txt"));
        Directory.CreateSymbolicLink(Path.Combine(_root, "inside", "nested-escape"), _outside);
    }

    /// <summary>Removes the sandbox, including the links it holds.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
        {
            Directory.Delete(_sandbox, recursive: true);
        }
    }

    /// <summary>The root listing shows real entries and links that stay inside, and hides links that leave the root.</summary>
    [Fact]
    public void ListDirectory_Root_HidesLinksThatLeaveTheRoot()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);

        var names = fileSystem.ListDirectory(string.Empty).Select(entry => entry.Name).Order(StringComparer.Ordinal).ToArray();

        names.ShouldBe(["inside", "linked-inside"]);
    }

    /// <summary>A link to a directory outside the root is never listed, and listing it by path is refused.</summary>
    [Fact]
    public void ListDirectory_ThroughALinkOutsideTheRoot_IsRefused()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);

        Should.Throw<UnauthorizedAccessException>(() => fileSystem.ListDirectory("escape"));
        Should.Throw<UnauthorizedAccessException>(() => fileSystem.ListDirectory("inside/nested-escape"));
    }

    /// <summary>A directory reached through a link that stays inside lists its contents, with paths under the link's own path.</summary>
    [Fact]
    public void ListDirectory_ThroughALinkInsideTheRoot_ListsItsContents()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);

        var entries = fileSystem.ListDirectory("linked-inside");

        entries.ShouldHaveSingleItem().ShouldBe(new WorkspaceEntry("notes.txt", "linked-inside/notes.txt", IsDirectory: false));
    }

    /// <summary>A path that climbs above the root is refused before the disk is touched.</summary>
    [Fact]
    public void ListDirectory_ClimbingAboveTheRoot_IsRefused()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);

        Should.Throw<ArgumentException>(() => fileSystem.ListDirectory("../outside"));
    }

    /// <summary>A missing directory is reported as missing, and a missing root cannot be opened.</summary>
    [Fact]
    public void MissingDirectoryAndMissingRoot_AreReported()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);

        Should.Throw<DirectoryNotFoundException>(() => fileSystem.ListDirectory("nowhere"));
        Should.Throw<DirectoryNotFoundException>(() => new LocalWorkspaceFileSystem(Path.Combine(_sandbox, "absent")));
    }

    /// <summary>The tree over the disk shows only the root's safe entries, and cannot expand a link that leaves the root.</summary>
    [Fact]
    public void WorkspaceTree_OverDisk_CannotReachALinkOutsideTheRoot()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(new LocalWorkspaceFileSystem(_root));

        tree.Nodes.Select(node => node.Name).Order(StringComparer.Ordinal).ShouldBe(["inside", "linked-inside"]);
        Should.Throw<ArgumentException>(() => tree.ExpandPath("escape"));
    }
}
