using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Composition;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The native head over the per-user data directory: a configured state directory is used verbatim and never migrated, the default roots
/// (<c>XDG_DATA_HOME</c> or <c>~/.local/share</c>, and the application data folder for the head services) move a legacy directory once, and a
/// both-present warning reaches the logger of the head services.
/// </summary>
public sealed class UserDataDirectoryMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "autonogrammer-userdata-native-" + Guid.NewGuid().ToString("N"));

    private string Home => Path.Combine(_root, "home");

    private string Workspace => Path.Combine(_root, "workspace");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A configured state directory is returned verbatim and nothing is migrated: the legacy sibling, the marker and the warning stay out of it.</summary>
    [Fact]
    public void AConfiguredStateDirectoryIsUsedVerbatimAndNeverMigrated()
    {
        var dataHome = Path.Combine(Home, ".local", "share");
        SeedLegacy(dataHome);
        var configured = Path.Combine(_root, "configured-state");
        Func<string, string?> variables = name => string.Equals(name, AgentLoopEnvironment.StateDirectoryVariable, StringComparison.Ordinal) ? configured : null;
        var legacyBefore = Directory.GetFileSystemEntries(dataHome);

        var migration = AgentLoopEnvironment.EnsureStateRootMigrated(variables, Home);
        var environment = AgentLoopEnvironment.Resolve(variables, Workspace, Workspace, Home);

        migration.ShouldBeNull();
        environment.StateRoot.ShouldBe(configured);
        Directory.GetFileSystemEntries(dataHome).ShouldBe(legacyBefore);
        Directory.Exists(UserDataDirectory.PathFor(dataHome)).ShouldBeFalse();
    }

    /// <summary>With the data-home variable set the state root is the new directory under it, and a legacy sibling there is migrated once.</summary>
    [Fact]
    public void TheDataHomeVariableRootsTheStateAndMigratesItsLegacySiblingOnce()
    {
        var xdg = Path.Combine(_root, "xdg");
        SeedLegacy(xdg);
        Func<string, string?> variables = name => string.Equals(name, "XDG_DATA_HOME", StringComparison.Ordinal) ? xdg : null;

        var first = AgentLoopEnvironment.EnsureStateRootMigrated(variables, Home);
        var second = AgentLoopEnvironment.EnsureStateRootMigrated(variables, Home);
        var environment = AgentLoopEnvironment.Resolve(variables, Workspace, Workspace, Home);

        first.ShouldNotBeNull().Migrated.ShouldBeTrue();
        second.ShouldNotBeNull().Migrated.ShouldBeFalse();
        environment.StateRoot.ShouldBe(UserDataDirectory.PathFor(xdg));
        File.ReadAllText(Path.Combine(environment.StateRoot, "state.json")).ShouldBe("legacy-state");
        Directory.Exists(Path.Combine(xdg, LegacyBrandNames.UserDataDirectory)).ShouldBeFalse();
    }

    /// <summary>Without any variable the default root is the home directory's <c>.local/share</c>, migrated once, with both-present keeping the new directory and returning a warning.</summary>
    [Fact]
    public void TheDefaultRootMigratesOnceAndBothPresentKeepsTheNewDirectoryWithAWarning()
    {
        var dataHome = Path.Combine(Home, ".local", "share");
        SeedLegacy(dataHome);
        Func<string, string?> variables = static _ => null;

        var first = AgentLoopEnvironment.EnsureStateRootMigrated(variables, Home);

        first.ShouldNotBeNull().Migrated.ShouldBeTrue();
        AgentLoopEnvironment.Resolve(variables, Workspace, Workspace, Home).StateRoot.ShouldBe(UserDataDirectory.PathFor(dataHome));

        SeedLegacy(dataHome);
        var legacyFile = Path.Combine(dataHome, LegacyBrandNames.UserDataDirectory, "state.json");
        var warnings = new List<string>();
        var second = AgentLoopEnvironment.EnsureStateRootMigrated(variables, Home, warnings.Add);

        second.ShouldNotBeNull().Migrated.ShouldBeFalse();
        second.Warning.ShouldNotBeNull();
        warnings.ShouldBe([second.Warning]);
        File.ReadAllText(legacyFile).ShouldBe("legacy-state");
        File.ReadAllText(Path.Combine(UserDataDirectory.PathFor(dataHome), "state.json")).ShouldBe("legacy-state");
    }

    /// <summary>The head services resolve their config directory under the application data root: a legacy directory moves once and the config path is the new directory.</summary>
    [Fact]
    public void TheHeadServicesConfigDirectoryIsTheMigratedApplicationDataDirectory()
    {
        var applicationData = Path.Combine(_root, "appdata");
        SeedLegacy(applicationData);

        var config = HeadServices.ResolveDefaultConfigDirectory(applicationData, Path.Combine(_root, "state"), loggerFactory: null);

        config.ShouldBe(UserDataDirectory.PathFor(applicationData));
        Directory.Exists(Path.Combine(applicationData, LegacyBrandNames.UserDataDirectory)).ShouldBeFalse();
        File.ReadAllText(Path.Combine(config, "state.json")).ShouldBe("legacy-state");
    }

    /// <summary>A platform without an application data folder keeps the config directory under the state root.</summary>
    [Fact]
    public void WithoutAnApplicationDataFolderTheConfigDirectoryStaysUnderTheStateRoot()
    {
        var state = Path.Combine(_root, "state");

        HeadServices.ResolveDefaultConfigDirectory(string.Empty, state, loggerFactory: null).ShouldBe(Path.Combine(state, "config"));
    }

    /// <summary>A both-present warning reaches the logger of the head services, names directories only, and the legacy directory stays untouched.</summary>
    [Fact]
    public void ABothPresentWarningReachesTheHeadServicesLogger()
    {
        var applicationData = Path.Combine(_root, "appdata");
        SeedLegacy(applicationData);
        Directory.CreateDirectory(UserDataDirectory.PathFor(applicationData));
        var legacyFile = Path.Combine(applicationData, LegacyBrandNames.UserDataDirectory, "state.json");
        using var factory = new CapturingLoggerFactory();

        var config = HeadServices.ResolveDefaultConfigDirectory(applicationData, Path.Combine(_root, "state"), factory);

        config.ShouldBe(UserDataDirectory.PathFor(applicationData));
        var entry = factory.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain(UserDataDirectory.CurrentName);
        entry.Message.ShouldContain(LegacyBrandNames.UserDataDirectory);
        entry.Message.ShouldNotContain("legacy-state");
        File.ReadAllText(legacyFile).ShouldBe("legacy-state");
    }

    private static void SeedLegacy(string parent)
    {
        var legacy = Path.Combine(parent, LegacyBrandNames.UserDataDirectory);
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "state.json"), "legacy-state");
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        private readonly List<LogEntry> _entries = [];

        public IReadOnlyList<LogEntry> Entries => _entries;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(List<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
