using System.Text;
using Autonogrammer.Harness.Remote.Containers;
using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// The exec layer, the file system and the convention discovery of a container: the same code that serves an ssh host runs over <c>podman exec</c> through
/// <see cref="IRemoteExec"/>, so these tests prove that the generalisation holds for scripts, hostile names, containment and the stale-content guard.
/// </summary>
public sealed class PodmanRemoteExecTests
{
    private const int Cap = WorkspaceFileLimits.DefaultMaxReadBytes;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Stdin reaches the script, the output is capped and says so, and the exit code and stderr are reported.</summary>
    [Fact]
    public async Task StdinOutputExitCodeAndErrorAreReported()
    {
        using var host = FakeContainerHost.Create();
        using var exec = new PodmanRemoteExec(host.Profile, host.ClientOptions);

        var echoed = await exec.RunAsync("cat", Encoding.UTF8.GetBytes("hello"), 100, Token).ConfigureAwait(true);
        var capped = await exec.RunAsync("printf 0123456789", ReadOnlyMemory<byte>.Empty, 4, Token).ConfigureAwait(true);
        var failed = await exec.RunAsync("echo oops >&2; exit 7", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);

        echoed.Succeeded.ShouldBeTrue();
        echoed.OutputText().ShouldBe("hello");
        capped.OutputText().ShouldBe("0123");
        capped.OutputTruncated.ShouldBeTrue();
        failed.ExitCode.ShouldBe(7);
        failed.IsConnectionFailure.ShouldBeFalse();
        failed.Error.Trim().ShouldBe("oops");
    }

    /// <summary>The home directory is the one of the user inside the container, and the workspace root is the profile's folder.</summary>
    [Fact]
    public async Task TheHomeAndTheRootAreTheContainers()
    {
        using var host = FakeContainerHost.Create();
        using var exec = new PodmanRemoteExec(host.Profile, host.ClientOptions);

        (await exec.ResolveHomeDirectoryAsync(Token).ConfigureAwait(true)).ShouldBe(host.HomeDirectory);
        exec.WorkspaceRoot.ShouldBe(host.WorkspaceDirectory);
        ((IRemoteExec)exec).WorkspaceRoot.ShouldBe(host.WorkspaceDirectory);
    }

