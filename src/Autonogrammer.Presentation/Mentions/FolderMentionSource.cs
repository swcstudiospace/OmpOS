using System.Text;

namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The folder mention: <c>@folder/</c> attaches a listing of the files under the folder (names only, ignored and secret-bearing files left out), so the
/// agent knows what is there and can open what it needs.
/// </summary>
public sealed class FolderMentionSource : IMentionSource
{
    private const int MaxListedEntries = 500;

    private readonly IMentionFileSource _files;
    private readonly IMentionIgnoreFilter? _ignore;

    /// <summary>Creates the source.</summary>
    /// <param name="files">The workspace tree.</param>
    /// <param name="ignore">The ignore filter, or <see langword="null"/> to list everything.</param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public FolderMentionSource(IMentionFileSource files, IMentionIgnoreFilter? ignore = null)
    {
        ArgumentNullException.ThrowIfNull(files);

        _files = files;
        _ignore = ignore;
    }

    /// <inheritdoc/>
    public MentionKind Kind => MentionKind.Folder;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = await _files.ListAsync(cancellationToken).ConfigureAwait(false);
        return MentionFileCandidates.Search(entries, _ignore, query, limit, directories: true);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var folder = candidate.Id.TrimEnd('/');
        if (_ignore is not null && _ignore.IsIgnored(folder, isDirectory: true))
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(MentionCopy.IgnoredByRule(candidate.Display)));
        }

        var mention = new ResolvedMention(
            MentionKind.Folder,
            folder,
            folder + "/",
            _files.ToUri(folder),
            "text/plain",
            -1,
            false,
            async (maxBytes, token) => MentionText.Cut(await ListingAsync(folder, token).ConfigureAwait(false), maxBytes, keepEnd: false));
        return Task.FromResult(mention);
    }

    private async Task<string> ListingAsync(string folder, CancellationToken cancellationToken)
    {
        var entries = await _files.ListAsync(cancellationToken).ConfigureAwait(false);
        var prefix = folder + "/";
        var builder = new StringBuilder().Append("Folder ").Append(folder).Append("/\n");
        var listed = 0;
        var cut = false;
        foreach (var entry in entries.OrderBy(static entry => entry.Path, StringComparer.Ordinal))
        {
            if (!entry.Path.StartsWith(prefix, StringComparison.Ordinal)
                || (_ignore is not null && _ignore.IsIgnored(entry.Path, entry.IsDirectory))
                || (!entry.IsDirectory && MentionSecretFiles.IsSecretBearing(entry.Path)))
            {
                continue;
            }

            if (listed == MaxListedEntries)
            {
                cut = true;
                break;
            }

            builder.Append(entry.Path).Append(entry.IsDirectory ? "/\n" : "\n");
            listed++;
        }

        if (cut)
        {
            builder.Append("[listing cut at ").Append(MaxListedEntries).Append(" entries]\n");
        }

        return builder.ToString();
    }
}
