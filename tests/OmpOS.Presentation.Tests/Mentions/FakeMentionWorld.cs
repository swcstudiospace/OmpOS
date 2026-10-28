using System.Text;
using OmpOS.Presentation.Mentions;

namespace OmpOS.Presentation.Tests.Mentions;

/// <summary>One in-memory workspace that stands in for every seam the mention sources read: files, terminals, chats, git, rules and skills, and the browser.</summary>
internal sealed class FakeMentionWorld :
    IMentionFileSource,
    IMentionTerminalProvider,
    IMentionChatProvider,
    IGitDiffSource,
    IMentionConventionProvider,
    IMentionBrowserProvider
{
    /// <summary>Gets the workspace tree.</summary>
    internal List<MentionFileEntry> Entries { get; } = [];

    /// <summary>Gets the file texts by path.</summary>
    internal Dictionary<string, string> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the terminals.</summary>
    internal List<MentionTerminalInfo> Terminals { get; } = [];

    /// <summary>Gets the terminal output by id.</summary>
    internal Dictionary<string, string> TerminalOutput { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the chats and side chats.</summary>
    internal List<MentionChatInfo> Chats { get; } = [];

    /// <summary>Gets the transcripts by id.</summary>
    internal Dictionary<string, string> Transcripts { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the summary of the working-state diff.</summary>
    internal MentionGitDiffSummary WorkingSummary { get; set; } = new(true, 2, null, null);

    /// <summary>Gets or sets the summary of the branch diff.</summary>
    internal MentionGitDiffSummary BranchSummary { get; set; } = new(true, 3, "main", null);

    /// <summary>Gets or sets the text of the working-state diff.</summary>
    internal string WorkingDiff { get; set; } = "diff --git a/x b/x\n+one\n";

    /// <summary>Gets or sets the text of the branch diff.</summary>
    internal string BranchDiff { get; set; } = "diff --git a/y b/y\n+two\n";

    /// <summary>Gets the rules.</summary>
    internal List<MentionConventionItem> Rules { get; } = [];

    /// <summary>Gets the skills.</summary>
    internal List<MentionConventionItem> Skills { get; } = [];

    /// <summary>Gets the rule and skill bodies by <c>rule:name</c> or <c>skill:name</c>.</summary>
    internal Dictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets the open browser page, or null.</summary>
    internal MentionBrowserPage? Page { get; set; }

    /// <inheritdoc />
    public string? CurrentTitle => Page?.Title;

    /// <summary>Adds a file to the tree and to the file texts.</summary>
    /// <param name="path">The workspace-relative path.</param>
    /// <param name="text">The text.</param>
    internal void AddFile(string path, string text = "content")
    {
        Entries.Add(new MentionFileEntry(path, false, Encoding.UTF8.GetByteCount(text)));
        Files[path] = text;
    }

    /// <summary>Adds a folder to the tree.</summary>
    /// <param name="path">The workspace-relative path.</param>
    internal void AddFolder(string path) => Entries.Add(new MentionFileEntry(path, true, 0));

    /// <inheritdoc />
    Task<IReadOnlyList<MentionFileEntry>> IMentionFileSource.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionFileEntry>>([.. Entries]);

    /// <inheritdoc />
    public Task<MentionContent> ReadAsync(string relativePath, int maxBytes, CancellationToken cancellationToken) =>
        Files.TryGetValue(relativePath, out var text)
            ? Task.FromResult(new MentionContent(text, Encoding.UTF8.GetByteCount(text), false))
            : Task.FromException<MentionContent>(new MentionUnavailableException(relativePath + " is gone."));

    /// <inheritdoc />
    public Uri ToUri(string relativePath) => new("file:///work/" + relativePath);

    /// <inheritdoc />
    Task<IReadOnlyList<MentionTerminalInfo>> IMentionTerminalProvider.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionTerminalInfo>>([.. Terminals]);

    /// <inheritdoc />
    public Task<MentionContent> ReadOutputAsync(string id, int maxBytes, CancellationToken cancellationToken) =>
        TerminalOutput.TryGetValue(id, out var text)
            ? Task.FromResult(new MentionContent(text, Encoding.UTF8.GetByteCount(text), false))
            : Task.FromException<MentionContent>(new MentionUnavailableException("The terminal is gone."));

    /// <inheritdoc />
    Task<IReadOnlyList<MentionChatInfo>> IMentionChatProvider.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionChatInfo>>([.. Chats]);

    /// <inheritdoc />
    public Task<MentionContent> ReadTranscriptAsync(string id, int maxBytes, CancellationToken cancellationToken) =>
        Transcripts.TryGetValue(id, out var text)
            ? Task.FromResult(new MentionContent(text, Encoding.UTF8.GetByteCount(text), false))
            : Task.FromException<MentionContent>(new MentionUnavailableException("The chat is gone."));

    /// <inheritdoc />
    public Task<MentionGitDiffSummary> GetSummaryAsync(MentionGitDiffKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(kind == MentionGitDiffKind.Working ? WorkingSummary : BranchSummary);

    /// <inheritdoc />
    public Task<MentionContent> ReadAsync(MentionGitDiffKind kind, int maxBytes, CancellationToken cancellationToken)
    {
        var text = kind == MentionGitDiffKind.Working ? WorkingDiff : BranchDiff;
        return Task.FromResult(new MentionContent(text, Encoding.UTF8.GetByteCount(text), false));
    }

    /// <inheritdoc />
    Task<IReadOnlyList<MentionConventionItem>> IMentionConventionProvider.ListRulesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionConventionItem>>([.. Rules]);

    /// <inheritdoc />
    Task<IReadOnlyList<MentionConventionItem>> IMentionConventionProvider.ListSkillsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionConventionItem>>([.. Skills]);

    /// <inheritdoc />
    public Task<MentionContent> ReadAsync(MentionKind kind, string name, int maxBytes, CancellationToken cancellationToken)
    {
        var key = (kind == MentionKind.Rule ? "rule:" : "skill:") + name;
        return Bodies.TryGetValue(key, out var text)
            ? Task.FromResult(new MentionContent(text, Encoding.UTF8.GetByteCount(text), false))
            : Task.FromException<MentionContent>(new MentionUnavailableException(name + " is gone."));
    }

    /// <inheritdoc />
    public Task<MentionBrowserPage> CaptureAsync(int maxBytes, CancellationToken cancellationToken) =>
        Page is { } page ? Task.FromResult(page) : Task.FromException<MentionBrowserPage>(new MentionUnavailableException("No page is open."));
}
