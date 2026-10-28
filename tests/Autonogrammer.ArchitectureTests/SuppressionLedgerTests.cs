using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Phase 25.9 suppression ledger gate: every <c>#pragma warning disable</c>, <c>SuppressMessage</c>
/// and <c>.editorconfig</c> severity override set to none in first-party source has exactly one kept
/// SUP row in docs/open-questions.md, and every kept row still has its suppression. The controls
/// below prove the scanner reports an unlisted suppression and ignores comments, strings and
/// generated output.
/// </summary>
public sealed class SuppressionLedgerTests
{
    private static readonly (string Id, string Path, string Rule)[] ListedRow = [("SUP-900", "src/Listed.cs", "CA1031")];

    [Fact]
    public void EveryPragmaSuppressMessageAndSeverityNoneHasARowInOpenQuestions()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var occurrences = SuppressionScanner.Scan(repoRoot);
        occurrences.ShouldNotBeEmpty("the scan found no suppression at all; the scanner no longer reads the tree");
        var rows = SuppressionScanner.ReadKeptRows(Path.Combine(repoRoot, "docs", "open-questions.md"));
        rows.ShouldNotBeEmpty("docs/open-questions.md has no kept SUP row");

        var unlisted = SuppressionScanner.FindUnlisted(occurrences, rows);