    /// <summary>
    /// A container that does not exist, a stopped container and a down service are connection failures (podman's own exit code 125 is reported as 255, as ssh's is),
    /// and a missing podman is a start failure. Each result says so in words and keeps podman's text.
    /// </summary>
    [Fact]
    public async Task PodmanFailuresAreConnectionFailures()
    {
        using var host = FakeContainerHost.Create();
        using var stopped = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited" });
        using var down = FakeContainerHost.Create(new FakeContainerHostOptions { Mode = "service-down" });
        using var ghost = new PodmanRemoteExec(host.Profile with { Container = "ghost" }, host.ClientOptions);
        using var stoppedExec = new PodmanRemoteExec(stopped.Profile, stopped.ClientOptions);
        using var downExec = new PodmanRemoteExec(down.Profile, down.ClientOptions);
        using var missing = new PodmanRemoteExec(host.Profile, new PodmanClientOptions { PodmanPath = Path.Combine(host.Root, "no-such-podman") });

        var ghostResult = await ghost.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);
        var stoppedResult = await stoppedExec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);
        var downResult = await downExec.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);
        var missingResult = await missing.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);

        ghostResult.ExitCode.ShouldBe(255);
        ghostResult.IsConnectionFailure.ShouldBeTrue();
        ghostResult.DescribeConnectionFailure().ShouldContain("no such container", Case.Insensitive);
        stoppedResult.IsConnectionFailure.ShouldBeTrue();
        stoppedResult.DescribeConnectionFailure().ShouldContain("running containers", Case.Sensitive);
        downResult.IsConnectionFailure.ShouldBeTrue();
        missingResult.StartFailure.ShouldNotBeNull();
        missingResult.DescribeConnectionFailure().ShouldContain("could not be started", Case.Sensitive);
    }

    /// <summary>A script that spans several lines, and a profile that fails validation, are refused before podman starts.</summary>
    [Fact]
    public async Task ABadScriptOrProfileIsRefused()
    {
        using var host = FakeContainerHost.Create();
        using var exec = new PodmanRemoteExec(host.Profile, host.ClientOptions);

        await Should.ThrowAsync<ArgumentException>(() => exec.RunAsync("true\nrm -rf x", ReadOnlyMemory<byte>.Empty, 10, Token)).ConfigureAwait(true);
        Should.Throw<ArgumentException>(() => new PodmanRemoteExec(host.Profile with { Container = "--privileged" }, host.ClientOptions));

        host.PodmanCalls().ShouldBeEmpty();
    }

    /// <summary>Files and directories are listed with their kinds, including names with spaces, quotes, dollars and backticks.</summary>
    [Fact]
    public async Task ListingShowsKindsAndHostileNames()
    {
        using var fixture = ContainerFixture.Create();
        const string Hostile = "it's \"q\" $HOME `id`.txt";
        _ = fixture.Write("a b.txt", "x");
        _ = fixture.Write("sub/c.md", "y");
        _ = fixture.Write(Hostile, "w");

        var entries = await fixture.FileSystem().ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);

        entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).Select(entry => (entry.Name, entry.IsDirectory)).ToArray().ShouldBe(
        [
            ("a b.txt", false),
            (Hostile, false),
            ("sub", true),
        ]);
    }

    /// <summary>A file reads back, a write checks the expected text, and a stale write is refused with the file untouched.</summary>
    [Fact]
    public async Task ReadWriteAndTheStaleGuardWork()
    {
        using var fixture = ContainerFixture.Create();
        var path = fixture.Write("f.txt", "one");
        var fileSystem = fixture.FileSystem();

        (await fileSystem.ReadFileAsync("f.txt", Cap, Token).ConfigureAwait(true)).ShouldBe("one");
        await fileSystem.WriteFileAsync("f.txt", "two", "one", Token).ConfigureAwait(true);
        await Should.ThrowAsync<StaleWorkspaceFileException>(() => fileSystem.WriteFileAsync("f.txt", "three", "one", Token)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe("two");
    }

    /// <summary>Create makes parents and refuses an existing path; delete checks the expected text.</summary>
    [Fact]
    public async Task CreateAndDeleteWork()
    {
        using var fixture = ContainerFixture.Create();
        _ = fixture.Write("taken.txt", "x");
        _ = fixture.Write("gone.txt", "bye");
        var fileSystem = fixture.FileSystem();

        await fileSystem.CreateFileAsync("new/deep/f.txt", "made", Token).ConfigureAwait(true);
        await Should.ThrowAsync<IOException>(() => fileSystem.CreateFileAsync("taken.txt", "again", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<StaleWorkspaceFileException>(() => fileSystem.DeleteFileAsync("gone.txt", "other", Token)).ConfigureAwait(true);
        await fileSystem.DeleteFileAsync("gone.txt", "bye", Token).ConfigureAwait(true);

        (await File.ReadAllTextAsync(Path.Combine(fixture.Workspace, "new", "deep", "f.txt"), Token).ConfigureAwait(true)).ShouldBe("made");
        File.Exists(Path.Combine(fixture.Workspace, "gone.txt")).ShouldBeFalse();
    }

    /// <summary>Nothing outside the workspace root is read or written through a link, and a climbing path is refused before podman runs.</summary>
    [Fact]
    public async Task ContainmentHoldsOverPodman()
    {
        using var fixture = ContainerFixture.Create();
        await File.WriteAllTextAsync(fixture.Outside.Combine("secret.txt"), "TOP-SECRET", Token).ConfigureAwait(true);
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "escape"), fixture.Outside.Path);
        var fileSystem = fixture.FileSystem();

        var error = await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.ReadFileAsync("escape/secret.txt", Cap, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.CreateFileAsync("escape/new.txt", "leak", Token)).ConfigureAwait(true);
        var callsBefore = fixture.Host.PodmanCalls().Count;
        await Should.ThrowAsync<ArgumentException>(() => fileSystem.ReadFileAsync("../outside.txt", Cap, Token)).ConfigureAwait(true);

        error.Message.ShouldNotContain("TOP-SECRET", Case.Sensitive);
        Directory.GetFileSystemEntries(fixture.Outside.Path).Length.ShouldBe(1);
        fixture.Host.PodmanCalls().Count.ShouldBe(callsBefore);
    }

    /// <summary>A stopped container surfaces as an <see cref="IOException"/> that carries podman's words, not as an empty listing.</summary>
    [Fact]
    public async Task AStoppedContainerFailsTheFileSystem()
    {
        using var fixture = ContainerFixture.Create(new FakeContainerHostOptions { State = "exited" });

        var error = await Should.ThrowAsync<IOException>(() => fixture.FileSystem().ListDirectoryAsync(string.Empty, Token)).ConfigureAwait(true);

        error.Message.ShouldContain("running containers", Case.Sensitive);
    }

    /// <summary>The convention discovery sees the commands of the container workspace and of the home directory inside the container.</summary>
    [Fact]
    public async Task ConventionsAreDiscoveredInsideTheContainer()
    {
        using var fixture = ContainerFixture.Create();
        _ = fixture.Write(".claude/commands/ship.md", "Ship it\n");
        var homeCommands = Path.Combine(fixture.Host.HomeDirectory, ".claude", "commands");
        _ = Directory.CreateDirectory(homeCommands);
        await File.WriteAllTextAsync(Path.Combine(homeCommands, "review.md"), "Review it\n", Token).ConfigureAwait(true);

        var setup = await RemoteConventionSources.CreateAsync(fixture.Exec, null, Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        setup.UserHome.ShouldBe(fixture.Host.HomeDirectory);
        setup.Roots.ShouldBe([fixture.Workspace]);
        set.Commands.Select(command => command.Name).Order(StringComparer.Ordinal).ToArray().ShouldBe(["review", "ship"]);
    }

    /// <summary>The same file system and exec code runs over <c>wsl.exe</c>: the distribution's files list, read and write through <see cref="WslRemoteExec"/>.</summary>
    [Fact]
    public async Task TheSameCodeRunsOverWsl()
    {
        using var host = FakeWslHost.Create();
        using var exec = new WslRemoteExec(host.Profile, host.ClientOptions);
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, "f.txt"), "one", Token).ConfigureAwait(true);
        var fileSystem = new RemoteWorkspaceFileSystem(exec);

        var entries = await fileSystem.ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);
        await fileSystem.WriteFileAsync("f.txt", "two", "one", Token).ConfigureAwait(true);

        entries.ShouldHaveSingleItem().Name.ShouldBe("f.txt");
        (await File.ReadAllTextAsync(Path.Combine(host.WorkspaceDirectory, "f.txt"), Token).ConfigureAwait(true)).ShouldBe("two");
        (await exec.ResolveHomeDirectoryAsync(Token).ConfigureAwait(true)).ShouldBe(host.HomeDirectory);
        exec.WorkspaceRoot.ShouldBe(host.WorkspaceDirectory);
    }

    /// <summary>A distribution that does not exist is a connection failure with wsl's words; a missing wsl.exe is a start failure.</summary>
    [Fact]
    public async Task WslFailuresAreConnectionFailures()
    {
        using var host = FakeWslHost.Create();
        using var ghost = new WslRemoteExec(host.Profile with { Distro = "Nope" }, host.ClientOptions);
        using var missing = new WslRemoteExec(host.Profile, new WslClientOptions { WslPath = Path.Combine(host.Root, "no-such-wsl.exe") });

        var ghostResult = await ghost.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);
        var missingResult = await missing.RunAsync("true", ReadOnlyMemory<byte>.Empty, 10, Token).ConfigureAwait(true);

        ghostResult.IsConnectionFailure.ShouldBeTrue();
        ghostResult.Error.ShouldContain("no distribution", Case.Insensitive);
        missingResult.StartFailure.ShouldNotBeNull();
    }
}
