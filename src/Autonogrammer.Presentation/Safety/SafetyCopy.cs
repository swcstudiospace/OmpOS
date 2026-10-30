namespace Autonogrammer.Presentation.Safety;

/// <summary>Every text of the Run mode page, in one place so the view carries no literal.</summary>
public static class SafetyCopy
{
    /// <summary>The navigation label.</summary>
    public const string Title = "Run mode";

    /// <summary>The heading.</summary>
    public const string Heading = "Run mode";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "How much an agent may do without asking you: shell commands, MCP tools, web fetches, and files outside the workspace.";

    /// <summary>The heading of the mode list.</summary>
    public const string ModeLabel = "Mode";

    /// <summary>The name of Auto-review.</summary>
    public const string AutoReviewTitle = "Auto-review";

    /// <summary>What Auto-review does in Autonogrammer.</summary>
    public const string AutoReviewSummary =
        "Runs calls that match your allow rules and read-only commands from a short known-safe list. Everything else asks you. Cursor's classifier is not part of this mode here, so no model ever decides for you.";

    /// <summary>The name of Allowlist.</summary>
    public const string AllowlistTitle = "Allowlist";

    /// <summary>What Allowlist does in Autonogrammer.</summary>
    public const string AllowlistSummary = "Runs only calls that match your allow rules. Everything else asks you.";

    /// <summary>The name of Run Everything.</summary>
    public const string RunEverythingTitle = "Run Everything";

    /// <summary>What Run Everything does in Autonogrammer.</summary>
    public const string RunEverythingSummary =
        "Runs every call without asking. omp then asks Autonogrammer nothing, so your deny rules are enforced only through omp's own command patterns. Use it only in a workspace you trust.";

    /// <summary>The line that names the mode in force.</summary>
    /// <param name="title">The mode's name.</param>
    /// <returns>The sentence.</returns>
    public static string EffectiveMode(string title) => "Mode in force: " + title + ". Changing it takes effect when you save and applies to agents you start afterwards.";

    /// <summary>The heading of the classifier note.</summary>
    public const string ClassifierHeading = "Auto-review's classifier is not available in Autonogrammer";

    /// <summary>The reason the classifier is not available.</summary>
    public const string ClassifierNote =
        "In Cursor, Auto-review sends each call it cannot settle to a small hosted model that runs on Cursor's servers. Autonogrammer has no hosted model and sends nothing anywhere itself, "
        + "so a call that classifier would have reviewed asks you instead. The sentences under autoRun in a permissions file are kept and shown below; they are not enforced.";

    /// <summary>The honest statement about containment.</summary>
    public const string NoSandboxNote =
        "Autonogrammer cannot sandbox commands: omp has no operating-system sandbox. Run mode and the read boundary decide what runs without asking. They do not limit what a command you allow can do.";

    /// <summary>The warning shown while Run Everything is selected.</summary>
    public const string RunEverythingWarning = "Run Everything is selected. Every tool call will run as soon as the agent proposes it.";

    /// <summary>The heading of the terminal allowlist.</summary>
    public const string TerminalHeading = "Terminal allowlist";

    /// <summary>The help text of the terminal allowlist.</summary>
    public const string TerminalHelp = "Commands that run without asking. git matches any git command, git status matches commands that start with those words, and npm:install* matches npm with arguments that start with install.";

    /// <summary>The label of the box for a new terminal entry.</summary>
    public const string TerminalAddLabel = "New terminal allowlist entry";

    /// <summary>The label of the button that adds a terminal entry.</summary>
    public const string TerminalAddButton = "Add terminal entry";

    /// <summary>The heading of the MCP allowlist.</summary>
    public const string McpHeading = "MCP allowlist";

    /// <summary>The help text of the MCP allowlist.</summary>
    public const string McpHelp = "MCP tools that run without asking, written server:tool. Use * for any: github:* allows every tool of the github server.";

    /// <summary>The label of the box for a new MCP entry.</summary>
    public const string McpAddLabel = "New MCP allowlist entry";

    /// <summary>The label of the button that adds an MCP entry.</summary>
    public const string McpAddButton = "Add MCP entry";

    /// <summary>The example shown in the empty terminal entry box.</summary>
    public const string TerminalPlaceholder = "git status";

    /// <summary>The example shown in the empty MCP entry box.</summary>
    public const string McpPlaceholder = "github:*";

    /// <summary>The example shown in the empty read path box.</summary>
    public const string ReadPathPlaceholder = "~/notes/*.md";

    /// <summary>The label of a remove button.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The words shown when a list has no entry.</summary>
    public const string EmptyList = "No entries.";

    /// <summary>The heading of the rules list.</summary>
    public const string RulesHeading = "Rules from permission files";

    /// <summary>The help text of the rules list.</summary>
    public const string RulesHelp = "Tokens such as Shell(git), Read(src/**), Write(docs/**), WebFetch(docs.example.com) and Mcp(github:*), from permissions.allow and permissions.deny. A deny rule always beats an allow rule, and a project file cannot remove a deny rule of your user file.";

    /// <summary>The label of the allow rules.</summary>
    public const string AllowRulesLabel = "Allow";

    /// <summary>The label of the deny rules.</summary>
    public const string DenyRulesLabel = "Deny";

    /// <summary>The heading of the instructions list.</summary>
    public const string InstructionsHeading = "Auto-review instructions";

