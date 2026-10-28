using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>Builds the mention sources of a chat session: the list the <c>MentionComposerModel</c> is created with.</summary>
internal static class MentionSourceFactory
{
    private static readonly Func<string, int, CancellationToken, Task<MentionContent>> TerminalOutputUnavailable =
        static (_, _, _) => Task.FromException<MentionContent>(new MentionUnavailableException("The output of this terminal is not available in this window."));

    /// <summary>
    /// Creates the ten sources, in this order: file, folder, terminal, chat, side chat, working diff, branch diff, rule, skill, pull request. Nothing here touches the file system or starts a
    /// process: the file tree, the ignore files, the conventions and git are read when the picker first asks. The browser source is not part of the list; a head with a browser pane adds it.
    /// </summary>
    /// <param name="context">The session's workspace, catalog and optional seams.</param>
    /// <returns>The sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static IReadOnlyList<IMentionSource> Create(MentionSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var matcher = IgnoreMatcherFactory.Create(context.GlobalIgnorePatterns, context.UseDefaultIgnoreList);
        var ignore = new IgnoreMatcherFilter(matcher);
        var files = context.Files ?? new WorkspaceMentionFiles(context.WorkspaceRoot, matcher, context.HierarchicalIgnore, context.TimeProvider);
        var conventions = context.Conventions ?? LocalConventions(context);
        var git = context.Git ?? new GitMentionDiffSource(context.WorkspaceRoot);
        var catalog = new CatalogMentionProvider(
            context.Catalog,
            context.ReadTranscript,
            context.ReadTerminalOutput ?? TerminalOutputUnavailable,
            context.SideChats,
            () => context.SessionId);

        return
        [
            new FileMentionSource(files, ignore),
            new FolderMentionSource(files, ignore),
            new TerminalMentionSource(catalog),
            new ChatMentionSource(MentionKind.Chat, catalog),
            new ChatMentionSource(MentionKind.SideChat, catalog),
            new GitDiffMentionSource(MentionKind.GitDiffWorking, git),
            new GitDiffMentionSource(MentionKind.GitDiffBranch, git),
            new ConventionMentionSource(MentionKind.Rule, conventions),
            new ConventionMentionSource(MentionKind.Skill, conventions),
            new PullRequestMentionSource(context.PullRequests ?? new UnavailablePullRequestSource()),
        ];
    }

    private static ConventionMentionProvider LocalConventions(MentionSourceContext context)
    {
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ConventionMentionProvider([context.WorkspaceRoot, .. context.AdditionalRoots], userHome.Length == 0 ? null : userHome);
    }
}
