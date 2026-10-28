using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>
/// What <see cref="MentionSourceFactory.Create"/> needs to build the mention sources of one chat session. A property marked required has no default; every other one is optional and
/// falls back as its summary says. A remote or cloud session supplies its own <see cref="Files"/>, <see cref="Conventions"/> and <see cref="Git"/>, so the local ones are never built.
/// </summary>
internal sealed record MentionSourceContext
{
    /// <summary>Gets the identifier of the chat session; the chat list leaves this session out.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the workspace root of the session.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Gets the other workspace roots of the session, searched for rules and skills after <see cref="WorkspaceRoot"/>. Empty by default.</summary>
    public IReadOnlyList<string> AdditionalRoots { get; init; } = [];

    /// <summary>Gets the session catalog model of the Agents rail, which lists the chats and terminals.</summary>
    public required SessionCatalogModel Catalog { get; init; }

    /// <summary>Gets the delegate that reads a chat or side chat transcript as text: its arguments are the session identifier, the byte cap and the cancellation token.</summary>
    public required Func<string, int, CancellationToken, Task<MentionContent>> ReadTranscript { get; init; }

    /// <summary>Gets the delegate that reads the recent output of a terminal, or <see langword="null"/> when the head has none: every terminal mention then says that its output is not available.</summary>
    public Func<string, int, CancellationToken, Task<MentionContent>>? ReadTerminalOutput { get; init; }

    /// <summary>Gets the delegate that lists the side chats of the open session, or <see langword="null"/> when the head has none.</summary>
    public Func<IReadOnlyList<MentionChatInfo>>? SideChats { get; init; }

    /// <summary>Gets the user's global ignore list in gitignore syntax. Empty by default.</summary>
    public IReadOnlyList<string> GlobalIgnorePatterns { get; init; } = [];

    /// <summary>Gets whether the built-in ignore list (lock files, environment files, binaries, caches) applies. On by default.</summary>
    public bool UseDefaultIgnoreList { get; init; } = true;

    /// <summary>Gets whether the ignore files below the workspace root are read as well (the root's always are). Off by default.</summary>
    public bool HierarchicalIgnore { get; init; }

    /// <summary>Gets the pull request source, or <see langword="null"/> for <see cref="UnavailablePullRequestSource"/>.</summary>
    public IPullRequestSource? PullRequests { get; init; }

    /// <summary>Gets the workspace file source, or <see langword="null"/> for <see cref="WorkspaceMentionFiles"/> over <see cref="WorkspaceRoot"/>. A remote session passes its own.</summary>
    public IMentionFileSource? Files { get; init; }

    /// <summary>Gets the rules and skills provider, or <see langword="null"/> for <see cref="ConventionMentionProvider"/> over the local roots and the user's profile folder.</summary>
    public IMentionConventionProvider? Conventions { get; init; }

    /// <summary>Gets the git diff source, or <see langword="null"/> for <see cref="GitMentionDiffSource"/> over <see cref="WorkspaceRoot"/>.</summary>
    public IGitDiffSource? Git { get; init; }

    /// <summary>Gets the clock of the listing cache, or <see langword="null"/> for the system clock.</summary>
    public TimeProvider? TimeProvider { get; init; }
}
