using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Old-name literals may appear under src, services and apps only in files that scripts/rebrand/rebrand.config.json
/// freezes (content: true excluded globs, such as **/LegacyBrandNames.cs) or on spans it protects as identity-bearing
/// (protectedUrlPatterns, optionally gated by lineMustMatch). Both lists and the brand regex are read from that config;
/// nothing is hard-coded here except the pin count.
/// </summary>
public sealed class LegacyBrandLiteralGuardTests
{
    /// <summary>The LegacyBrandNames.cs pins tracked today (owner ruling, 2026-11-03).</summary>
    private const int ExpectedLegacyBrandNamesPins = 14;

    /// <summary>The loaded pattern ignores the compose family and reports a planted literal as file:line.</summary>
    [Fact]
    public void SelfTestIgnoresComposeAndCatchesAPlantedLiteral()
    {
        var probe = LegacyBrandLiteralGuard.Load(ProjectGraphChecker.FindRepoRoot());
        probe.Brand.IsMatch("compose").ShouldBeFalse();
        probe.Brand.IsMatch("component").ShouldBeFalse();
        var root = Directory.CreateTempSubdirectory("literal-guard-").FullName;
        try
        {
            var planted = Path.Combine(root, "src", "Planted.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(planted)!);
            File.WriteAllText(planted, "class X { string Name = \"" + string.Concat("om", "pos") + "\"; }\n");
            File.WriteAllText(Path.Combine(root, "src", "Compose.cs"), "class Y { string Name = \"compose component\"; }\n");
            var hits = LegacyBrandLiteralGuard.Scan(root, probe);
            hits.ShouldHaveSingleItem().ShouldContain("Planted.cs:1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>The migrated tree has no old-name literal outside frozen files and config-protected spans.</summary>
    [Fact]
    public void FullTreeScanPasses()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var hits = LegacyBrandLiteralGuard.Scan(root, LegacyBrandLiteralGuard.Load(root));
        hits.ShouldBeEmpty(string.Join(Environment.NewLine, hits));
    }

    /// <summary>The migration pins are exactly the LegacyBrandNames.cs files tracked in the index.</summary>
    [Fact]
    public void LegacyBrandNamesPinCountMatchesTheTrackedFiles()
    {
        var root = ProjectGraphChecker.FindRepoRoot();
        var pins = RunGit(root, "ls-files", "-z")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(static path => string.Equals(Path.GetFileName(path), "LegacyBrandNames.cs", StringComparison.Ordinal))
            .ToList();
        pins.Count.ShouldBe(ExpectedLegacyBrandNamesPins, string.Join(Environment.NewLine, pins));
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var git = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start git");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0, "git " + string.Join(' ', arguments) + " must succeed");
        return output;
    }

    /// <summary>Loads and scans with the probe built from the rebrand config. Literals in this file are built from fragments.</summary>
    private static class LegacyBrandLiteralGuard
    {
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

        /// <summary>A config-protected span: the pattern is blanked out of a line when lineMustMatch (if any) matches that line.</summary>
        internal sealed record ProtectedSpan(Regex Pattern, Regex? LineMustMatch);

        /// <summary>The brand regex, the protected spans and the frozen-file globs, all loaded from the config.</summary>
        internal sealed record Probe(Regex Brand, IReadOnlyList<ProtectedSpan> Protected, IReadOnlyList<Regex> FrozenFiles);

        public static Probe Load(string repoRoot)
        {
            var config = Path.Combine(repoRoot, "scripts", "rebrand", "rebrand.config.json");
            using var document = JsonDocument.Parse(File.ReadAllText(config));
            var root = document.RootElement;
            if (!root.TryGetProperty("brandRegex", out var brand) || brand.GetString() is not { Length: > 0 } brandSource)
            {
                throw new InvalidOperationException("scripts/rebrand/rebrand.config.json has no brandRegex key.");
            }

            var protectedSpans = root.GetProperty("protectedUrlPatterns").EnumerateArray()
                .Select(static entry => new ProtectedSpan(
                    new Regex(entry.GetProperty("pattern").GetString()!, RegexOptions.CultureInvariant, MatchTimeout),
                    entry.TryGetProperty("lineMustMatch", out var gate)
                        ? new Regex(gate.GetString()!, RegexOptions.CultureInvariant, MatchTimeout)
                        : null))
                .ToList();
            var frozenFiles = root.GetProperty("excludedGlobs").EnumerateArray()
                .Where(static entry => entry.TryGetProperty("content", out var content) && content.GetBoolean())
                .Select(static entry => GlobToRegex(entry.GetProperty("glob").GetString()!))
                .ToList();
            return new Probe(new Regex(brandSource, RegexOptions.CultureInvariant, MatchTimeout), protectedSpans, frozenFiles);
        }

        public static List<string> Scan(string root, Probe probe)
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

                    if (probe.FrozenFiles.Any(glob => glob.IsMatch(relative)))
                    {
                        continue;
                    }

                    var lines = File.ReadAllLines(file);
                    for (var index = 0; index < lines.Length; index++)
                    {
                        if (IsBrandLiteral(probe, lines[index]))
                        {
                            hits.Add(relative + ":" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":");
                        }
                    }
                }
            }

            return hits;
        }

        private static bool IsBrandLiteral(Probe probe, string line)
        {
            var remaining = line;
            foreach (var span in probe.Protected)
            {
                if (span.LineMustMatch is null || span.LineMustMatch.IsMatch(line))
                {
                    remaining = span.Pattern.Replace(remaining, " ");
                }
            }

            return probe.Brand.IsMatch(remaining);
        }

        /// <summary>
        /// Converts a root-relative config glob: '*' stays in one segment, '**/' matches any depth (including none),
        /// '**' crosses segments, '?' is one non-separator character, and [...] is a character class.
        /// </summary>
        private static Regex GlobToRegex(string glob)
        {
            var pattern = new StringBuilder("^");
            for (var index = 0; index < glob.Length; index++)
            {
                var character = glob[index];
                if (character == '*' && index + 1 < glob.Length && glob[index + 1] == '*')
                {
                    index++;
                    if (index + 1 < glob.Length && glob[index + 1] == '/')
                    {
                        index++;
                        pattern.Append("(?:.*/)?");
                    }
                    else
                    {
                        pattern.Append(".*");
                    }
                }
                else if (character == '*')
                {
                    pattern.Append("[^/]*");
                }
                else if (character == '?')
                {
                    pattern.Append("[^/]");
                }
                else if (character == '[' && glob.IndexOf(']', index + 1) > 0)
                {
                    var close = glob.IndexOf(']', index + 1);
                    pattern.Append(glob, index, close - index + 1);
                    index = close;
                }
                else
                {
                    pattern.Append(Regex.Escape(character.ToString()));
                }
            }

            pattern.Append('$');
            return new Regex(pattern.ToString(), RegexOptions.CultureInvariant, MatchTimeout);
        }
    }
}
