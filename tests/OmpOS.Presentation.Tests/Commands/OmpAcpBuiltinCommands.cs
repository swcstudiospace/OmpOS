namespace OmpOS.Presentation.Tests.Commands;

/// <summary>
/// The built-in slash commands that omp 18.12.6 handles over ACP, each with its aliases. Read from omp's own registry (<c>src/slash-commands/builtin-registry.ts</c> of
/// <c>@oh-my-pi/pi-coding-agent</c> 18.12.6) by listing every spec whose <c>handle</c> is set; the entries without <c>handle</c> run in omp's terminal UI only and are not advertised over ACP.
/// A catalogue token is delegated to omp only when it is in this list.
/// </summary>
internal static class OmpAcpBuiltinCommands
{
    /// <summary>The omp version the list was read from.</summary>
    public const string OmpVersion = "18.12.6";

    /// <summary>The commands with an ACP handler: the primary name and its aliases.</summary>
    public static readonly (string Name, string[] Aliases)[] Handled =
    [
        ("security", []),
        ("model", ["models"]),
        ("switch", []),
        ("fast", []),
        ("slow", []),
        ("skillful", []),
        ("extended-context", []),
        ("computer", []),
        ("ratchet", []),
        ("prewalk", []),
        ("modelpreset", []),
        ("effort", []),
        ("advisor", []),
        ("export", []),
        ("trace", []),
        ("dump", []),
        ("share", []),
        ("browser", []),
        ("todo", []),
        ("session", []),
        ("jobs", []),
        ("usage", []),
        ("stats", []),
        ("changelog", []),
        ("tools", []),
        ("context", []),
        ("mcp", []),
        ("ssh", []),
        ("fresh", []),
        ("compact", []),
        ("shake", []),
        ("handoff", []),
        ("pin", []),
        ("retry", []),
        ("memory", []),
        ("rename", []),
        ("move", []),
        ("wt", ["worktree"]),
        ("add-dir", []),
        ("remove-dir", []),
        ("dirs", []),
        ("marketplace", []),
        ("plugins", ["plugin"]),
        ("reload-plugins", []),
        ("force", ["force:"]),
        ("forget-approvals", []),
    ];

    /// <summary>Every name a user can type that omp handles over ACP: the primary names and the aliases.</summary>
    public static readonly HashSet<string> AllNames = new(Handled.SelectMany(static command => command.Aliases.Prepend(command.Name)), StringComparer.Ordinal);
}
