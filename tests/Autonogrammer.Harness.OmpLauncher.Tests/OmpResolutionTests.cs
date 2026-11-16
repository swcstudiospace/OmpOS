using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.OmpLauncher.Tests;

public sealed class OmpResolutionTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly OmpTestHost _host = new();
    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose()
    {
        _host.Dispose();
        _fake.Dispose();
    }

    [Fact]
    public async Task ConfiguredPathWinsOverAValidManagedInstallAndPath()
    {
        _host.SeedActive("1.0.0", null);
        var configured = _fake.WriteExecutable(_fake.CreateDirectory("configured"), "exit 0");
        var pathDirectory = _fake.CreateDirectory("path");
        _fake.WriteExecutable(pathDirectory, "exit 0");

        var resolution = await OmpResolution.ResolveAsync(_host.Options, configured, new OmpLocatorOptions(null, [pathDirectory], []), Token);

        resolution.Source.ShouldBe(OmpResolutionSource.Configured);
        resolution.ExecutablePath.ShouldBe(configured);
        _host.Runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidManagedInstallBeatsPath()
    {
        _host.SeedActive("1.0.0", null);
        var pathDirectory = _fake.CreateDirectory("path");
        _fake.WriteExecutable(pathDirectory, "exit 0");

        var resolution = await OmpResolution.ResolveAsync(_host.Options, null, new OmpLocatorOptions(null, [pathDirectory], []), Token);

        resolution.Source.ShouldBe(OmpResolutionSource.Managed);
        resolution.ExecutablePath.ShouldBe(_host.ExecutablePath("1.0.0"));
    }

    [Fact]
    public async Task PathIsUsedWhenNoManagedInstallExists()
    {
        var pathDirectory = _fake.CreateDirectory("path");
        var onPath = _fake.WriteExecutable(pathDirectory, "exit 0");

        var resolution = await OmpResolution.ResolveAsync(_host.Options, null, new OmpLocatorOptions(null, [pathDirectory], []), Token);

        resolution.Source.ShouldBe(OmpResolutionSource.Path);
        resolution.ExecutablePath.ShouldBe(onPath);
    }

    [Fact]
    public async Task ManagedInstallWithADigestMismatchIsNotRunAndPathIsUsed()
    {
        _host.SeedActive("1.0.0", null);
        _host.Store().Save(new OmpManagedState("1.0.0", "00", null, null, null));
        var pathDirectory = _fake.CreateDirectory("path");
        var onPath = _fake.WriteExecutable(pathDirectory, "exit 0");

        var resolution = await OmpResolution.ResolveAsync(_host.Options, null, new OmpLocatorOptions(null, [pathDirectory], []), Token);

        resolution.Source.ShouldBe(OmpResolutionSource.Path);
        resolution.ExecutablePath.ShouldBe(onPath);
        _host.Runner.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task NoneIsReportedWhenNothingIsInstalled()
    {
        var empty = _fake.CreateDirectory("empty");

        var resolution = await OmpResolution.ResolveAsync(_host.Options, null, new OmpLocatorOptions(null, [empty], []), Token);

        resolution.Source.ShouldBe(OmpResolutionSource.None);
        resolution.ExecutablePath.ShouldBeNull();
    }
}
