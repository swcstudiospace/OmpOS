using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Kanban;

/// <summary>Rules on the Kanban markup that no headless render shows: no literal copy, no literal font family, compiled bindings.</summary>
public sealed class KanbanMarkupTests
{
    private const string KanbanDirectory = "src/OmpOS.Native.Views/Views/Kanban";

    private static readonly Regex LiteralCopy = new(
        "(?:\\bText|\\bContent|\\bHeader|\\bPlaceholderText|AutomationProperties\\.Name|ToolTip\\.Tip)=\"(?<value>[^{\"][^\"]*)\"",
        RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly Regex LiteralFont = new("\\bFontFamily=\"(?<value>[^{\"][^\"]*)\"", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

    private static readonly string[] ViewsWithAModel = ["KanbanBoardView", "KanbanCardView"];

    private static string Markup(string file)
    {
        var text = File.ReadAllText(Path.Combine(ShellHarness.RepoRoot(), KanbanDirectory, file));
        return Regex.Replace(text, "<!--.*?-->", string.Empty, RegexOptions.Singleline | RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);
    }

    /// <summary>No Kanban view writes a sentence or a name as a literal: every one is read from <c>KanbanCopy</c> or bound to a model.</summary>
    [Fact]
    public void NoKanbanViewHoldsLiteralCopy()
    {
        var literals = Directory.EnumerateFiles(Path.Combine(ShellHarness.RepoRoot(), KanbanDirectory), "*.axaml")
            .SelectMany(file => LiteralCopy.Matches(Markup(Path.GetFileName(file))).Select(match => $"{Path.GetFileName(file)}: {match.Groups["value"].Value}"))
            .ToList();

        literals.ShouldBeEmpty();
    }

    /// <summary>No Kanban view names a font family: families come only from the <c>OmpOS.Font.*</c> tokens.</summary>
    [Fact]
    public void NoKanbanViewHoldsALiteralFontFamily()
    {
        var literals = Directory.EnumerateFiles(Path.Combine(ShellHarness.RepoRoot(), KanbanDirectory), "*.axaml")
            .SelectMany(file => LiteralFont.Matches(Markup(Path.GetFileName(file))).Select(match => $"{Path.GetFileName(file)}: {match.Groups["value"].Value}"))
            .ToList();

        literals.ShouldBeEmpty();
    }

    /// <summary>Every Kanban view that draws a model declares its type, so its bindings compile.</summary>
    [Fact]
    public void EveryKanbanViewDeclaresItsDataType()
    {
        foreach (var view in ViewsWithAModel)
        {
            Markup(view + ".axaml").ShouldContain("x:DataType=\"pres:", Case.Sensitive, $"{view} must declare the model it draws");
        }
    }

    /// <summary>The Kanban styles use tokens for the focus ring and holds no animation outside the skeleton pulse.</summary>
    [Fact]
    public void TheKanbanStylesUseTokensAndAnimateOnlyTheSkeleton()
    {
        var styles = Markup("KanbanStyles.axaml");

        styles.ShouldContain("OmpOS.Color.AccentPurple", Case.Sensitive);
        Regex.Count(styles, "<Animation\\b", RegexOptions.NonBacktracking | RegexOptions.CultureInvariant).ShouldBe(1);
        styles.ShouldContain("skeleton-pulse", Case.Sensitive);
    }
}
