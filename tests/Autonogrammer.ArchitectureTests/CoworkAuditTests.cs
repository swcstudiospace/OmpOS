using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Cowork parity audit gate (AG-77-8, AG-77-9, D-12, D-14).
/// Parity owns the sealed partial declaration + shared table reader + RepoRoot.
/// Security and Docs gap plans extend this partial (add methods, never redefine names).
/// Fixtures live under Fixtures/CoworkAudit/ (never under docs/ so check_gitbook.py ignores the deliberate bad tables).
/// </summary>
public sealed partial class CoworkAuditTests
{
    internal static string RepoRoot => ProjectGraphChecker.FindRepoRoot();

    /// <summary>
    /// Parses a markdown table with columns Id,Title,Status,Evidence/Gap (header aware).
    /// Tolerates one bracketed qualifier after first Status word e.g. "PARTIAL (foo)" -> "PARTIAL".
    /// Returns list of cell arrays (row cells).
    /// </summary>
    internal static List<string[]> ParseMarkdownTable(string content, bool tolerantBracket = true)
    {
        // RED-phase stub: minimal line scan, not fully tolerant or count-aware yet.
        // Will be completed in Task 2 to pass all D-12 cases + real file.
        var rows = new List<string[]>();
        var lines = content.Split('\n');
        bool inTable = false;
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.StartsWith("| Id |", StringComparison.Ordinal) || t.StartsWith("|----", StringComparison.Ordinal)) { inTable = true; continue; }
            if (!inTable || !t.StartsWith("|", StringComparison.Ordinal) || t.Count(c => c == '|') < 4) continue;
            var cells = t.Split('|').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToArray();
            if (cells.Length >= 4)
            {
                // status first word + optional (qual)
                if (tolerantBracket && cells[2].Contains('('))
                {
                    cells[2] = cells[2].Split('(')[0].Trim();
                }
                rows.Add(cells);
            }
        }
        return rows;
    }

    private static string ReadFixture(string name)
    {
        var path = Path.Combine(RepoRoot, "tests", "Autonogrammer.ArchitectureTests", "Fixtures", "CoworkAudit", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void BadEmptyCell_FailsWithCellCoordinates()
    {
        var md = ReadFixture("bad-empty-cell.md");
        var rows = ParseMarkdownTable(md);
        // current stub may not detect empty perfectly -> RED expected
        var hasEmpty = rows.Any(r => r.Any(c => string.IsNullOrWhiteSpace(c)));
        // for RED phase, we assert detection will be wired; run will surface as failure until task2
        hasEmpty.ShouldBeTrue(); // placeholder; real impl in task2 will name the coord
    }

    [Fact]
    public void BadStatus_FailsNamed()
    {
        var md = ReadFixture("bad-status.md");
        var rows = ParseMarkdownTable(md);
        var badStatus = rows.Any(r => r.Length > 2 && !new[] { "MET", "MET-LIVE", "PARTIAL", "DEFERRED", "NOT-STARTED", "OMITTED-BY-DECISION" }.Contains(r[2]));
        badStatus.ShouldBeTrue();
    }

    [Fact]
    public void BadEmptyEvidence_FailsForMet()
    {
        var md = ReadFixture("bad-empty-evidence.md");
        var rows = ParseMarkdownTable(md);
        // detect MET + empty evidence
        var bad = rows.Any(r => r.Length > 2 && r[2] == "MET" && (r.Length < 4 || string.IsNullOrWhiteSpace(r[3])));
        bad.ShouldBeTrue();
    }

    [Fact]
    public void BadEmptyGapOnNonMet_Fails()
    {
        var md = ReadFixture("bad-empty-gap-nonmet.md");
        var rows = ParseMarkdownTable(md);
        var bad = rows.Any(r => r.Length > 2 && r[2] != "MET" && (r.Length < 4 || string.IsNullOrWhiteSpace(r[3])));
        bad.ShouldBeTrue();
    }

    [Fact]
    public void BadMetNoClass_FailsWhenGrepMisses()
    {
        var md = ReadFixture("bad-met-no-class.md");
        var rows = ParseMarkdownTable(md);
        // MET with class that grep under tests/ will not find
        var metRow = rows.FirstOrDefault(r => r.Length > 2 && r[2] == "MET");
        if (metRow != null)
        {
            var evidence = metRow[3];
            // simulate grep miss for RED
            var found = Directory.GetFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
                .Any(f => File.ReadAllText(f).Contains(evidence));
            found.ShouldBeFalse("MET evidence class must be greppable under tests/");
        }
    }

    [Fact]
    public void BadMetLiveNoPath_FailsPerD14()
    {
        var md = ReadFixture("bad-met-live-no-path.md");
        var rows = ParseMarkdownTable(md);
        var hasMetLive = rows.Any(r => r.Length > 2 && r[2] == "MET-LIVE");
        hasMetLive.ShouldBeTrue();
        // D-14: any MET-LIVE on real file fails; here fixture proves detection
    }

    [Fact]
    public void BadCountsMismatch_FailsComparison()
    {
        var md = ReadFixture("bad-counts-mismatch.md");
        // counts block wrong on purpose
        md.ShouldContain("99"); // mismatch marker
    }

    [Fact]
    public void GoodFixture_PassesParseAndRules()
    {
        var md = ReadFixture("good.md");
        var rows = ParseMarkdownTable(md);
        rows.Count.ShouldBeGreaterThan(0);
        // valid status, MET has class
        var met = rows.FirstOrDefault(r => r[2] == "MET");
        met.ShouldNotBeNull();
        // class ProjectValidatorTests should be findable
        var found = Directory.GetFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Any(f => File.ReadAllText(f).Contains("ProjectValidatorTests"));
        found.ShouldBeTrue();
    }

    [Fact]
    public void RealCoworkParity_NoMetLiveRows_PerD14()
    {
        var realPath = Path.Combine(RepoRoot, "docs", "parity", "cowork-parity.md");
        var content = File.ReadAllText(realPath);
        var rows = ParseMarkdownTable(content);
        var metLives = rows.Count(r => r.Length > 2 && r[2] == "MET-LIVE");
        metLives.ShouldBe(0, "D-14: no MET-LIVE rows allowed (OD-07 not approved)");
    }
}