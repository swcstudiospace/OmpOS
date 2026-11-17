using System.Diagnostics;
using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Plugins;

/// <summary>
/// Pins what each built-in omp plugin registers (commands, tools, environment variables and schema identifiers) and the
/// package it ships. The contracts live in <c>Fixtures/OmpPluginContracts</c>, one file per plugin, recorded from the
/// commit each plugin is pinned to. The offline tests need only the superproject index; the Live tests read the plugin
/// sources at the pin and need the plugin submodules initialised.
/// </summary>
public sealed class OmpPluginContractTests
{
    private const string FixtureDirectory = "tests/Autonogrammer.Harness.AgentLoop.Tests/Fixtures/OmpPluginContracts";
    private const string SubmoduleSource = "submodule";
    private const string GitlinkMode = "160000";
    private static readonly char[] FieldSeparators = [' ', '\t'];

    [Fact]
    public void EveryBuiltInPluginHasExactlyOneSupportedContract()
    {
        var contracts = LoadContracts();

        contracts.Select(contract => contract.PluginId).Order(StringComparer.Ordinal).ShouldBe(
            OmpPluginCatalog.BuiltIn.Select(plugin => plugin.Id).Order(StringComparer.Ordinal),
            "a built-in plugin without a recorded contract cannot ship");
        contracts.ShouldAllBe(contract => contract.SchemaVersion == 1, "the contract schema is version 1");
    }

    [Fact]
    public void RecordedCommandsAndToolsMatchTheCatalogDescriptors()
    {
        foreach (var contract in LoadContracts())
        {
            var descriptor = Descriptor(contract.PluginId);
            contract.Commands.Order(StringComparer.Ordinal).ShouldBe(
                descriptor.ExpectedCommands.Order(StringComparer.Ordinal),
                $"{contract.PluginId}: the commands the catalog expects must match the commands recorded at the pin");
            contract.Tools.Order(StringComparer.Ordinal).ShouldBe(
                descriptor.ExpectedTools.Order(StringComparer.Ordinal),
                $"{contract.PluginId}: the tools the catalog expects must match the tools recorded at the pin");
        }
    }

    [Fact]
    public void RecordedSourceLocationMatchesTheCatalogDescriptor()
    {
        foreach (var contract in LoadContracts())
        {
            var descriptor = Descriptor(contract.PluginId);
            var expectedPath = descriptor.SubmoduleName is { } submodule
                ? $"plugins/{submodule}"
                : $"plugins/{descriptor.RelativeDirectory}";

            contract.SubmodulePath.ShouldBe(expectedPath, $"{contract.PluginId}: the fixture must name the directory the catalog loads from");
            (string.Equals(contract.Source, SubmoduleSource, StringComparison.Ordinal) == (descriptor.SubmoduleName is not null))
                .ShouldBeTrue($"{contract.PluginId}: a submodule plugin is recorded as a submodule and an in-repo plugin as in-repo");
        }
    }

    [Fact]
    public void SubmodulePinsMatchTheGitlinksInTheIndex()
    {
        var root = FindRepoRoot();
        foreach (var contract in LoadContracts())
        {
            if (!string.Equals(contract.Source, SubmoduleSource, StringComparison.Ordinal))
            {
                contract.PinnedCommit.ShouldBeNull($"{contract.PluginId} is in-repo and has no pinned commit");
                continue;
            }

            var pin = contract.PinnedCommit
                ?? throw new InvalidOperationException($"{contract.PluginId} is a submodule and needs a pinned commit");
            (pin.Length == 40 && pin.All(char.IsAsciiHexDigitLower)).ShouldBeTrue(
                $"{contract.PluginId}: the pin must be a full lowercase commit id");

            // The index, not HEAD: a pin staged by a bump is visible here before it is committed.
            var entry = Git(root, "ls-files", "--stage", "--", contract.SubmodulePath).Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
            entry.Length.ShouldBeGreaterThanOrEqualTo(2, $"{contract.PluginId}: {contract.SubmodulePath} must be a gitlink in the index");
            entry[0].ShouldBe(GitlinkMode, $"{contract.PluginId}: {contract.SubmodulePath} must be a gitlink (mode 160000)");
            entry[1].ShouldBe(pin, $"{contract.PluginId}: the recorded pin must equal the gitlink; re-record the contract when the pin moves");
        }
    }

