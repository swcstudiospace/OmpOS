using System.Globalization;

namespace Autonogrammer.Presentation.Settings.Ignore;

/// <summary>Every text of the Ignore page, in one place so the view carries no literal.</summary>
public static class IgnoreCopy
{
    /// <summary>The navigation label of the Ignore page.</summary>
    public const string Title = "Ignore";

    /// <summary>The heading of the Ignore page.</summary>
    public const string Heading = "Ignore files";

    /// <summary>The sentence under the heading.</summary>
    public const string Description =
        "Files matched here are not offered as @ mentions and are not attached to a prompt through a mention. Rules use gitignore syntax.";

    /// <summary>The heading of the global list.</summary>
    public const string GlobalHeading = "Global ignore list";

    /// <summary>The help of the global list. It says where its rank is.</summary>
    public const string GlobalHelp =
        "Patterns that apply in every workspace. Ignore files in the workspace rank above this list, and the built-in list ranks below it. A pattern starting with ! shows a file again.";

    /// <summary>The label of the pattern box.</summary>
    public const string PatternLabel = "Ignore pattern";

    /// <summary>The placeholder of the pattern box.</summary>
    public const string PatternPlaceholder = "for example build/ or *.log";

    /// <summary>The label of the Add button.</summary>
    public const string AddLabel = "Add pattern";

    /// <summary>The text of a Remove button; the pattern follows in its accessible name.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The text shown when the global list is empty.</summary>
    public const string EmptyList = "No global patterns.";

    /// <summary>The label of the built-in list switch.</summary>
    public const string UseDefaultLabel = "Use the built-in ignore list";

    /// <summary>The help of the built-in list switch.</summary>
    public const string UseDefaultHelp =
        "Hides folders such as .git and node_modules, build output, binaries, images, archives, fonts and private keys.";

    /// <summary>The label of the hierarchical switch.</summary>
    public const string HierarchicalLabel = "Read ignore files in folders below the root";

    /// <summary>The help of the hierarchical switch.</summary>
    public const string HierarchicalHelp =
        "Off: only the ignore files at the workspace root apply. On: .cursorignore and .autonogrammer/ignore in a folder also apply to that folder's files.";

    /// <summary>The label of the button that shows the built-in list.</summary>
    public const string ShowDefaultsLabel = "Show the built-in list";

    /// <summary>The label of the button that hides the built-in list.</summary>
    public const string HideDefaultsLabel = "Hide the built-in list";

    /// <summary>The heading of the built-in list.</summary>
    public const string DefaultsHeading = "Built-in list";

    /// <summary>The help of the built-in list. It says the list is fixed.</summary>
    public const string DefaultsHelp = "These patterns are fixed. Add a pattern starting with ! to the global list to show one of these files again.";

    /// <summary>The heading of the found files.</summary>
    public const string FilesHeading = "Ignore files in this workspace";

    /// <summary>The text shown when the workspace has none of the ignore files.</summary>
    public const string NoFiles = "This workspace has no .cursorignore, .autonogrammer/ignore or .gitignore at its root.";

    /// <summary>The text shown above the problems of a file.</summary>
    public const string ProblemsLabel = "Skipped";

    /// <summary>The heading of the inline save error.</summary>
    public const string SaveFailedHeading = "The ignore settings were not saved";

    /// <summary>The reason a blank pattern is refused.</summary>
    public const string BlankPattern = "Type a pattern first.";

    /// <summary>The reason a pattern with a line break is refused.</summary>
    public const string PatternHasLineBreak = "A pattern is one line. Add each line on its own.";

    /// <summary>The reason a duplicate is refused.</summary>
    public const string DuplicatePattern = "That pattern is already in the list.";

    /// <summary>The reason a list that holds too many characters is refused.</summary>
    public const string ListTooLong = "The patterns are too long together. Remove some first.";

    /// <summary>Builds the accessible name of the Remove button of a pattern.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The name.</returns>
    public static string RemoveName(string pattern) => RemoveLabel + " " + pattern;

    /// <summary>Builds the line that says what was skipped in an ignore file.</summary>
    /// <param name="problem">The problem sentence.</param>
    /// <returns>The line.</returns>
    public static string ProblemLine(string problem) => ProblemsLabel + ": " + problem;

    /// <summary>Builds the reason a pattern that is too long is refused.</summary>
    /// <param name="max">The longest pattern.</param>
    /// <returns>The sentence.</returns>
    public static string PatternTooLong(int max) => string.Create(CultureInfo.InvariantCulture, $"A pattern is at most {max} characters long.");

    /// <summary>Builds the reason a full list is refused.</summary>
    /// <param name="max">The most patterns.</param>
    /// <returns>The sentence.</returns>
    public static string TooManyPatterns(int max) => string.Create(CultureInfo.InvariantCulture, $"The list holds at most {max} patterns. Remove one first.");

    /// <summary>Builds the sentence for one ignore file.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="rules">The number of rules.</param>
    /// <param name="problems">The number of problems.</param>
    /// <returns>The sentence.</returns>
    public static string FileSummary(string name, int rules, int problems) => string.Create(
        CultureInfo.InvariantCulture,
        $"{name}: {rules} {(rules == 1 ? "rule" : "rules")}{(problems == 0 ? string.Empty : $", {problems} skipped")}.");
}
