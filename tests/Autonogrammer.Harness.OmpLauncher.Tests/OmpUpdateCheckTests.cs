using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpUpdateCheckTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly OmpTestHost _host = new();

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task OfflineKeepsTheActiveVersionAndShowsWhenItWasLastChecked()
    {
        var lastChecked = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero);
        _host.SeedActive("1.0.0", lastChecked);
        _host.Registry.Route(OmpTestHost.PackageUrl, () => throw new HttpRequestException("no network"));

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var offline = result.ShouldBeOfType<OmpOffline>();
        offline.StaleVersion.ShouldBe("1.0.0");
        offline.LastCheckedAt.ShouldBe(lastChecked);
        _host.Store().Load().ActiveVersion.ShouldBe("1.0.0");
        _host.Runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task TlsInterceptionIsReportedAsATrustProblemNotAsOffline()
    {
        _host.SeedActive("1.0.0", null);
        _host.Registry.Route(
            OmpTestHost.PackageUrl,
            () => throw new HttpRequestException("the SSL connection could not be established", new System.Security.Authentication.AuthenticationException("certificate rejected")));

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var failed = result.ShouldBeOfType<OmpUpdateFailed>();
        failed.Kind.ShouldBe(OmpFailureKind.TlsTrust);
        failed.Reason.ShouldContain("trust store");
    }

    [Fact]
    public async Task UpToDateRecordsTheCheckTimeWithoutRunningAnything()
    {
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"));
        _host.SeedActive("2.0.0", null);

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpUpToDate>().Version.ShouldBe("2.0.0");
        _host.Store().Load().LastCheckedAt.ShouldBe(_host.Clock.GetUtcNow());
        _host.Runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task CorruptActiveExecutableIsReportedBrokenAndIsNeverRun()
    {
        _host.SeedActive("1.0.0", null);
        _host.Store().Save(new OmpManagedState("1.0.0", "00", null, null, null));

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        var broken = result.ShouldBeOfType<OmpBroken>();
        broken.Version.ShouldBe("1.0.0");
        broken.RollbackAvailable.ShouldBeFalse();
        _host.Runner.Calls.ShouldBeEmpty();
        _host.Registry.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task LockedActiveExecutableDefersWithoutTouchingTheState()
    {
        _host.SeedActive("1.0.0", null);
        var before = await File.ReadAllBytesAsync(_host.StatePath, Token);
        _host.Publish("2.0.0", TarballBuilder.Package("2.0.0"));
        _host.FileSystem.LockedPath = _host.ExecutablePath("1.0.0");

        var result = await _host.UpdateCheck().CheckForUpdateAsync(Token);

        result.ShouldBeOfType<OmpDeferred>();
        (await File.ReadAllBytesAsync(_host.StatePath, Token)).ShouldBe(before);
        _host.Registry.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RollBackRestoresThePreviousGoodVersionWhenTheActiveOneIsBroken()
    {
        _host.WriteExecutable("1.0.0");
        _host.WriteExecutable("2.0.0");
        _host.Store().Save(new OmpManagedState("2.0.0", "00", "1.0.0", null, null));

        var result = await _host.UpdateCheck().RollBackBrokenAsync(Token);

        var rolled = result.ShouldBeOfType<OmpRolledBack>();
        rolled.FailedVersion.ShouldBe("2.0.0");
        rolled.RestoredVersion.ShouldBe("1.0.0");
        _host.Store().Load().ActiveVersion.ShouldBe("1.0.0");
    }
}
