using System.Globalization;

namespace Autonogrammer.Presentation.Mentions;

/// <summary>Every user-facing sentence of the mention picker and the mention chips. The views and their tests read the same constants.</summary>
public static class MentionCopy
{
    /// <summary>The accessible name of the picker list.</summary>
    public const string ListName = "Mentions";

    /// <summary>The help text of the picker list: how to move through it and what each key does.</summary>
    public const string ListHelp = "Up and Down choose an item. Enter or Tab attaches it. Escape closes the list. Type a slash after a folder to look inside it.";

    /// <summary>The accessible name of the live region that announces how many items match.</summary>
    public const string CountName = "Mention results";

    /// <summary>The accessible name of the live region that says why nothing is listed or that asks to confirm.</summary>
    public const string MessageName = "Mention message";

    /// <summary>The accessible name of the list of attached items.</summary>
    public const string ChipsName = "Attached context";

    /// <summary>The message while a search runs.</summary>
    public const string Searching = "Searching";

    /// <summary>The message when nothing matches the query.</summary>
    public const string NothingMatches = "Nothing matches";

    /// <summary>The message of a failed search, shown with the reason.</summary>
    public const string SearchFailed = "Search failed";

    /// <summary>The default sentence of a mention that cannot be attached.</summary>
    public const string CannotAttach = "This item cannot be attached.";

    /// <summary>The sentence of pull requests when no host is configured.</summary>
    public const string PullRequestsNoHost = "Pull requests need a configured Git host. Add one in Settings to mention a pull request.";

    /// <summary>The reason the browser row is disabled when no page is open.</summary>
    public const string BrowserNoPage = "Open a page in the browser first.";

    /// <summary>The reason a git row is disabled when the workspace is not a git repository.</summary>
    public const string NoRepository = "This workspace is not a git repository.";

    /// <summary>The group title of files.</summary>
    public const string GroupFiles = "Files";

    /// <summary>The group title of folders.</summary>
    public const string GroupFolders = "Folders";

    /// <summary>The group title of terminals.</summary>
    public const string GroupTerminals = "Terminals";

    /// <summary>The group title of chats.</summary>
    public const string GroupChats = "Chats";

    /// <summary>The group title of git diffs.</summary>
    public const string GroupGit = "Git";

    /// <summary>The group title of the browser.</summary>
    public const string GroupBrowser = "Browser";

    /// <summary>The group title of rules.</summary>
    public const string GroupRules = "Rules";

    /// <summary>The group title of skills.</summary>
    public const string GroupSkills = "Skills";

    /// <summary>The group title of side chats.</summary>
    public const string GroupSideChats = "Side chats";

    /// <summary>The group title of pull requests.</summary>
    public const string GroupPullRequests = "Pull requests";

    /// <summary>The label of the working-state diff row.</summary>
    public const string GitWorkingDisplay = "Commit";

    /// <summary>The label of the branch diff row.</summary>
    public const string GitBranchDisplay = "Branch";

    /// <summary>The detail of the working-state diff row.</summary>
    public const string GitWorkingDetail = "Diff of working state";

    /// <summary>The label of the browser row when no page title is known.</summary>
    public const string BrowserDisplay = "Browser";

    /// <summary>The accessible name of the button that removes a chip, without the item name.</summary>
    public const string RemovePrefix = "Remove ";

    /// <summary>Returns the title of the group of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The group title.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not defined.</exception>
    public static string GroupTitle(MentionKind kind) => kind switch
    {
        MentionKind.File => GroupFiles,
        MentionKind.Folder => GroupFolders,
        MentionKind.Terminal => GroupTerminals,
        MentionKind.Chat => GroupChats,
        MentionKind.GitDiffWorking or MentionKind.GitDiffBranch => GroupGit,
        MentionKind.Browser => GroupBrowser,
        MentionKind.Rule => GroupRules,
        MentionKind.Skill => GroupSkills,
        MentionKind.SideChat => GroupSideChats,
        MentionKind.PullRequest => GroupPullRequests,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown mention kind."),
    };

