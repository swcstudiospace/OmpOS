using OmpOS.Presentation.Settings;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The status table of docs/settings-parity.md: every Cursor row S01 to S50 is there once, has a status, and names a page that exists.</summary>
public sealed class SettingsParityTableTests
{
    private static readonly string[] Statuses = ["Built", "Wired", "Partial", "Omitted"];

    private static List<string[]> Rows()
    {
        var path = Path.Combine(ShellHarness.RepoRoot(), "docs", "settings-parity.md");
        var rows = new List<string[]>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| S", StringComparison.Ordinal) || line.Length < 5 || !char.IsAsciiDigit(line[3]))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|', StringSplitOptions.TrimEntries);
            cells.Length.ShouldBe(6, line);
            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>Every id S01 to S50 appears exactly once, with one of the four statuses and a note.</summary>
    [Fact]
    public void EveryRowIdIsPresentOnceWithAStatus()
    {
        var rows = Rows();

        rows.Select(static row => row[0]).ShouldBe(Enumerable.Range(1, 50).Select(static n => "S" + n.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var row in rows)
        {
            Statuses.ShouldContain(row[2], StringComparer.Ordinal, row[0]);
            row[5].ShouldNotBeNullOrWhiteSpace(row[0]);
        }
    }

    /// <summary>A Built, Wired or Partial row that names a page names a section of the window; a Built or Wired row names a page and a key; an Omitted row names neither.</summary>
    [Fact]
    public void RowsNameExistingPagesAndKeys()
    {
        var sections = Enum.GetNames<SettingsSection>().ToHashSet(StringComparer.Ordinal);
        foreach (var row in Rows())
        {
            var (id, status, page, key) = (row[0], row[2], row[3], row[4]);
            if (!string.Equals(page, "-", StringComparison.Ordinal))
            {
                sections.ShouldContain(page, StringComparer.Ordinal, id);
            }

            if (status is "Built" or "Wired")
            {
                string.Equals(page, "-", StringComparison.Ordinal).ShouldBeFalse(id);
                string.Equals(key, "-", StringComparison.Ordinal).ShouldBeFalse(id);
            }

            if (string.Equals(status, "Omitted", StringComparison.Ordinal))
            {
                page.ShouldBe("-", id);
                key.ShouldBe("-", id);
            }
        }
    }
}
