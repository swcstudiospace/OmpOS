using System.Globalization;
using OmpOS.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>
/// Convention discovery over the fake ssh: the same tree is discovered through the remote source and through the local source and the two must agree,
/// and the remote source must refuse what the local one refuses (a link that leaves the root, a huge file) and never throw for a host that does not answer.
/// </summary>
public sealed class RemoteConventionFileSourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RemoteConventionFileSource SourceOver(RemoteFixture fixture, params string[] roots) => new(fixture.Exec, roots);

    private static string[] Summary(ConventionSet set) =>
    [
        .. set.Commands.Select(item => $"command {item.Name} {item.Scope} {item.Origin} {item.Path}"),
        .. set.Skills.Select(item => $"skill {item.Name} {item.Scope} {item.Origin} {item.Path}"),
        .. set.Rules.Select(item => $"rule {item.Name} {item.Scope} {item.Origin} {item.Path}"),
        .. set.Subagents.Select(item => $"agent {item.Name} {item.Scope} {item.Origin} {item.Path}"),
        .. set.Diagnostics.Select(item => $"diagnostic {item.Item} {item.Kind} {item.Origin}"),
    ];

    /// <summary>Every convention directory at the workspace and at the remote home is discovered through ssh exactly as the local source discovers the same tree.</summary>
    [Fact]
    public async Task RemoteDiscoveryMatchesLocalDiscoveryForEveryDirectoryAndScope()
    {
        using var fixture = RemoteFixture.Create();
        foreach (var directory in ConventionDirectories.Names)
        {
            _ = fixture.Write(directory + "/commands/cmd-" + directory.TrimStart('.') + ".md", "Run " + directory + "\n");
            _ = fixture.Write(directory + "/skills/skill-" + directory.TrimStart('.') + "/SKILL.md", "Skill from " + directory + "\n");
            _ = fixture.Write(directory + "/rules/rule-" + directory.TrimStart('.') + ".md", "Rule from " + directory + "\n");
            _ = fixture.Write(directory + "/agents/agent-" + directory.TrimStart('.') + ".md", "Agent from " + directory + "\n");
            _ = fixture.WriteHome(directory + "/commands/home-" + directory.TrimStart('.') + ".md", "Home " + directory + "\n");
            _ = fixture.WriteHome(directory + "/skills/hskill-" + directory.TrimStart('.') + "/SKILL.md", "Home skill " + directory + "\n");
        }

        _ = fixture.Write(".ompos/commands/shared.md", "project shared\n");
        _ = fixture.WriteHome(".ompos/commands/shared.md", "user shared\n");
        _ = fixture.Write("AGENTS.md", "Root agents file\n");
        _ = fixture.Write("pkg/api/.cursor/rules/nested.mdc", "---\ndescription: nested rule\nglobs: \"**/*.cs\"\n---\nBody\n");

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var remote = await setup.DiscoverAsync(Token).ConfigureAwait(true);
        var local = await ConventionCatalog.DiscoverAsync(
            [fixture.Workspace],
            fixture.Host.HomeDirectory,
            ConventionSources.Local([fixture.Workspace], fixture.Host.HomeDirectory),
            RemoteConventionSources.DiscoveryOptions,
            Token).ConfigureAwait(true);

        setup.UserHome.ShouldBe(fixture.Host.HomeDirectory);
        setup.Roots.ShouldBe([fixture.Workspace]);
        remote.Commands.Count.ShouldBeGreaterThan(10);
        remote.Skills.Count.ShouldBe(12);
        Summary(remote).ShouldBe(Summary(local));
    }

    /// <summary>The user scope is read from the remote home directory (<c>$HOME</c> on the remote machine), not from this machine.</summary>
    [Fact]
    public async Task UserScopeComesFromTheRemoteHome()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.WriteHome(".claude/commands/remote-only.md", "From the remote home\n");

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        var command = set.Commands.ShouldHaveSingleItem();
        command.Name.ShouldBe("remote-only");
        command.Scope.ShouldBe(ConventionScope.User);
        command.Path.ShouldBe(Path.Combine(fixture.Host.HomeDirectory, ".claude", "commands", "remote-only.md"));
    }

    /// <summary>The folder listing is in ordinal name order, cut at the requested count, and flags symbolic links.</summary>
    [Fact]
    public async Task ListingIsOrderedCutAndFlagsLinks()
    {
        using var fixture = RemoteFixture.Create();
        foreach (var name in new[] { "c.md", "a.md", "b.md", "d.md" })
        {
            _ = fixture.Write("dir/" + name, "x");
        }

        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "dir", "z-link.md"), "a.md");
        var source = SourceOver(fixture, fixture.Workspace);

        var all = await source.ListAsync(Path.Combine(fixture.Workspace, "dir"), 100, Token).ConfigureAwait(true);
        var cut = await source.ListAsync(Path.Combine(fixture.Workspace, "dir"), 2, Token).ConfigureAwait(true);

        all.Entries.Select(entry => entry.Name).ToArray().ShouldBe(["a.md", "b.md", "c.md", "d.md", "z-link.md"]);
        all.Entries.Single(entry => string.Equals(entry.Name, "z-link.md", StringComparison.Ordinal)).IsSymbolicLink.ShouldBeTrue();
        all.Entries.Single(entry => string.Equals(entry.Name, "a.md", StringComparison.Ordinal)).IsSymbolicLink.ShouldBeFalse();
        cut.Entries.Select(entry => entry.Name).ToArray().ShouldBe(["a.md", "b.md"]);
    }

    /// <summary>A folder that does not exist lists no entries and no problem; a file used as a folder too.</summary>
    [Fact]
    public async Task AMissingFolderListsNothing()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("f.txt", "x");
        var source = SourceOver(fixture, fixture.Workspace);

        var missing = await source.ListAsync(Path.Combine(fixture.Workspace, "nope"), 10, Token).ConfigureAwait(true);
        var file = await source.ListAsync(Path.Combine(fixture.Workspace, "f.txt"), 10, Token).ConfigureAwait(true);

        missing.Entries.ShouldBeEmpty();
        missing.Problem.ShouldBeNull();
        file.Entries.ShouldBeEmpty();
        file.Problem.ShouldBeNull();
    }

    /// <summary>A path outside every root, a path with dot-dot and a link out of the root are refused for list, read and exists; nothing outside is read.</summary>
    [Fact]
    public async Task PathsOutsideTheRootsAreRefused()
    {
        using var fixture = RemoteFixture.Create();
        await File.WriteAllTextAsync(fixture.Outside.Combine("secret.txt"), "TOP-SECRET", TestContext.Current.CancellationToken).ConfigureAwait(true);
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "escape"), fixture.Outside.Path);
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "leak.md"), fixture.Outside.Combine("secret.txt"));
        var source = SourceOver(fixture, fixture.Workspace);

        var outside = await source.ReadTextAsync(fixture.Outside.Combine("secret.txt"), 1000, Token).ConfigureAwait(true);
        var climbing = await source.ReadTextAsync(fixture.Workspace + "/../" + Path.GetFileName(fixture.Outside.Path) + "/secret.txt", 1000, Token).ConfigureAwait(true);
        var viaDirectoryLink = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "escape", "secret.txt"), 1000, Token).ConfigureAwait(true);
        var viaFileLink = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "leak.md"), 1000, Token).ConfigureAwait(true);
        var listed = await source.ListAsync(Path.Combine(fixture.Workspace, "escape"), 10, Token).ConfigureAwait(true);
        var exists = await source.ExistsAsync(Path.Combine(fixture.Workspace, "leak.md"), Token).ConfigureAwait(true);

        foreach (var result in new[] { outside, climbing, viaDirectoryLink, viaFileLink })
        {
            result.IsRead.ShouldBeFalse();
            result.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
            (result.Problem ?? string.Empty).ShouldNotContain("TOP-SECRET", Case.Sensitive);
        }

        listed.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
        exists.ShouldBeFalse();
    }

    /// <summary>A file larger than the cap is TooLarge and the remote side reads no more than the cap; a file that is not UTF-8 is Malformed; a byte order mark is dropped.</summary>
    [Fact]
    public async Task ReadsAreCappedAndDecodedStrictly()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("big.md", new string('x', 300 * 1024));
        await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "latin.md"), [0x63, 0x61, 0x66, 0xE9], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "bom.md"), [0xEF, 0xBB, 0xBF, 0x68, 0x69], TestContext.Current.CancellationToken).ConfigureAwait(true);
        var source = SourceOver(fixture, fixture.Workspace);
        var limit = new ConventionDiscoveryOptions().MaxFileBytes;

        var big = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "big.md"), limit, Token).ConfigureAwait(true);
        var latin = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "latin.md"), limit, Token).ConfigureAwait(true);
        var bom = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "bom.md"), limit, Token).ConfigureAwait(true);
        var missing = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "none.md"), limit, Token).ConfigureAwait(true);

        big.IsRead.ShouldBeFalse();
        big.ProblemKind.ShouldBe(ConventionDiagnosticKind.TooLarge);
        big.Problem.ShouldBe(string.Create(CultureInfo.InvariantCulture, $"the file is larger than the {limit / 1024} KiB limit"));
        latin.ProblemKind.ShouldBe(ConventionDiagnosticKind.Malformed);
        bom.Text.ShouldBe("hi");
        missing.IsRead.ShouldBeFalse();
        missing.ProblemKind.ShouldBe(ConventionDiagnosticKind.Unreadable);
    }

    /// <summary>A file the remote user cannot read is an Unreadable diagnostic in discovery and never an exception.</summary>
    [Fact]
    public async Task AnUnreadableFileIsADiagnostic()
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Skip("A privileged user can read any file, so a file cannot be made unreadable.");
            return;
        }

        using var fixture = RemoteFixture.Create();
        var path = fixture.Write(".cursor/commands/locked.md", "secret");
        File.SetUnixFileMode(path, UnixFileMode.None);
        _ = fixture.Write(".cursor/commands/open.md", "open");

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        set.Commands.ShouldHaveSingleItem().Name.ShouldBe("open");
        set.Diagnostics.ShouldContain(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.Unreadable);
    }

    /// <summary>A symbolic link to a convention file outside the workspace becomes an EscapesRoot diagnostic in discovery, and its content never reaches an item.</summary>
    [Fact]
    public async Task AnEscapingLinkInAConventionFolderIsADiagnostic()
    {
        using var fixture = RemoteFixture.Create();
        await File.WriteAllTextAsync(fixture.Outside.Combine("evil.md"), "EVIL-TEMPLATE", TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = fixture.Write(".cursor/commands/fine.md", "fine");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Workspace, ".cursor", "commands"));
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, ".cursor", "commands", "leak.md"), fixture.Outside.Combine("evil.md"));

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        set.Commands.ShouldHaveSingleItem().Name.ShouldBe("fine");
        set.Diagnostics.ShouldContain(diagnostic => diagnostic.Kind == ConventionDiagnosticKind.EscapesRoot && diagnostic.Origin.EndsWith("leak.md", StringComparison.Ordinal));
    }

    /// <summary>A folder with far more entries than the discovery bound lists only the bound, so a huge folder costs one bounded call.</summary>
    [Fact]
    public async Task AHugeConventionFolderIsBounded()
    {
        using var fixture = RemoteFixture.Create();
        for (var index = 0; index < 60; index++)
        {
            _ = fixture.Write(".claude/commands/c" + index.ToString("D3", CultureInfo.InvariantCulture) + ".md", "x");
        }

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var set = await ConventionCatalog.DiscoverAsync(
            setup.Roots,
            setup.UserHome,
            setup.Sources,
            new ConventionDiscoveryOptions { MaxEntriesPerDirectory = 10 },
            Token).ConfigureAwait(true);

        set.Commands.Count.ShouldBe(10);
        set.Commands.Select(command => command.Name).First().ShouldBe("c000");
    }

    /// <summary>A host that does not answer never throws from the source: list and read report an Unreadable problem and exists is false.</summary>
    [Fact]
    public async Task AnUnreachableHostNeverThrows()
    {
        using var fixture = RemoteFixture.Create(new FakeRemoteHostOptions { Mode = "refused" });
        var source = SourceOver(fixture, fixture.Workspace);

        var listed = await source.ListAsync(fixture.Workspace, 10, Token).ConfigureAwait(true);
        var read = await source.ReadTextAsync(Path.Combine(fixture.Workspace, "x.md"), 100, Token).ConfigureAwait(true);
        var exists = await source.ExistsAsync(fixture.Workspace, Token).ConfigureAwait(true);
        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);

        listed.ProblemKind.ShouldBe(ConventionDiagnosticKind.Unreadable);
        listed.Problem.ShouldNotBeNull().ShouldContain("could not be reached", Case.Sensitive);
        read.IsRead.ShouldBeFalse();
        read.ProblemKind.ShouldBe(ConventionDiagnosticKind.Unreadable);
        exists.ShouldBeFalse();
        setup.UserHome.ShouldBeNull();
    }

    /// <summary>Hostile folder names (quotes, dollar signs, spaces) are one word to the remote shell and are found.</summary>
    [Fact]
    public async Task HostileFolderNamesAreOneWordToTheRemoteShell()
    {
        using var fixture = RemoteFixture.Create();
        var hostile = Path.Combine(fixture.Workspace, "it's $(touch pwned) `id`");
        _ = Directory.CreateDirectory(hostile);
        await File.WriteAllTextAsync(Path.Combine(hostile, "a.md"), "x", TestContext.Current.CancellationToken).ConfigureAwait(true);
        var source = SourceOver(fixture, fixture.Workspace);

        var listed = await source.ListAsync(hostile, 10, Token).ConfigureAwait(true);

        listed.Entries.ShouldHaveSingleItem().Name.ShouldBe("a.md");
        Directory.GetFileSystemEntries(fixture.Workspace, "pwned").ShouldBeEmpty();
    }
}
