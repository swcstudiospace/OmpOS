using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Data-plane cutover guards (ADR-0031 D11, D-53). Needles are assembled at run time so this file does not
/// match its own scan. The brief's literal tree grep is retired: it matches the new prefix, the test-service
/// names, the omp managed-block markers and the <c>autonogrammer://</c> scheme, so it can never return empty.
/// The corrected check is the old-token scan below, also recorded in <c>docs/runbooks/data-plane.md</c>.
/// </summary>
public sealed class DataPlaneGuardTests
{
    // Note: M0010 ownership is phase 74 (see MigrationRegistry and 68 docs); guard ensures no old 68 head claim here.

    private const string SelfPath = "tests/Autonogrammer.ArchitectureTests/DataPlaneGuardTests.cs";
    private const string AllowListPrefix = "scripts/rebrand/";


    private static readonly string[] ScanRoots = ["src", "services", "tests", "scripts", "deploy"];

    private static readonly string[] OldTokens =
    [
        "om" + "pos:",
        "om" + "pos_test",
        "om" + "pos.sessions",
        "om" + "pos.cloud",
        "schema " + "ompos",
        "SCHEMA " + "ompos",
        "autonogrammer.sessions" + ".schema",
        "autonogrammer.cloud-agents" + ".schema",
    ];

    private static readonly string[] IsolationNeedles =
    [
        "FLU" + "SHDB",
        "FLU" + "SHALL",
    ];

