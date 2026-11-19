using Autonogrammer.Presentation.Commands;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands;

/// <summary>The slash palette shows each command's description first, so it must describe what the command does (PR06-02, PR06-03).</summary>
public sealed class SlashCommandDescriptionRegressionTests
{
    /// <summary>/open opens a workspace file in the Autonogrammer editor; it never launches Cursor or opens the Git root.</summary>
    [Fact]
    public void OpenDescriptionNamesTheAutonogrammerEditorNotCursor()
    {
        var description = Describe("open");

        description.ShouldContain("Autonogrammer editor");
        description.ShouldNotContain("Cursor");
        description.ShouldNotContain("Git root");
    }

    /// <summary>/best-of-n runs n candidates of the current model, each in its own worktree, so the description must not promise one candidate per model.</summary>
    [Fact]
    public void BestOfNDescriptionDescribesNCandidatesOfTheCurrentModel()
    {
        var description = Describe("best-of-n");

        description.ShouldContain("n candidates of the current model");
        description.ShouldNotContain("per model");
    }

    private static string Describe(string name) => SlashCommandCatalogue.Shipped.ListAll().Single(command => string.Equals(command.Name, name, StringComparison.Ordinal)).Description;
}
