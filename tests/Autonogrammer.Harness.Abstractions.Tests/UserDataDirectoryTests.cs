using System.Globalization;
using System.Runtime.Versioning;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Abstractions.Tests;

/// <summary>
/// The shared per-user data directory resolver: the legacy directory is moved once and atomically, a new file is never overwritten, the legacy
/// directory is never read afterwards, both present means the new one wins with a warning, an unreadable legacy directory is left alone, and two
/// racing starters perform one migration.
/// </summary>
public sealed class UserDataDirectoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly string _parent = Path.Combine(Path.GetTempPath(), "autonogrammer-userdata-" + Guid.NewGuid().ToString("N"));

    private string Legacy => Path.Combine(_parent, LegacyBrandNames.UserDataDirectory);

    private string Current => Path.Combine(_parent, UserDataDirectory.CurrentName);

    public void Dispose()
    {
        if (Directory.Exists(_parent))
        {
            // A test that revoked access restores it itself; this only guards a failed assertion in between.
            foreach (var directory in Directory.EnumerateDirectories(_parent, "*", SearchOption.TopDirectoryOnly))
            {
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }

            Directory.Delete(_parent, recursive: true);
        }
    }

    /// <summary>The current name is the product name and the paths sit beside each other under the parent root.</summary>
    [Fact]
    public void PathsAreSiblingsUnderTheParentRoot()
    {
        UserDataDirectory.CurrentName.ShouldBe("Autonogrammer");
        UserDataDirectory.PathFor(_parent).ShouldBe(Current);
        UserDataDirectory.LegacyPath(_parent).ShouldBe(Legacy);
    }

    /// <summary>A blank parent root is refused.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankParentRootIsRefused(string parent)
    {
        Should.Throw<ArgumentException>(() => UserDataDirectory.PathFor(parent));
        Should.Throw<ArgumentException>(() => UserDataDirectory.LegacyPath(parent));
        Should.Throw<ArgumentException>(() => UserDataDirectory.EnsureCurrent(parent));
    }

    /// <summary>Without a legacy directory nothing is created, locked or reported.</summary>
    [Fact]
    public void NoLegacyDirectoryIsANoOpThatCreatesNothing()
    {
        Directory.CreateDirectory(_parent);

        var result = UserDataDirectory.EnsureCurrent(_parent);

        result.ShouldBe(new UserDataMigration(Current, false, null));
        Directory.EnumerateFileSystemEntries(_parent).ShouldBeEmpty();
    }

    /// <summary>A legacy-only parent moves to the new directory with identical bytes, a marker with the time, and the legacy path gone.</summary>
    [Fact]
    public void ALegacyOnlyParentMovesOnceWithAMarker()
    {
        Seed(Legacy, ("mcp-state.json", "legacy-mcp"), ("nested/environments.json", "legacy-environments"));
        var warnings = new List<string>();

        var result = UserDataDirectory.EnsureCurrent(_parent, warnings.Add, new FixedClock(Now));

        result.Migrated.ShouldBeTrue();
        result.Path.ShouldBe(Current);
        result.Warning.ShouldBeNull();
        warnings.ShouldBeEmpty();
        Directory.Exists(Legacy).ShouldBeFalse();
        File.ReadAllText(Path.Combine(Current, "mcp-state.json")).ShouldBe("legacy-mcp");
        File.ReadAllText(Path.Combine(Current, "nested", "environments.json")).ShouldBe("legacy-environments");
        var marker = File.ReadAllText(Path.Combine(Current, UserDataDirectory.MarkerFileName));
        marker.ShouldContain("migratedUtc");
        marker.ShouldContain(Now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        File.Exists(Path.Combine(_parent, UserDataDirectory.LockFileName)).ShouldBeFalse();
        Directory.EnumerateFileSystemEntries(_parent).Select(Path.GetFileName).ShouldBe([UserDataDirectory.CurrentName]);
    }

    /// <summary>A parent that was migrated before reruns as a no-op: no second move, no warning, no byte touched.</summary>
    [Fact]
    public void AnAlreadyMigratedParentIsLeftUntouched()
    {
        Seed(Legacy, ("mcp-state.json", "legacy-mcp"));
        UserDataDirectory.EnsureCurrent(_parent, null, new FixedClock(Now)).Migrated.ShouldBeTrue();
        var before = Snapshot(Current);

        var result = UserDataDirectory.EnsureCurrent(_parent, null, new FixedClock(Now.AddHours(1)));

        result.ShouldBe(new UserDataMigration(Current, false, null));
        Snapshot(Current).ShouldBe(before);
    }

    /// <summary>With both directories present the new one wins, the legacy tree is byte-identical afterwards, and a warning is returned and sent to the sink.</summary>
    [Fact]
    public void BothPresentKeepsTheNewDirectoryTouchesNoLegacyByteAndWarns()
    {
        Seed(Legacy, ("mcp-state.json", "legacy-mcp"), ("only-legacy.json", "legacy-only"));
        Seed(Current, ("mcp-state.json", "new-mcp"));
        var legacyBefore = Snapshot(Legacy);
        var currentBefore = Snapshot(Current);
        var warnings = new List<string>();

        var result = UserDataDirectory.EnsureCurrent(_parent, warnings.Add, new FixedClock(Now));

        result.Migrated.ShouldBeFalse();
        result.Path.ShouldBe(Current);
        result.Warning.ShouldNotBeNull();
        result.Warning.ShouldContain(UserDataDirectory.CurrentName);
        result.Warning.ShouldContain(LegacyBrandNames.UserDataDirectory);
        warnings.ShouldBe([result.Warning]);
        Snapshot(Legacy).ShouldBe(legacyBefore);
        Snapshot(Current).ShouldBe(currentBefore);
        File.ReadAllText(Path.Combine(Current, "mcp-state.json")).ShouldBe("new-mcp");
        File.Exists(Path.Combine(Current, "only-legacy.json")).ShouldBeFalse();
    }

    /// <summary>A new file is never overwritten and the legacy directory is never read after the start: repeated starts keep serving the new bytes only.</summary>
    [Fact]
    public void ANewFileIsNeverOverwrittenAndTheLegacyDirectoryIsNeverReadAgain()
    {
        Seed(Legacy, ("setup-consent.json", "legacy-consent"));
        Seed(Current, ("setup-consent.json", "new-consent"));
        var legacyBefore = Snapshot(Legacy);
        var reads = new List<string>();

        for (var start = 0; start < 3; start++)
        {
            var result = UserDataDirectory.EnsureCurrent(_parent, null, new FixedClock(Now), directory => RecordRead(reads, directory));
            result.Path.ShouldBe(Current);
            result.Migrated.ShouldBeFalse();
        }

        File.ReadAllText(Path.Combine(Current, "setup-consent.json")).ShouldBe("new-consent");
        Snapshot(Legacy).ShouldBe(legacyBefore);
        reads.ShouldBeEmpty("both present decides from the two directories existing, without listing the legacy directory.");
    }

    /// <summary>A legacy directory that cannot be listed is left alone: the new directory is used, a warning is returned, nothing throws.</summary>
    [Fact]
    public void AnUnreadableLegacyDirectoryIsLeftAloneWithAWarning()
    {
        Seed(Legacy, ("mcp-state.json", "legacy-mcp"));
        var legacyBefore = Snapshot(Legacy);
        var warnings = new List<string>();

        var result = UserDataDirectory.EnsureCurrent(
            _parent,
            warnings.Add,
            new FixedClock(Now),
            static _ => throw new UnauthorizedAccessException("access denied"));

        result.Migrated.ShouldBeFalse();
        result.Path.ShouldBe(Current);
        result.Warning.ShouldNotBeNull();
        result.Warning.ShouldContain(LegacyBrandNames.UserDataDirectory);
        result.Warning.ShouldNotContain("access denied");
        warnings.ShouldBe([result.Warning]);
        Directory.Exists(Current).ShouldBeFalse();
        Snapshot(Legacy).ShouldBe(legacyBefore);
    }

    /// <summary>A legacy directory whose listing fails with an I/O error is left alone as well.</summary>
    [Fact]
    public void ALegacyDirectoryThatFailsWithAnIoErrorIsLeftAloneWithAWarning()
    {
        Seed(Legacy, ("mcp-state.json", "legacy-mcp"));

        var result = UserDataDirectory.EnsureCurrent(_parent, null, new FixedClock(Now), static _ => throw new IOException("disk gone"));

        result.Migrated.ShouldBeFalse();
        result.Warning.ShouldNotBeNull();
        Directory.Exists(Current).ShouldBeFalse();
        File.ReadAllText(Path.Combine(Legacy, "mcp-state.json")).ShouldBe("legacy-mcp");
    }

    /// <summary>A legacy directory with its access revoked on Linux or macOS is left alone. Skipped for a privileged process, which ignores permissions.</summary>
    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ALegacyDirectoryWithRevokedAccessIsLeftAloneWithAWarning()
    {
        if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
        {
            Assert.Skip("Revoked access is only observable for an unprivileged process on a Unix file system.");
        }

        Seed(Legacy, ("mcp-state.json", "legacy-mcp"));
        File.SetUnixFileMode(Legacy, UnixFileMode.None);

        var result = UserDataDirectory.EnsureCurrent(_parent, null, new FixedClock(Now));

        File.SetUnixFileMode(Legacy, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        result.Migrated.ShouldBeFalse();
        result.Path.ShouldBe(Current);
        result.Warning.ShouldNotBeNull();
        Directory.Exists(Current).ShouldBeFalse();
        File.ReadAllText(Path.Combine(Legacy, "mcp-state.json")).ShouldBe("legacy-mcp");
    }

    /// <summary>Two starters racing on one legacy parent perform one migration: one reports Migrated, both return the same path, nothing is duplicated or partial.</summary>
    [Fact]
    public async Task TwoRacingStartersPerformOneMigration()
    {
        var ct = TestContext.Current.CancellationToken;
        for (var round = 0; round < 25; round++)
        {
            var parent = Path.Combine(_parent, "race-" + round.ToString(CultureInfo.InvariantCulture));
            Seed(Path.Combine(parent, LegacyBrandNames.UserDataDirectory), ("mcp-state.json", "legacy-mcp"), ("nested/environments.json", "legacy-environments"));
            using var start = new ManualResetEventSlim(false);
            var first = Task.Run(() => RaceOne(parent, start), ct);
            var second = Task.Run(() => RaceOne(parent, start), ct);
            start.Set();

            var results = await Task.WhenAll(first, second).ConfigureAwait(true);

            results.Count(static result => result.Migrated).ShouldBe(1);
            results.Select(static result => result.Path).Distinct(StringComparer.Ordinal).Count().ShouldBe(1);
            results.ShouldAllBe(static result => result.Warning == null);
            var current = Path.Combine(parent, UserDataDirectory.CurrentName);
            (await File.ReadAllTextAsync(Path.Combine(current, "mcp-state.json"), ct).ConfigureAwait(true)).ShouldBe("legacy-mcp");
            (await File.ReadAllTextAsync(Path.Combine(current, "nested", "environments.json"), ct).ConfigureAwait(true)).ShouldBe("legacy-environments");
            Directory.EnumerateFileSystemEntries(parent).Select(Path.GetFileName).ShouldBe([UserDataDirectory.CurrentName]);
        }
    }

    private static UserDataMigration RaceOne(string parent, ManualResetEventSlim start)
    {
        start.Wait();
        return UserDataDirectory.EnsureCurrent(parent, null, new FixedClock(Now));
    }

    private static IEnumerable<string> RecordRead(List<string> reads, string directory)
    {
        reads.Add(directory);
        return Directory.EnumerateFileSystemEntries(directory);
    }

    private static void Seed(string directory, params (string RelativePath, string Content)[] files)
    {
        Directory.CreateDirectory(directory);
        foreach (var (relative, content) in files)
        {
            var path = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }

    private static Dictionary<string, string> Snapshot(string directory)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, path);
            snapshot[relative] = Directory.Exists(path) ? "<directory>" : Convert.ToBase64String(File.ReadAllBytes(path));
        }

        return snapshot;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