    [Fact]
    public void EnvironmentVariablesAndSchemaIdsAreBackedByEvidence()
    {
        foreach (var contract in LoadContracts())
        {
            foreach (var name in contract.EnvironmentVariables)
            {
                contract.Evidence.Any(evidence => string.Equals(evidence.Literal, name, StringComparison.Ordinal)).ShouldBeTrue(
                    $"{contract.PluginId}: environment variable {name} needs an evidence literal equal to its name");
            }

            foreach (var schemaId in contract.SchemaIds)
            {
                contract.Evidence.Any(evidence => evidence.Literal.Contains(schemaId, StringComparison.Ordinal)).ShouldBeTrue(
                    $"{contract.PluginId}: schema {schemaId} needs an evidence literal that names it");
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public void EveryEvidenceLiteralAppearsInThePinnedSource()
    {
        var root = FindRepoRoot();
        foreach (var contract in LoadContracts())
        {
            foreach (var evidence in contract.Evidence)
            {
                ReadAtPin(root, contract, evidence.Path).Contains(evidence.Literal, StringComparison.Ordinal).ShouldBeTrue(
                    $"{contract.PluginId}: '{evidence.Name}' is not in {evidence.Path} at {contract.PinnedCommit ?? "the working tree"}; the plugin changed, so re-record the contract and update the catalog");
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public void ManifestsAndEntriesAtThePinMatchTheRecordedPackage()
    {
        var root = FindRepoRoot();
        foreach (var contract in LoadContracts())
        {
            using var document = JsonDocument.Parse(ReadAtPin(root, contract, contract.ManifestPath));
            var manifest = document.RootElement;

            StringOrNull(manifest, "name").ShouldBe(contract.PackageName, $"{contract.PluginId}: package name at the pin");
            StringOrNull(manifest, "version").ShouldBe(contract.PackageVersion, $"{contract.PluginId}: package version at the pin");
            StringOrNull(manifest, "license").ShouldBe(contract.Licence, $"{contract.PluginId}: licence at the pin");

            var extensions = manifest.TryGetProperty("omp", out var omp) && omp.TryGetProperty("extensions", out var list)
                ? list.EnumerateArray().Select(static entry => entry.GetString() ?? string.Empty).ToList()
                : new List<string>();
            extensions.ShouldBe(contract.OmpExtensions, $"{contract.PluginId}: omp.extensions at the pin");

            foreach (var entry in contract.EntryFiles)
            {
                ReadAtPin(root, contract, entry).ShouldNotBeNullOrWhiteSpace($"{contract.PluginId}: entry file {entry} must exist and be non-empty at the pin");
            }
        }
    }

    private static PluginDescriptorView Descriptor(string pluginId)
    {
        var descriptor = OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, pluginId, StringComparison.Ordinal));
        return new PluginDescriptorView(descriptor.SubmoduleName, descriptor.RelativeDirectory, descriptor.ExpectedCommands, descriptor.ExpectedTools);
    }

    private static string? StringOrNull(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Required(JsonElement element, string property) =>
        StringOrNull(element, property) ?? throw new InvalidOperationException($"the contract property '{property}' must be a string");

    private static List<string> Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray()
            .Select(static item => item.GetString() ?? throw new InvalidOperationException("the contract arrays hold strings only"))
            .ToList();

    private static PluginContract ParseContract(JsonElement root) =>
        new(
            SchemaVersion: root.GetProperty("schemaVersion").GetInt32(),
            PluginId: Required(root, "pluginId"),
            Source: Required(root, "source"),
            SubmodulePath: Required(root, "submodulePath"),
            PinnedCommit: StringOrNull(root, "pinnedCommit"),
            ManifestPath: Required(root, "manifestPath"),
            PackageName: Required(root, "packageName"),
            PackageVersion: StringOrNull(root, "packageVersion"),
            Licence: StringOrNull(root, "licence"),
            OmpExtensions: Strings(root, "ompExtensions"),
            EntryFiles: Strings(root, "entryFiles"),
            Commands: Strings(root, "commands"),
            Tools: Strings(root, "tools"),
            EnvironmentVariables: Strings(root, "environmentVariables"),
            SchemaIds: Strings(root, "schemaIds"),
            Evidence: root.GetProperty("evidence").EnumerateArray()
                .Select(static item => new EvidenceLiteral(Required(item, "name"), Required(item, "path"), Required(item, "literal")))
                .ToList());

    private static string ReadAtPin(string root, PluginContract contract, string path) =>
        string.Equals(contract.Source, SubmoduleSource, StringComparison.Ordinal)
            ? Git(Path.Combine(root, contract.SubmodulePath), "show", $"{contract.PinnedCommit}:{path}")
            : File.ReadAllText(Path.Combine(root, contract.SubmodulePath, path));

    private static List<PluginContract> LoadContracts()
    {
        var directory = Path.Combine(FindRepoRoot(), FixtureDirectory);
        var contracts = new List<PluginContract>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            contracts.Add(ParseContract(document.RootElement));
        }

        return contracts;
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, ".gitmodules")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root (the directory holding .gitmodules) was not found above the test binaries.");
    }

    private static string Git(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git could not be started");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {workingDirectory}: {error.Trim()}");
        }

        return output;
    }

    private sealed record PluginDescriptorView(
        string? SubmoduleName,
        string RelativeDirectory,
        IReadOnlyList<string> ExpectedCommands,
        IReadOnlyList<string> ExpectedTools);

    private sealed record PluginContract(
        int SchemaVersion,
        string PluginId,
        string Source,
        string SubmodulePath,
        string? PinnedCommit,
        string ManifestPath,
        string PackageName,
        string? PackageVersion,
        string? Licence,
        IReadOnlyList<string> OmpExtensions,
        IReadOnlyList<string> EntryFiles,
        IReadOnlyList<string> Commands,
        IReadOnlyList<string> Tools,
        IReadOnlyList<string> EnvironmentVariables,
        IReadOnlyList<string> SchemaIds,
        IReadOnlyList<EvidenceLiteral> Evidence);

    private sealed record EvidenceLiteral(string Name, string Path, string Literal);
}