    // A KEYS command is the token followed by its argument. The left edge rejects identifiers that end in it
    // (ROW_KEYS, NAVIGATION_KEYS); the right edge rejects a declaration such as a constant assigned an object literal.
    private static readonly Regex KeysCommand = new(@"(?<![A-Za-z0-9_])KE" + "YS (?!=)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    private static readonly string[] ScopedPrefixRoots =
    [
        "src/Autonogrammer.Coordination",
        "services/Autonogrammer.Api",
        "tests/Autonogrammer.Coordination.Tests",
    ];

    private static readonly string[] RedisWriteMethods =
    [
        "StringSetAsync",
        "KeyDeleteAsync",
        "ScriptEvaluateAsync",
    ];

    [Fact]
    public void PrefixAndSchemaConstantsMatchTheCutover()
    {
        var keyspace = File.ReadAllText(RepoPath("src/Autonogrammer.Coordination.Dragonfly/DragonflyKeyspace.cs"));
        keyspace.ShouldContain("public const string Prefix = \"autonogrammer:v1:\";");

        var runner = File.ReadAllText(RepoPath("src/Autonogrammer.Sessions.Postgres/Migrations/MigrationRunner.cs"));
        runner.ShouldContain("public const string DefaultSchema = \"autonogrammer\";");
    }

    [Fact]
    public void OldPhysicalNamesAppearOnlyUnderTheRebrandAllowList()
    {
        var offenders = new List<string>();
        var allowListed = new List<string>();
        foreach (var (relative, line) in SourceLines())
        {
            if (!OldTokens.Any(token => line.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            if (relative.StartsWith(AllowListPrefix, StringComparison.Ordinal))
            {
                allowListed.Add(relative);
            }
            else
            {
                offenders.Add(relative);
            }
        }

        offenders.ShouldBeEmpty("old physical names are allowed only under " + AllowListPrefix);
        allowListed.ShouldNotBeEmpty("the rename tool must still spell the old names");
        allowListed.Select(Path.GetDirectoryName)!.Distinct(StringComparer.Ordinal).ShouldBe(["scripts/rebrand"]);
        allowListed.Select(Path.GetFileName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ShouldBe(["rebrand.py", "test_rebrand.py"]);
    }

    [Fact]
    public void ScopedKeyLiteralsUseOnlyTheVersionedPrefix()
    {
        const string prefix = "autonogrammer:v1:";
        var offenders = new List<string>();
        foreach (var root in ScopedPrefixRoots)
        {
            foreach (var file in Enumerate(root))
            {
                var relative = Relative(file);
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (!HasBarePrefixLiteral(lines[index], prefix))
                    {
                        continue;
                    }

                    offenders.Add($"{relative}:{index + 1}");
                }
            }
        }

        offenders.ShouldBeEmpty("a key literal in the scoped trees must be the prefix constant or a v1 expectation");
    }

    [Fact]
    public void DragonflyWritesStayInsideTheKeyspaceHelper()
    {
        var flush = new List<string>();
        var writesOutside = new List<string>();
        foreach (var (relative, line) in SourceLines())
        {
            if (IsolationNeedles.Any(needle => line.Contains(needle, StringComparison.Ordinal)) || KeysCommand.IsMatch(line))
            {
                flush.Add(relative);
            }

            if (RedisWriteMethods.Any(method => line.Contains(method, StringComparison.Ordinal))
                && !relative.StartsWith("src/Autonogrammer.Coordination.Dragonfly/", StringComparison.Ordinal)
                && !relative.StartsWith("tests/", StringComparison.Ordinal))
            {
                writesOutside.Add(relative);
            }
        }

        flush.ShouldBeEmpty();
        writesOutside.ShouldBeEmpty("Redis writes belong in the Dragonfly project, which builds keys through DragonflyKeyspace");

        foreach (var file in Enumerate("src/Autonogrammer.Coordination.Dragonfly"))
        {
            var text = File.ReadAllText(file);
            if (RedisWriteMethods.Any(method => text.Contains(method, StringComparison.Ordinal)))
            {
                text.ShouldContain("DragonflyKeyspace", Case.Sensitive, Relative(file) + " writes a key without the keyspace helper");
            }
        }
    }

    /// <summary>
    /// Fixture sites allowed to drop only their own GUID-named throwaway schema. Exact statement matching and
    /// occurrence counting prevent another drop, appended SQL, or a production schema from using the exemption.
    /// </summary>
    private static readonly (string Path, string Statement)[] ThrowawaySchemaFixtures =
    [
        ("tests/Autonogrammer.Cowork.Artifacts.Stores.Tests/Support/PostgresArtifactFixture.cs", "EXECUTE format('DROP SCHEMA IF EXISTS %I CASCADE', current_setting('autonogrammer.drop_schema'));"),
        ("tests/Autonogrammer.Cowork.Memory.Postgres.Tests/PostgresMemoryStoreTests.cs", "await using var drop = new NpgsqlCommand(\"DROP SCHEMA IF EXISTS \" + _schema + \" CASCADE; DROP ROLE IF EXISTS \" + _role, connection);"),
        ("tests/Autonogrammer.Cowork.Memory.Tests/MemoryStoreContractTests.cs", "await using var drop = new NpgsqlCommand(\"DROP SCHEMA IF EXISTS \" + _schema + \" CASCADE\", connection);"),
        ("tests/Autonogrammer.Api.IntegrationTests/Cowork/CoworkPersistenceTests.cs", "var command = _admin.CreateCommand(\"DROP SCHEMA IF EXISTS \" + _schema + \" CASCADE; DROP ROLE IF EXISTS \" + _role);"),
        ("tests/Autonogrammer.Api.IntegrationTests/Cowork/ProjectsApiTests.cs", "var command = _admin.CreateCommand(\"DROP SCHEMA IF EXISTS \" + _schema + \" CASCADE\");"),
        ("tests/Autonogrammer.Api.IntegrationTests/DataPlane/DataPlaneStartupGateTests.cs", "await ExecuteAdminAsync($\"DROP SCHEMA IF EXISTS \\\"{_schema}\\\" CASCADE; DROP ROLE IF EXISTS \\\"{_role}\\\"\", CancellationToken.None).ConfigureAwait(false);"),
        ("tests/Autonogrammer.Sessions.Postgres.Tests/PostgresTestSchema.cs", "await ExecuteAdminAsync($\"DROP SCHEMA IF EXISTS \\\"{SchemaName}\\\" CASCADE\", CancellationToken.None).ConfigureAwait(false);"),
    ];

    [Fact]
    public void ExtensionAndSchemaDropsStayInTheirOnlyHomes()
    {
        var extension = "CREATE " + "EXTENSION";
        var dropSchema = "DROP " + "SCHEMA";
        var extensionHits = new List<string>();
        var dropHits = new List<string>();
        var seenFixtures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (relative, line) in SourceLines())
        {
            if (line.Contains(extension, StringComparison.Ordinal)
                && !string.Equals(relative, "deploy/data-plane/bootstrap.sql", StringComparison.Ordinal)
                && !line.Contains("ShouldBeFalse", StringComparison.Ordinal))
            {
                extensionHits.Add(relative);
            }

            if (!line.Contains(dropSchema, StringComparison.Ordinal))
            {
                continue;
            }

            var allowedThrowaway = IsAllowedThrowaway(relative, line, seenFixtures);
            if (!allowedThrowaway)
            {
                dropHits.Add(relative + ": " + line.Trim());
            }
        }

        extensionHits.ShouldBeEmpty("CREATE EXTENSION belongs only in deploy/data-plane/bootstrap.sql");
        dropHits.ShouldBeEmpty();
    }

    [Fact]
    public void ThrowawaySchemaExemptionRejectsAlteredOrRepeatedStatements()
    {
        var (path, statement) = ThrowawaySchemaFixtures[0];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        IsAllowedThrowaway(path, statement + " DROP SCHEMA autonogrammer;", seen).ShouldBeFalse();
        IsAllowedThrowaway("src/production.cs", statement, seen).ShouldBeFalse();
        IsAllowedThrowaway(path, statement, seen).ShouldBeTrue();
        IsAllowedThrowaway(path, statement, seen).ShouldBeFalse();
    }

    private static bool IsAllowedThrowaway(string relative, string line, HashSet<string> seenFixtures) =>
        ThrowawaySchemaFixtures.Any(fixture =>
            string.Equals(relative, fixture.Path, StringComparison.Ordinal)
            && string.Equals(line.Trim(), fixture.Statement, StringComparison.Ordinal))
        && seenFixtures.Add(relative);

    [Fact]
    public void MigrationUpStepsAreAdditive()
    {
        var files = Directory.GetFiles(RepoPath("src/Autonogrammer.Sessions.Postgres/Migrations"), "M000*.cs")
            .Select(Relative)
            .Where(path => path.Contains("/M000", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();
        files.Count.ShouldBe(9, "M0001 through M0009 must all be scanned");
        files.ShouldContain(path => path.EndsWith("M0001_SessionStore.cs", StringComparison.Ordinal));
        files.ShouldContain(path => path.EndsWith("M0009_CoworkProjectsMemory.cs", StringComparison.Ordinal));

        var offenders = new List<string>();
        foreach (var relative in files)
        {
            var text = File.ReadAllText(RepoPath(relative));
            var upStart = text.IndexOf("const string Up", StringComparison.Ordinal);
            var downStart = text.IndexOf("const string Down", StringComparison.Ordinal);
            upStart.ShouldBeGreaterThanOrEqualTo(0, relative);
            downStart.ShouldBeGreaterThan(upStart, relative + " must split UpSql from DownSql");
            var up = text[upStart..downStart];
            foreach (var line in up.Split('\n'))
            {
                if (line.Contains("DROP", StringComparison.OrdinalIgnoreCase)
                    || (line.Contains("ALTER", StringComparison.OrdinalIgnoreCase) && line.Contains("TYPE", StringComparison.OrdinalIgnoreCase)))
                {
                    offenders.Add(relative + ": " + line.Trim());
                }
            }
        }

        offenders.ShouldBeEmpty("an up step must not DROP or ALTER a type; down steps are not scanned");
    }

    [Fact]
    public void NoSolanaCapabilityAndNoSecondUnavailableProblem()
    {
        var solana = "Sol" + "ana";
        var secondProblem = "coordination_" + "unavailable";
        var solanaHits = new List<string>();
        var problemHits = new List<string>();
        foreach (var (relative, line) in SourceLines())
        {
            if (line.Contains(solana, StringComparison.OrdinalIgnoreCase))
            {
                solanaHits.Add(relative);
            }

            if (line.Contains(secondProblem, StringComparison.Ordinal))
            {
                problemHits.Add(relative);
            }
        }

        solanaHits.ShouldBeEmpty("no RPC URL, signer or program id");
        problemHits.ShouldBeEmpty("the only store-outage problem is data_plane_unavailable");
    }

    [Fact]
    public void ApiImageEntrypointForwardsArguments()
    {
        var containerfile = File.ReadAllText(RepoPath("deploy/api.Containerfile"));
        // The image's ENTRYPOINT is a JSON exec-form array, so the shell text's quotes are stored escaped as \" in the file.
        containerfile.ShouldContain("exec dotnet /app/Autonogrammer.Api.dll \\\"$@\\\"");
    }

    private static bool HasBarePrefixLiteral(string line, string prefix)
    {
        var cursor = 0;
        while ((cursor = line.IndexOf("\"autonogrammer:", cursor, StringComparison.Ordinal)) >= 0)
        {
            var literal = line[(cursor + 1)..];
            if (!literal.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }

            cursor += 1;
        }

        return false;
    }

    private static IEnumerable<(string Relative, string Line)> SourceLines()
    {
        foreach (var root in ScanRoots)
        {
            foreach (var file in Enumerate(root))
            {
                var relative = Relative(file);
                if (string.Equals(relative, SelfPath, StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var line in File.ReadLines(file))
                {
                    yield return (relative, line);
                }
            }
        }

        var example = RepoPath(".env.example");
        if (File.Exists(example))
        {
            foreach (var line in File.ReadLines(example))
            {
                yield return (".env.example", line);
            }
        }
    }

    private static IEnumerable<string> Enumerate(string relativeRoot)
    {
        var root = RepoPath(relativeRoot);
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            if (IsSkipped(file) || IsBinary(file))
            {
                continue;
            }

            yield return file;
        }
    }

    private static bool IsSkipped(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part is "bin" or "obj" or "__pycache__" or ".git");
    }

    private static bool IsBinary(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".dll" or ".pdb" or ".png" or ".jpg" or ".webp" or ".woff" or ".woff2" or ".exe" or ".so";
    }

    private static string Relative(string path) =>
        Path.GetRelativePath(ProjectGraphChecker.FindRepoRoot(), path).Replace('\\', '/');

    private static string RepoPath(string relative) =>
        Path.Combine(ProjectGraphChecker.FindRepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
}
