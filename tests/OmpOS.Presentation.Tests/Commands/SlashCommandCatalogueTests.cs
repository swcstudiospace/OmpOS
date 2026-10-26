using System.Text.RegularExpressions;
using OmpOS.Presentation.Commands;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Commands;

/// <summary>Every shipped slash command cites the row of the Cursor research record that it was verified against (VX-02).</summary>
public sealed class SlashCommandCatalogueTests
{
    /// <summary>
    /// Each shipped command names a research row, and that row exists in the research record, is a row of the command tables, and
    /// documents the command's name as its syntax. No row is cited for a command that is not shipped.
    /// </summary>
    [Fact]
    public void EveryCatalogueEntryCitesAResearchRow()
    {
        var research = File.ReadAllLines(
            Path.Combine(FindRepositoryRoot(), SlashCommandCatalogue.Reference.Source.Replace('/', Path.DirectorySeparatorChar)));
        var shippedNames = SlashCommandCatalogue.Shipped.ListAll().Select(command => command.Name).ToArray();

        shippedNames.ShouldNotBeEmpty();
        SlashCommandCatalogue.ResearchRows.Keys.OrderBy(name => name, StringComparer.Ordinal).ShouldBe(shippedNames);
        foreach (var name in shippedNames)
        {
            var row = SlashCommandCatalogue.ResearchRows[name];
            Regex.IsMatch(row, "^[ABC][0-9]{2}$", RegexOptions.None, TimeSpan.FromSeconds(1))
                .ShouldBeTrue($"Row '{row}' cited by /{name} is not a command row identifier.");

            var tableRow = research.SingleOrDefault(line => line.StartsWith($"| {row} |", StringComparison.Ordinal));
            tableRow.ShouldNotBeNull($"Row {row} cited by /{name} is not in the research record.");
            var syntax = tableRow.Split('|')[2];
            syntax.ShouldContain($"`/{name}", Case.Sensitive, $"Row {row} does not document the syntax of /{name}.");
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OmpOS.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"The repository root (OmpOS.slnx) was not found above {AppContext.BaseDirectory}.");
    }
}
