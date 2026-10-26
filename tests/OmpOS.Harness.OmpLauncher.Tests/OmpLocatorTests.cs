using Shouldly;
using Xunit;

namespace OmpOS.Harness.OmpLauncher.Tests;

public sealed class OmpLocatorTests : IDisposable
{
    private readonly FakeOmp _fake = new();

    /// <inheritdoc />
    public void Dispose() => _fake.Dispose();

    [Fact]
    public void ConfiguredPathWinsOverPath()
    {
        var configuredDirectory = _fake.CreateDirectory("configured");
        var pathDirectory = _fake.CreateDirectory("path");
        var configured = _fake.WriteExecutable(configuredDirectory, "exit 0");
        _fake.WriteExecutable(pathDirectory, "exit 0");

        var location = OmpLocator.Locate(new OmpLocatorOptions(configured, [pathDirectory], []));

        location.ShouldBe(new OmpFound(configured));
    }

    [Fact]
    public void PathDirectoriesAreSearchedInOrder()
    {
        var empty = _fake.CreateDirectory("empty");
        var first = _fake.CreateDirectory("first");
        var second = _fake.CreateDirectory("second");
        _fake.WriteExecutable(first, "exit 0");
        _fake.WriteExecutable(second, "exit 0");

        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [empty, first, second], []));

        location.ShouldBe(new OmpFound(Path.Combine(first, OmpLocator.ExecutableName)));
    }

    [Fact]
    public void NotFoundListsEveryTriedPathInSearchOrder()
    {
        var pathA = _fake.CreateDirectory("a");
        var pathB = _fake.CreateDirectory("b");
        var knownOne = Path.Combine(_fake.Root, "known-one", "omp");
        var knownTwo = Path.Combine(_fake.Root, "known-two", "omp");
        var configured = Path.Combine(_fake.Root, "configured", "omp");

        var location = OmpLocator.Locate(new OmpLocatorOptions(configured, [pathA, pathB], [knownOne, knownTwo]));

        var notFound = location.ShouldBeOfType<OmpNotFound>();
        notFound.TriedPaths.ShouldBe(
        [
            configured,
            Path.Combine(pathA, OmpLocator.ExecutableName),
            Path.Combine(pathB, OmpLocator.ExecutableName),
            knownOne,
            knownTwo,
        ]);
        notFound.InstallGuidance.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NotFoundWithNoCandidatesReportsNoTriedPaths()
    {
        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [], []));

        location.ShouldBeOfType<OmpNotFound>().TriedPaths.ShouldBeEmpty();
    }

    [Fact]
    public void KnownInstallLocationIsUsedWhenPathHasNoOmp()
    {
        var pathDirectory = _fake.CreateDirectory("path");
        var knownDirectory = _fake.CreateDirectory("known");
        var known = _fake.WriteExecutable(knownDirectory, "exit 0");

        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [pathDirectory], [known]));

        location.ShouldBe(new OmpFound(known));
    }

    [Fact]
    public void PathWinsOverKnownInstallLocation()
    {
        var pathDirectory = _fake.CreateDirectory("path");
        var knownDirectory = _fake.CreateDirectory("known");
        var onPath = _fake.WriteExecutable(pathDirectory, "exit 0");
        var known = _fake.WriteExecutable(knownDirectory, "exit 0");
        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [pathDirectory], [known]));

        location.ShouldBe(new OmpFound(onPath));
    }

    [Fact]
    public void NonExecutableFileIsSkippedAndSearchContinues()
    {
        var blocked = _fake.CreateDirectory("blocked");
        var runnable = _fake.CreateDirectory("runnable");
        var nonExecutable = _fake.WriteNonExecutable(blocked);
        var executable = _fake.WriteExecutable(runnable, "exit 0");

        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [blocked, runnable], []));

        location.ShouldBe(new OmpFound(executable));
        var blockedOnly = OmpLocator.Locate(new OmpLocatorOptions(null, [blocked], []));
        blockedOnly.ShouldBeOfType<OmpNotFound>().TriedPaths.ShouldBe([nonExecutable]);
    }

    [Fact]
    public void NonExecutableConfiguredPathFallsThroughToPath()
    {
        var configuredDirectory = _fake.CreateDirectory("configured");
        var pathDirectory = _fake.CreateDirectory("path");
        var configured = _fake.WriteNonExecutable(configuredDirectory);
        var onPath = _fake.WriteExecutable(pathDirectory, "exit 0");

        var location = OmpLocator.Locate(new OmpLocatorOptions(configured, [pathDirectory], []));

        location.ShouldBe(new OmpFound(onPath));
    }

    [Fact]
    public void RelativeConfiguredPathIsRejected()
    {
        Should.Throw<ArgumentException>(() =>
        {
            OmpLocator.Locate(new OmpLocatorOptions("bin/omp", [], []));
        });
    }

    [Fact]
    public void RelativePathEntriesAreNeverSearched()
    {
        var relativeDirectory = Path.Combine("ompos-relative-dir-not-real", Guid.NewGuid().ToString("N"));

        var location = OmpLocator.Locate(new OmpLocatorOptions(null, [relativeDirectory, string.Empty], []));

        location.ShouldBeOfType<OmpNotFound>().TriedPaths.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultKnownInstallLocationsListHomeEntriesThenSystemEntries()
    {
        var home = Path.Combine(_fake.Root, "home");

        var locations = OmpLocator.DefaultKnownInstallLocations(home);

        locations.ShouldBe(
        [
            Path.Combine(home, ".bun", "bin", OmpLocator.ExecutableName),
            Path.Combine(home, ".local", "bin", OmpLocator.ExecutableName),
            Path.Combine("/usr/local/bin", OmpLocator.ExecutableName),
            Path.Combine("/usr/bin", OmpLocator.ExecutableName),
        ]);
    }

    [Fact]
    public void DefaultKnownInstallLocationsOmitHomeEntriesWithoutHome()
    {
        var locations = OmpLocator.DefaultKnownInstallLocations(string.Empty);

        locations.Count.ShouldBe(2);
        locations.ShouldAllBe(path => Path.IsPathRooted(path));
    }
}