        unlisted.ShouldBeEmpty("every suppression needs one kept SUP row (file, rule, justification, owner) in docs/open-questions.md, or must be fixed at the cause:" + Environment.NewLine + string.Join(Environment.NewLine, unlisted));
    }

    [Fact]
    public void NoKeptSuppressionRowIsOrphaned()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var occurrences = SuppressionScanner.Scan(repoRoot);
        var rows = SuppressionScanner.ReadKeptRows(Path.Combine(repoRoot, "docs", "open-questions.md"));

        var orphans = SuppressionScanner.FindOrphanRows(occurrences, rows);

        orphans.ShouldBeEmpty("a kept SUP row whose suppression is gone must change its status to fixed:" + Environment.NewLine + string.Join(Environment.NewLine, orphans));
    }

    [Fact]
    public void ScanReportsAnUnlistedSuppressionInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "src/Listed.cs", "#pragma warning disable CA1031 // covered by a row", "#pragma warning restore CA1031");
            WriteFile(root, "src/Unlisted.cs", "#pragma warning disable CA9001 // no row", "#pragma warning restore CA9001");
            WriteFile(
                root,
                "src/Plain.cs",
                "[System.Diagnostics.CodeAnalysis.SuppressMessage(",
                "    \"Design\",",
                "    \"CA9002:Title\",",
                "    Justification = \"no row\")]");
            WriteFile(
                root,
                "src/Decoys.cs",
                "// #pragma warning disable CA9003",
                "/// [SuppressMessage(\"Design\", \"CA9004:Title\")]",
                "var text = \"[SuppressMessage(CA9005)]\";",
                "var pragma = \"#pragma warning disable CA9005\";",
                "/*",
                "[SuppressMessage(\"Design\", \"CA9006:Title\")]",
                "#pragma warning disable CA9006",
                "*/");
            WriteFile(
                root,
                "apps/Web/Page.razor",
                "@* [SuppressMessage(\"Design\", \"CA9007:Title\")] *@",
                "[SuppressMessage(\"Design\", \"CA9008:Title\", Justification = \"no row\")]");
            WriteFile(root, "apps/Head/Window.axaml", "<!-- #pragma warning disable CA9009 -->");
            WriteFile(
                root,
                ".editorconfig",
                "root = true",
                "[*.cs]",
                "# dotnet_diagnostic.CA9011.severity = none",
                "dotnet_diagnostic.CA9010.severity = none",
                "dotnet_diagnostic.CA9012.severity = warning");
            WriteFile(root, "obj/Generated.cs", "#pragma warning disable CA9013");
            WriteFile(root, "plugins/sibling/Plugin.cs", "#pragma warning disable CA9014");
            WriteFile(root, "docs/Doc.cs", "#pragma warning disable CA9015");

            var occurrences = SuppressionScanner.Scan(root);
            var unlisted = SuppressionScanner.FindUnlisted(occurrences, ListedRow);

            unlisted.Count.ShouldBe(4, string.Join(Environment.NewLine, unlisted));
            ContainsLine(unlisted, "src/Unlisted.cs:1: pragma CA9001").ShouldBeTrue(string.Join(Environment.NewLine, unlisted));
            ContainsLine(unlisted, "src/Plain.cs:1: SuppressMessage CA9002").ShouldBeTrue(string.Join(Environment.NewLine, unlisted));
            ContainsLine(unlisted, "apps/Web/Page.razor:2: SuppressMessage CA9008").ShouldBeTrue(string.Join(Environment.NewLine, unlisted));
            ContainsLine(unlisted, ".editorconfig:4: editorconfig CA9010").ShouldBeTrue(string.Join(Environment.NewLine, unlisted));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScanAcceptsAListedSuppressionInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "src/Listed.cs", "#pragma warning disable CA1031 // covered by a row", "#pragma warning restore CA1031");

            var occurrences = SuppressionScanner.Scan(root);

            occurrences.Count.ShouldBe(1);
            SuppressionScanner.FindUnlisted(occurrences, ListedRow).ShouldBeEmpty();
            SuppressionScanner.FindOrphanRows(occurrences, ListedRow).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScanRequiresOneRowPerSuppressionInTheSameFile()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(
                root,
                "src/Listed.cs",
                "#pragma warning disable CA1031 // first",
                "#pragma warning restore CA1031",
                "#pragma warning disable CA1031 // second",
                "#pragma warning restore CA1031");

            var unlisted = SuppressionScanner.FindUnlisted(SuppressionScanner.Scan(root), ListedRow);

            unlisted.Count.ShouldBe(1);
            ContainsLine(unlisted, "src/Listed.cs:3: pragma CA1031").ShouldBeTrue(string.Join(Environment.NewLine, unlisted));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScanReportsAKeptRowWhoseSuppressionIsGoneInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(root, "src/Listed.cs", "var clean = 1;");

            var orphans = SuppressionScanner.FindOrphanRows(SuppressionScanner.Scan(root), ListedRow);

            orphans.Count.ShouldBe(1);
            ContainsLine(orphans, "SUP-900:").ShouldBeTrue(string.Join(Environment.NewLine, orphans));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReadKeptRowsIgnoresFixedRowsAndSkipRows()
    {
        var root = CreateTempTree();
        try
        {
            WriteFile(
                root,
                "open-questions.md",
                "| ID | Question | Why it matters | Default taken | Owner | Status |",
                "| --- | --- | --- | --- | --- | --- |",
                "| SUP-001 | Is the suppression `CA1031` in `src/A.cs` still needed? | why | Kept. text | autonogrammer-maintainers | kept |",
                "| SUP-002 | Is the suppression `CA2007` in `tests/B.cs` still needed? | why | Fixed. text | autonogrammer-maintainers | fixed |",
                "| SUP-003 | Is the skip marker `Assert.Skip` in `tests/C.cs` still needed? | why | Kept. text | autonogrammer-maintainers | kept |",
                "| OQ-01 | Is the suppression `CA1031` in `src/D.cs` still needed? | why | Kept. text | autonogrammer-maintainers | kept |");

            var rows = SuppressionScanner.ReadKeptRows(Path.Combine(root, "open-questions.md"));

            rows.ShouldBe([("SUP-001", "src/A.cs", "CA1031")]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool ContainsLine(List<string> messages, string prefix)
    {
        return messages.Any(message => message.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string CreateTempTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "autonogrammer-suppression-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteFile(string root, string relative, params string[] lines)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
