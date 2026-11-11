using System.Diagnostics;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>Enforces the Cowork parity audit's evidence and counts contract.</summary>
public sealed partial class CoworkAuditTests
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly string[] ParityColumnNames = { "Row", "Feature", "Status", "Evidence" };

    internal static string RepoRoot => ProjectGraphChecker.FindRepoRoot();

    /// <summary>Reads pipe-table rows under each header with a Status column, preserving empty cells.</summary>
    internal static List<string[]> ParseMarkdownTable(string content, bool tolerantBracket = true)
    {
        var rows = new List<string[]>();
        var statusIndex = -1;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith('|') || !line.EndsWith('|'))
            {
                statusIndex = -1;
                continue;
            }

            var cells = line[1..^1].Split('|').Select(cell => cell.Trim()).ToArray();
            var headerStatus = Array.IndexOf(cells, "Status");
            if (headerStatus >= 0 && (cells[0].Equals("Row", StringComparison.Ordinal) || cells[0].Equals("Id", StringComparison.Ordinal)))
            {
                statusIndex = headerStatus;
                continue;
            }

            if (statusIndex < 0 || cells.All(cell => Regex.IsMatch(cell, @"^:?-+:?$", RegexOptions.CultureInvariant, RegexTimeout)))
            {
                continue;
            }

            if (tolerantBracket && statusIndex < cells.Length)
            {
                var match = Regex.Match(cells[statusIndex], @"^(?<status>[A-Z-]+)(?:\s+\([^()]+\))?$", RegexOptions.CultureInvariant, RegexTimeout);
                if (match.Success)
                {
                    cells[statusIndex] = match.Groups["status"].Value;
                }
            }
            rows.Add(cells);
        }
        return rows;
    }

    private static void ValidateParityTable(List<string[]> rows)
    {
        var statuses = new HashSet<string>(StringComparer.Ordinal) { "MET", "PARTIAL", "NOT-STARTED", "DEFERRED", "OMITTED-BY-DECISION" };
        var classNames = Directory.EnumerateFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal) && !path.Contains("/bin/", StringComparison.Ordinal))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\bclass\s+(?<name>\w+Tests)\b", RegexOptions.CultureInvariant, RegexTimeout).Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var id = row.Length > 0 ? row[0] : "unknown row";
            if (row.Length != 5)
            {
                throw new InvalidOperationException($"{id}: expected five cells");
            }
            for (var i = 0; i < 4; i++)
            {
                if (string.IsNullOrWhiteSpace(row[i]))
                {
                    throw new InvalidOperationException($"{id}: empty cell {i + 1} ({ParityColumnNames[i]}) status {row[2]}");
                }
            }
            if (!statuses.Contains(row[2]))
            {
                throw new InvalidOperationException($"{id}: invalid status {row[2]}");
            }
            if (!row[2].Equals("MET", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(row[4]))
            {
                throw new InvalidOperationException($"{id}: empty Gap cell for {row[2]}");
            }
            if (row[2].Equals("MET", StringComparison.Ordinal))
            {
                var named = Regex.Matches(row[3], @"\b[A-Za-z_][A-Za-z0-9_]*Tests\b", RegexOptions.CultureInvariant, RegexTimeout).Select(match => match.Value).Distinct(StringComparer.Ordinal).ToArray();
                if (named.Length == 0)
                {
                    throw new InvalidOperationException($"{id}: MET evidence names no test class");
                }
                foreach (var name in named)
                {
                    if (!classNames.Contains(name))
                    {
                        throw new InvalidOperationException($"{id}: MET test class {name} not found under tests/");
                    }
                }
            }
        }
    }

    private static string FixturePath(string name) => Path.Combine(RepoRoot, "tests", "Autonogrammer.ArchitectureTests", "Fixtures", "CoworkAudit", name);

    private static string ReadFixture(string name) => File.ReadAllText(FixturePath(name));

    private static (int ExitCode, string Error) RunCountsCheck(string path)
    {
        var start = new ProcessStartInfo("python3") { WorkingDirectory = RepoRoot, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(RepoRoot, "scripts", "docs", "parity_counts.py"));
        start.ArgumentList.Add("--check");
        start.ArgumentList.Add("--file");
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start parity counts checker");
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, error);
    }

    [Theory]
    [InlineData("bad-empty-cell.md", "C1")]
    [InlineData("bad-status.md", "FOO")]
    [InlineData("bad-empty-evidence.md", "Evidence")]
    [InlineData("bad-empty-gap-nonmet.md", "NOT-STARTED")]
    [InlineData("bad-met-no-class.md", "FooBarBazTests")]
    [InlineData("bad-met-live-no-path.md", "MET-LIVE")]
    public void InvalidFixtures_ReportTheirFailure(string fixture, string expected)
    {
        var rows = ParseMarkdownTable(File.ReadAllText(FixturePath(fixture)));
        rows.Count.ShouldBeGreaterThan(0);
        var exception = Should.Throw<InvalidOperationException>(() => ValidateParityTable(rows));
        exception.Message.ShouldContain(expected);
    }

    [Fact]
    public void GoodFixture_AcceptsQualifiedStatusAndExistingTestClass()
    {
        var rows = ParseMarkdownTable(File.ReadAllText(FixturePath("good.md")));
        rows.Count.ShouldBe(2);
        rows[1][2].ShouldBe("PARTIAL");
        ValidateParityTable(rows);
    }

    [Fact]
    public void BadCountsFixture_FailsTheActualChecker()
    {
        var result = RunCountsCheck(FixturePath("bad-counts-mismatch.md"));
        result.ExitCode.ShouldNotBe(0);
        result.Error.ShouldContain("MISMATCH");
    }

    [Fact]
    public void RealCoworkParity_HasExactlyTheRequiredRowsAndValidEvidence()
    {
        var path = Path.Combine(RepoRoot, "docs", "parity", "cowork-parity.md");
        var rows = ParseMarkdownTable(File.ReadAllText(path));
        var expected = Enumerable.Range(1, 31).Select(i => $"C{i}")
            .Concat(Enumerable.Range(1, 20).Select(i => $"K{i:00}"))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        rows.Select(row => row[0]).OrderBy(id => id, StringComparer.Ordinal).ShouldBe(expected);
        ValidateParityTable(rows);
    }

    [Fact]
    public void RealCoworkParity_CountsMatchTheActualChecker()
    {
        var result = RunCountsCheck(Path.Combine(RepoRoot, "docs", "parity", "cowork-parity.md"));
        result.ExitCode.ShouldBe(0, result.Error);
    }
}