    /// <summary>The note on the instructions.</summary>
    public const string InstructionsNote = "Kept from autoRun in your files and shown here. Without a classifier Autonogrammer cannot act on a sentence, so they change nothing.";

    /// <summary>The label of the allow instructions.</summary>
    public const string AllowInstructionsLabel = "Lean towards allowing";

    /// <summary>The label of the block instructions.</summary>
    public const string BlockInstructionsLabel = "Lean towards blocking";

    /// <summary>The heading of the read access section.</summary>
    public const string ReadHeading = "Read access";

    /// <summary>The help text of the read access section.</summary>
    public const string ReadHelp = "Where the agent may read files without asking. In Workspace, a read outside the workspace and the read allowlist asks you. Links that point outside the workspace count as outside.";

    /// <summary>The note shown instead of the read access section in Run Everything.</summary>
    public const string ReadHiddenNote = "Read access does not apply in Run Everything.";

    /// <summary>The name of the System boundary.</summary>
    public const string BoundarySystem = "System";

    /// <summary>What the System boundary does.</summary>
    public const string BoundarySystemHelp = "The agent can read files outside the workspace without asking. The default.";

    /// <summary>The name of the Workspace boundary.</summary>
    public const string BoundaryWorkspace = "Workspace";

    /// <summary>What the Workspace boundary does.</summary>
    public const string BoundaryWorkspaceHelp = "The agent reads freely inside the workspace. Anything outside needs your approval unless it is on the read allowlist.";

    /// <summary>The heading of the read allowlist.</summary>
    public const string ReadPathsHeading = "Additional read paths";

    /// <summary>The help text of the read allowlist.</summary>
    public const string ReadPathsHelp = "Folders, globs such as ~/notes/*.md, and ~ paths. A list in sandbox.json replaces the list from other files as a whole.";

    /// <summary>The label of the box for a new read path.</summary>
    public const string ReadPathAddLabel = "New additional read path";

    /// <summary>The label of the button that adds a read path.</summary>
    public const string ReadPathAddButton = "Add read path";

    /// <summary>The line that says which file controls the read boundary.</summary>
    /// <param name="source">The file's path, or null when the Settings value applies.</param>
    /// <returns>The sentence.</returns>
    public static string BoundarySource(string? source) =>
        string.IsNullOrEmpty(source) ? "The read boundary comes from this page. No sandbox.json sets it." : "The read boundary is controlled by " + source + ".";

    /// <summary>The line that says which file controls the read allowlist.</summary>
    /// <param name="source">The file's path, or null when the Settings value applies.</param>
    /// <returns>The sentence.</returns>
    public static string ReadPathsSource(string? source) =>
        string.IsNullOrEmpty(source) ? "The read allowlist comes from this page. No sandbox.json sets it." : "The read allowlist is controlled by " + source + ".";

    /// <summary>The line that says where entries are saved.</summary>
    /// <param name="path">The file the page saves to, or an empty string when there is none.</param>
    /// <returns>The sentence.</returns>
    public static string SavedTo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return path.Length == 0 ? "There is no Autonogrammer file to save to: open a workspace first." : "Entries you add are saved in " + path + ". Autonogrammer never changes a Cursor file.";
    }

    /// <summary>The heading of the file list.</summary>
    public const string FilesHeading = "Files";

    /// <summary>The help text of the file list.</summary>
    public const string FilesHelp = "Lowest precedence first: your Cursor file, your Autonogrammer file, the workspace's Cursor file, the workspace's Autonogrammer file. permissions.json lists are joined; sandbox.json values replace.";

    /// <summary>The word for a user file.</summary>
    public const string ScopeUser = "User";

    /// <summary>The word for a project file.</summary>
    public const string ScopeProject = "Project";

    /// <summary>The heading of the diagnostics list.</summary>
    public const string DiagnosticsHeading = "Problems";

    /// <summary>The words shown when no file has a problem.</summary>
    public const string NoProblems = "No problems found in your permission and sandbox files.";

    /// <summary>The warning shown when the workspace is not trusted.</summary>
    public const string UntrustedProject = "This workspace is not trusted: its allow entries are ignored. Its deny entries still apply.";

    /// <summary>The accessible name of the live region that announces a rejected entry.</summary>
    public const string EntryErrorName = "Entry problem";

    /// <summary>The heading of a failed save.</summary>
    public const string SaveFailedHeading = "Run mode could not be saved";

    /// <summary>The message for an empty entry.</summary>
    public const string EntryEmpty = "Write an entry first.";

    /// <summary>The message for an entry that is too long.</summary>
    public const string EntryTooLong = "An entry can have at most 512 characters.";

    /// <summary>The message for an entry that spans lines.</summary>
    public const string EntryMultiline = "An entry must be one line.";

    /// <summary>The message for an entry already in the list.</summary>
    public const string EntryDuplicate = "That entry is already in the list.";

    /// <summary>The message for an MCP entry without server:tool.</summary>
    public const string McpEntryInvalid = "Write the entry as server:tool, for example github:* .";

    /// <summary>The message for a relative read path.</summary>
    public const string ReadPathRelative = "Use an absolute path, a ~ path, or a glob such as ~/notes/*.md.";

    /// <summary>The message for a page with no file to save the entries to.</summary>
    public const string NoTarget = "There is no Autonogrammer file to save entries to: open a workspace first.";
}
