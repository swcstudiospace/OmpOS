using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpManagedInstallerTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly OmpTestHost _host = new();

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task HappyPathInstallsTheLatestVersionAndActivatesIt()
    {
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"));

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var updated = result.ShouldBeOfType<OmpUpdated>();
        updated.Version.ShouldBe("2.0.0");
        updated.PreviousVersion.ShouldBeNull();
        var state = _host.Store().Load();
        state.ActiveVersion.ShouldBe("2.0.0");
        state.ActiveSha256.ShouldBe(OmpTestHost.Sha256Of(_host.ExecutablePath("2.0.0")));
        File.Exists(_host.ExecutablePath("2.0.0")).ShouldBeTrue();
        Directory.GetDirectories(_host.StagingDirectory).ShouldBeEmpty();
        Directory.GetFiles(_host.DownloadsDirectory).ShouldBeEmpty();
        _host.Registry.Requests.ShouldContain(OmpTestHost.BunUrl, StringComparer.Ordinal);
    }

    [Fact]
    public async Task IntegrityMismatchRemovesThePartFileAndCreatesNoVersion()
    {
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"), integritySource: Encoding.UTF8.GetBytes("different bytes"));

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpUpdateFailed>().Kind.ShouldBe(OmpFailureKind.IntegrityFailed);
        File.Exists(Path.Combine(_host.DownloadsDirectory, "2.0.0.part")).ShouldBeFalse();
        Directory.Exists(_host.VersionDirectory("2.0.0")).ShouldBeFalse();
    }

    [Fact]
    public async Task TruncatedBodyIsFailedAndRemovesThePartFile()
    {
        var tarball = TarballBuilder.Package("2.0.0");
        _host.Publish("2.0.0", tarball, declaredLength: tarball.Length + 50);

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpUpdateFailed>().Kind.ShouldBe(OmpFailureKind.PartialDownload);
        File.Exists(Path.Combine(_host.DownloadsDirectory, "2.0.0.part")).ShouldBeFalse();
        Directory.Exists(_host.VersionDirectory("2.0.0")).ShouldBeFalse();
    }

    [Fact]
    public async Task IncompatibleBunRangeIsRefusedBeforeAnyDownloadOrInstall()
    {
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"), engines: ">=1.5.0");

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var failed = result.ShouldBeOfType<OmpUpdateFailed>();
        failed.Kind.ShouldBe(OmpFailureKind.IncompatibleVersion);
        failed.Reason.ShouldContain("1.5.0");
        failed.Reason.ShouldContain("1.4.4");
        _host.Runner.Calls.ShouldBeEmpty();
        _host.Registry.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TraversalEntryIsRejectedBeforeAnythingIsWritten()
    {
        var tarball = TarballBuilder.Build(tar => TarballBuilder.AddFile(tar, "package/../escape.txt", Encoding.UTF8.GetBytes("escaped")));
        _host.Publish("2.0.0", tarball);

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpUpdateFailed>().Kind.ShouldBe(OmpFailureKind.UnsafeArchive);
        File.Exists(Path.Combine(_host.Root, "escape.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_host.Root, "omp", "escape.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(_host.StagingDirectory, "escape.txt")).ShouldBeFalse();
        Directory.Exists(_host.VersionDirectory("2.0.0")).ShouldBeFalse();
    }

    [Fact]
    public async Task SymlinkEntryIsRejectedBeforeAnythingIsWritten()
    {
        var tarball = TarballBuilder.Build(tar => TarballBuilder.AddLink(tar, "package/evil", "/etc"));
        _host.Publish("2.0.0", tarball);

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpUpdateFailed>().Kind.ShouldBe(OmpFailureKind.UnsafeArchive);
        Directory.Exists(_host.VersionDirectory("2.0.0")).ShouldBeFalse();
        Directory.GetDirectories(_host.StagingDirectory).ShouldBeEmpty();
    }

    [Fact]
    public async Task HealthFailureAfterActivationRollsBackAndQuarantinesTheNewVersion()
    {
        _host.SeedActive("1.0.0", null);
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"));
        _host.FailActivationOf = "2.0.0";

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var rolled = result.ShouldBeOfType<OmpRolledBack>();
        rolled.FailedVersion.ShouldBe("2.0.0");
        rolled.RestoredVersion.ShouldBe("1.0.0");
        var state = _host.Store().Load();
        state.ActiveVersion.ShouldBe("1.0.0");
        state.ActiveSha256.ShouldBe(OmpTestHost.Sha256Of(_host.ExecutablePath("1.0.0")));
        state.IsQuarantined("2.0.0").ShouldBeTrue();
    }

    [Fact]
    public async Task LockedRenameDefersKeepsTheStateAndStagingThenRetriesCleanly()
    {
        _host.SeedActive("1.0.0", null);
        var before = await File.ReadAllBytesAsync(_host.StatePath, Token);
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"));
        _host.FileSystem.FailMoves = true;

        var deferred = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        deferred.ShouldBeOfType<OmpDeferred>();
        (await File.ReadAllBytesAsync(_host.StatePath, Token)).ShouldBe(before);
        Directory.GetDirectories(_host.StagingDirectory).Length.ShouldBe(1);

        _host.FileSystem.FailMoves = false;
        var updated = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        updated.ShouldBeOfType<OmpUpdated>().Version.ShouldBe("2.0.0");
        _host.Runner.Calls.Count(call => call.Arguments.Count > 0 && string.Equals(call.Arguments[0], "build", StringComparison.Ordinal)).ShouldBe(1);
    }
}
