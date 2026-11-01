using System.Text;
using Autonogrammer.Cowork.Artifacts.Rendering;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Rendering;

/// <summary>RFC 4180 caps and formula-like marking.</summary>
public sealed class CsvReaderTests
{
    [Fact]
    public void QuotesNewlinesBomAndRowWindowAreCapped()
    {
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.Append("\"say \"\"hi\"\"\",\"line\ntwo\"\n");
        for (var row = 0; row < 2001; row++)
        {
            builder.Append('r').Append(row.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        }

        var result = CsvReader.Read(Encoding.UTF8.GetBytes(builder.ToString()));
        result.IsSuccess.ShouldBeTrue();
        result.Rows[0][0].Text.ShouldBe("say \"hi\"");
        result.Rows[0][1].Text.ShouldBe("line\ntwo");
        result.Rows.Count.ShouldBe(CsvReader.MaxRows);
        result.RemainderRowCount.ShouldBeGreaterThan(0);
        result.Notice.ShouldBe("Showing the first 2000 rows");
    }

    [Fact]
    public void FormulaCellsAreMarked()
    {
        var bytes = File.ReadAllBytes(Fixture("formula-cells.csv"));
        var result = CsvReader.Read(bytes);
        result.IsSuccess.ShouldBeTrue();
        result.Rows[0].Count.ShouldBe(4);
        foreach (var cell in result.Rows[0])
        {
            cell.FormulaLike.ShouldBeTrue();
        }
    }

    [Fact]
    public void OversizedCellIsRefused()
    {
        var bytes = File.ReadAllBytes(Fixture("oversized-cell.csv"));
        var result = CsvReader.Read(bytes);
        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("cell_too_long");
        result.Rows.Count.ShouldBe(0);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "csv", name);
}
