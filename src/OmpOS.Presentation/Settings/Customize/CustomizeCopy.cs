using System.Globalization;
using OmpOS.Presentation.Commands;

namespace OmpOS.Presentation.Settings.Customize;

/// <summary>The words of the read-only Customize pages. Heads show these strings and never type their own.</summary>
public static class CustomizeCopy
{
    /// <summary>The navigation label of the Rules page.</summary>
    public const string RulesTitle = "Rules";

    /// <summary>The navigation label of the Skills page.</summary>
    public const string SkillsTitle = "Skills";

    /// <summary>The navigation label of the Commands page.</summary>
    public const string CommandsTitle = "Commands";

    /// <summary>The sentence under the Rules heading.</summary>
    public const string RulesIntro = "Rules and instruction files OmpOS found in the workspace and in your home folder. To change one, edit its file.";

    /// <summary>The sentence under the Skills heading.</summary>
    public const string SkillsIntro = "Skills OmpOS found in the workspace and in your home folder. To change one, edit its SKILL.md.";

    /// <summary>The sentence under the Commands heading.</summary>
    public const string CommandsIntro = "The slash commands you can type in the composer: OmpOS and Cursor commands, and the commands and subagents found in the workspace and in your home folder.";

    /// <summary>The empty state of the Rules page.</summary>
    public const string RulesEmpty = "No rules found. Add .mdc or .md files to a rules folder inside .ompos, .omp, .cursor, .claude, .codex or .agents (in the workspace or in your home folder), or add an AGENTS.md or CLAUDE.md file.";

    /// <summary>The empty state of the Skills page.</summary>
    public const string SkillsEmpty = "No skills found. Add a folder with a SKILL.md file to a skills folder inside .ompos, .omp, .cursor, .claude, .codex or .agents (in the workspace or in your home folder).";

    /// <summary>The empty state of the Commands page.</summary>
    public const string CommandsEmpty = "No slash commands are registered.";

    /// <summary>The sentence that says the page only shows what exists.</summary>
    public const string ReadOnlyNotice = "This page is read-only.";

    /// <summary>The text of the loading state.</summary>
    public const string LoadingText = "Loading...";

    /// <summary>The heading of the error state.</summary>
    public const string ErrorTitle = "The list could not be read";

    /// <summary>The reason shown when the source gives none.</summary>
    public const string LoadFailed = "The list could not be read. Try again.";

    /// <summary>The heading above the files that could not be read.</summary>
    public const string ProblemsHeading = "Files that could not be read";

    /// <summary>The heading of the title column.</summary>
    public const string ColumnTitle = "Name";

    /// <summary>The heading of the scope column.</summary>
    public const string ColumnScope = "Scope";

    /// <summary>The heading of the origin column.</summary>
    public const string ColumnOrigin = "File or origin";

    /// <summary>The heading of the description column.</summary>
    public const string ColumnDescription = "Description";

    /// <summary>Returns the words for a scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The words.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not defined.</exception>
    public static string ScopeLabel(CustomizeScope scope) => scope switch
    {
        CustomizeScope.User => "User",
        CustomizeScope.Project => "Project",
        CustomizeScope.BuiltIn => "Built in",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown scope."),
    };

    /// <summary>Returns the live-region text for the number of entries.</summary>
    /// <param name="count">The number of entries.</param>
    /// <returns>The text.</returns>
    public static string CountText(int count) =>
        count == 1 ? "1 entry." : $"{count.ToString(CultureInfo.InvariantCulture)} entries.";

    /// <summary>Returns the origin text of a slash command.</summary>
    /// <param name="platforms">The platforms the command is available on.</param>
    /// <returns>The text.</returns>
    public static string CommandOrigin(CommandPlatforms platforms)
    {
        var names = new List<string>();
        foreach (var platform in Enum.GetValues<CommandPlatforms>())
        {
            if (platform != CommandPlatforms.None && platforms.HasFlag(platform))
            {
                names.Add(PlatformName(platform));
            }
        }

        return names.Count == 0 ? "Slash command, available on no platform" : $"Slash command, available on {string.Join(", ", names)}";
    }

    /// <summary>Returns the description of a slash command that is disabled.</summary>
    /// <param name="description">What the command does.</param>
    /// <param name="disabledReason">Why it is disabled.</param>
    /// <returns>The text.</returns>
    public static string DisabledDescription(string description, string disabledReason) => $"{description} Disabled: {disabledReason}";

    /// <summary>Returns the description of a discovered subagent: it says what the entry is, then what the subagent is for.</summary>
    /// <param name="description">What the subagent is for; may be empty.</param>
    /// <returns>The text.</returns>
    public static string SubagentDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return description.Length == 0 ? "Subagent." : "Subagent. " + description;
    }

    private static string PlatformName(CommandPlatforms platform) => platform switch
    {
        CommandPlatforms.Linux => "Linux",
        CommandPlatforms.Windows => "Windows",
        CommandPlatforms.MacOS => "macOS",
        CommandPlatforms.Ios => "iOS",
        CommandPlatforms.Android => "Android",
        CommandPlatforms.Web => "Web",
        _ => platform.ToString(),
    };
}
