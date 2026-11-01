using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>Old-name literals may appear only in files named LegacyBrandNames.cs under src, services and apps.</summary>
public sealed class LegacyBrandLiteralGuardTests
{
    /// <summary>The loaded pattern ignores the compose family and reports a planted literal as file:line.</summary>
    [Fact]
    public void SelfTestIgnoresComposeAndCatchesAPlantedLiteral()
    {
        var pattern = LegacyBrandLiteralGuard.LoadPattern(ProjectGraphChecker.FindRepoRoot());
        pattern.IsMatch("compose").ShouldBeFalse();
        pattern.IsMatch("component").ShouldBeFalse();
        var root = Directory.CreateTempSubdirectory("literal-guard-").FullName;
        try
        {
            var planted = Path.Combine(root, "src", "Planted.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(planted)!);
            File.WriteAllText(planted, "class X { string Name = \"" + string.Concat("om", "pos") + "\"; }\n");
            File.WriteAllText(Path.Combine(root, "src", "Compose.cs"), "class Y { string Name = \"compose component\"; }\n");
            var hits = LegacyBrandLiteralGuard.Scan(root, pattern);
            hits.ShouldHaveSingleItem().ShouldContain("Planted.cs:1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The migrated tree has no old-name literal outside LegacyBrandNames.cs.</summary>
    [Fact]
    public void FullTreeScanPasses()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var hits = LegacyBrandLiteralGuard.Scan(root, LegacyBrandLiteralGuard.LoadPattern(root));
        hits.ShouldBeEmpty(string.Join(Environment.NewLine, hits));
    }

    /// <summary>Loads the tool boundary pattern and scans product C# files. Literals in this file are built from fragments.</summary>
    private static class LegacyBrandLiteralGuard
    {
        /// <summary>
        /// GitHub URLs that still carry the old repository slug. The slug stays until the owner renames the repository, so
        /// these exact forms are allowed: scripts/rebrand/rebrand.config.json protectedUrlPatterns ("github repo slug
        /// swcstudiospace/OmpOS") and docs/runbooks/rebrand-autonogrammer.md (protected until --urls). A longer slug, or any
        /// other path under it, is not allowed.
        /// </summary>
        private static readonly Regex ProtectedRepositoryUrls = new(
            @"https://github\.com/swcstudiospace/OmpOS(?:/issues/new)?(?![A-Za-z0-9_/-])",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        public static Regex LoadPattern(string repoRoot)
        {
            var config = Path.Combine(repoRoot, "scripts", "rebrand", "rebrand.config.json");
            using var document = JsonDocument.Parse(File.ReadAllText(config));
            if (!document.RootElement.TryGetProperty("brandRegex", out var property))
            {
                throw new InvalidOperationException("scripts/rebrand/rebrand.config.json has no brandRegex key.");
            }

            var source = property.GetString() ?? throw new InvalidOperationException("brandRegex is empty.");
            try
            {
                return new Regex(source, RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
            }
            catch (RegexParseException exception)
            {
                throw new InvalidOperationException("brandRegex is not a compatible .NET regex.", exception);
            }
        }

        public static List<string> Scan(string root, Regex pattern)
        {
            var hits = new List<string>();
            foreach (var top in new[] { "src", "services", "apps" })
            {
                var directory = Path.Combine(root, top);
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(Path.GetFileName(file), "LegacyBrandNames.cs", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var lines = File.ReadAllLines(file);
                    for (var index = 0; index < lines.Length; index++)
                    {
                        if (pattern.IsMatch(ProtectedRepositoryUrls.Replace(lines[index], " ")))
                        {
                            hits.Add(relative + ":" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":");
                        }
                    }
                }
            }

            return hits;
        }
    }
}