    /// <summary>Returns the singular name of a kind, spoken in a row's accessible name.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>For example <c>file</c> or <c>pull request</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not defined.</exception>
    public static string KindName(MentionKind kind) => kind switch
    {
        MentionKind.File => "file",
        MentionKind.Folder => "folder",
        MentionKind.Terminal => "terminal",
        MentionKind.Chat => "chat",
        MentionKind.GitDiffWorking => "working state diff",
        MentionKind.GitDiffBranch => "branch diff",
        MentionKind.Browser => "browser page",
        MentionKind.Rule => "rule",
        MentionKind.Skill => "skill",
        MentionKind.SideChat => "side chat",
        MentionKind.PullRequest => "pull request",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown mention kind."),
    };

    /// <summary>Builds the announcement of how many items match.</summary>
    /// <param name="count">The number of rows.</param>
    /// <returns>The sentence.</returns>
    public static string ResultCount(int count) => count switch
    {
        0 => NothingMatches,
        1 => "1 item",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} items"),
    };

    /// <summary>Builds the accessible name of a row.</summary>
    /// <param name="candidate">The row.</param>
    /// <returns>The kind, the name, the detail and the reason the row is disabled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="candidate"/> is null.</exception>
    public static string RowName(MentionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var name = KindName(candidate.Kind) + " " + candidate.Display;
        if (!string.IsNullOrEmpty(candidate.Detail))
        {
            name += ", " + candidate.Detail;
        }

        if (candidate.DisabledReason is not null)
        {
            name += ", unavailable: " + candidate.DisabledReason;
        }
        else if (candidate.RequiresConfirmation)
        {
            name += ", may contain secrets, needs confirmation";
        }

        return name;
    }

    /// <summary>Builds the question asked before a file that may hold secrets is attached.</summary>
    /// <param name="display">The file name.</param>
    /// <returns>The sentence.</returns>
    public static string ConfirmSecret(string display) =>
        display + " may contain secrets. Press Enter again to attach it, or Escape to cancel.";

    /// <summary>Builds the failure of a source that took too long.</summary>
    /// <param name="group">The group title of the source.</param>
    /// <returns>The sentence.</returns>
    public static string SourceTimedOut(string group) => group + " took too long to answer.";

    /// <summary>The message when a message holds more mentions than it may.</summary>
    public static string TooManyMentions(int limit) =>
        string.Create(CultureInfo.InvariantCulture, $"A message carries at most {limit} attached items. Remove one first.");

    /// <summary>Builds the message when attached items could no longer be read at send time.</summary>
    /// <param name="names">The names of the items.</param>
    /// <returns>The sentence.</returns>
    public static string AttachFailed(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return "These items could not be read and were removed: " + string.Join(", ", names) + ". Send again to continue without them.";
    }

    /// <summary>Builds the sentence of an item an ignore file excludes.</summary>
    /// <param name="display">The item name.</param>
    /// <returns>The sentence.</returns>
    public static string IgnoredByRule(string display) =>
        display + " is excluded by an ignore file (.cursorignore or .autonogrammer/ignore), so it cannot be attached.";

    /// <summary>Builds the accessible name of a chip.</summary>
    /// <param name="mention">The mention.</param>
    /// <returns>For example <c>file src/App.cs, 12 KB</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mention"/> is null.</exception>
    public static string ChipName(ResolvedMention mention)
    {
        ArgumentNullException.ThrowIfNull(mention);

        var name = KindName(mention.Kind) + " " + mention.Display;
        if (mention.SizeBytes >= 0)
        {
            name += ", " + FormatSize(mention.SizeBytes);
        }

        return mention.Truncated ? name + ", will be cut" : name;
    }

    /// <summary>Builds the accessible name of a chip's remove button.</summary>
    /// <param name="mention">The mention.</param>
    /// <returns>For example <c>Remove src/App.cs</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mention"/> is null.</exception>
    public static string RemoveName(ResolvedMention mention)
    {
        ArgumentNullException.ThrowIfNull(mention);
        return RemovePrefix + mention.Display;
    }

    /// <summary>Formats a byte count for a chip or a tooltip.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>For example <c>512 B</c>, <c>12.5 KB</c> or <c>1.2 MB</c>.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.#} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.#} MB"),
    };
}
