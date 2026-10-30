namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The file mention: <c>@name</c> over the workspace tree. Files that ignore files exclude are never listed; files that usually hold secrets (<c>.env</c>,
/// keys) are listed only when the user types the exact name, and then ask for confirmation. A slash after a folder name lists what is inside it.
/// </summary>
public sealed class FileMentionSource : IMentionSource
{
    private readonly IMentionFileSource _files;
    private readonly IMentionIgnoreFilter? _ignore;

    /// <summary>Creates the source.</summary>
    /// <param name="files">The workspace tree and file reads.</param>
    /// <param name="ignore">The ignore filter, or <see langword="null"/> to list everything the tree holds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public FileMentionSource(IMentionFileSource files, IMentionIgnoreFilter? ignore = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files = files;
        _ignore = ignore;
    }

    /// <inheritdoc/>
    public MentionKind Kind => MentionKind.File;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = await _files.ListAsync(cancellationToken).ConfigureAwait(false);
        return MentionFileCandidates.Search(entries, _ignore, query, limit, directories: false);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.DisabledReason is not null)
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(candidate.DisabledReason));
        }

        if (_ignore is not null && _ignore.IsIgnored(candidate.Id, isDirectory: false))
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(MentionCopy.IgnoredByRule(candidate.Display)));
        }

        var path = candidate.Id;
        var mention = new ResolvedMention(
            MentionKind.File,
            path,
            path,
            _files.ToUri(path),
            MentionMime.ForPath(path),
            candidate.SizeBytes,
            candidate.SizeBytes > MentionLimits.DefaultContentBytes,
            (maxBytes, token) => _files.ReadAsync(path, maxBytes, token));
        return Task.FromResult(mention);
    }
}
