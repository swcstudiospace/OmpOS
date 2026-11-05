using System.Security.Cryptography;
using Autonogrammer.Contracts.Artifacts;
using Autonogrammer.Cowork.Artifacts.Model;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>
/// Host-owned artifact core: version-7 ids, write-once hashed versions, compare-and-swap updates and integrity checks.
/// </summary>
public sealed class ArtifactService
{
    private static readonly HashSet<string> ImageTypes = new(StringComparer.Ordinal)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    private readonly IArtifactStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ArtifactOptions _options;
    private readonly Lock _writes = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _writesBySession = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ArtifactService"/> class.</summary>
    /// <param name="store">The persistence port.</param>
    /// <param name="timeProvider">The host clock.</param>
    /// <param name="options">The bound limits.</param>
    public ArtifactService(IArtifactStore store, TimeProvider timeProvider, IOptions<ArtifactOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    /// <summary>Creates an artifact and version 1.</summary>
    /// <param name="kind">The fixed kind.</param>
    /// <param name="title">The title.</param>
    /// <param name="content">The bytes. Never truncated.</param>
    /// <param name="sessionId">An optional session link.</param>
    /// <param name="language">Required for code.</param>
    /// <param name="mediaType">Required for images; ignored for other kinds.</param>
    /// <param name="changeNote">An optional note of at most 280 characters.</param>
    /// <param name="author">The host-set author.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The first version, or a typed failure.</returns>
    public Task<ArtifactResult<ArtifactSnapshot>> CreateAsync(
        ArtifactKind kind,
        string title,
        byte[] content,
        string? sessionId,
        string? language,
        string? mediaType,
        string? changeNote,
        ArtifactAuthor author,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(content);
        return MutateAsync(
            mutation => Create(mutation, kind, title, content, sessionId, language, mediaType, changeNote, author, derivedFrom: null),
            sessionId,
            cancellationToken);
    }

    /// <summary>Appends a version when <paramref name="baseVersion"/> is current, or returns the current version when the hash matches.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="baseVersion">The version the caller edited.</param>
    /// <param name="content">The new bytes.</param>
    /// <param name="changeNote">An optional note.</param>
    /// <param name="author">The host-set author.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The stored version, or a typed failure. A stale user save is refused with no merge.</returns>
    public Task<ArtifactResult<ArtifactSnapshot>> UpdateAsync(
        string artifactId,
        int baseVersion,
        byte[] content,
        string? changeNote,
        ArtifactAuthor author,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(content);
        return MutateAsync(
            mutation => Update(mutation, artifactId, baseVersion, content, changeNote, author),
            sessionId: null,
            cancellationToken);
    }

    /// <summary>Saves a user edit of a text kind. A stale base is refused.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="baseVersion">The version the person edited.</param>
    /// <param name="content">The edited bytes.</param>
    /// <param name="changeNote">An optional note.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The new user version, or a typed refusal.</returns>
    public Task<ArtifactResult<ArtifactSnapshot>> SaveUserEditAsync(
        string artifactId,
        int baseVersion,
        byte[] content,
        string? changeNote,
        CancellationToken cancellationToken) =>
        UpdateAsync(artifactId, baseVersion, content, changeNote, ArtifactAuthor.User, cancellationToken);

    /// <summary>Reads one version after recomputing the hash and the parent link. A mismatch returns an integrity refusal and no bytes.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The verified snapshot, or a typed failure.</returns>
    public async Task<ArtifactResult<ArtifactSnapshot>> ReadAsync(string artifactId, int number, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = await _store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.NotFound);
        }

        var versions = await _store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        try
        {
            Verify(versions);
        }
        catch (ArtifactIntegrityException)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.Integrity);
        }

        var version = versions.FirstOrDefault(candidate => candidate.Number == number);
        return version is null
            ? ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.NotFound)
            : ArtifactResults.Ok(ToSnapshot(artifact, version));
    }

    /// <summary>Reads every verified version. An integrity failure returns no content.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The versions, or a typed failure.</returns>
    public async Task<ArtifactResult<IReadOnlyList<ArtifactSnapshot>>> ReadAllAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        var artifact = await _store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return ArtifactResults.Fail<IReadOnlyList<ArtifactSnapshot>>(ArtifactFailure.NotFound);
        }

        var versions = await _store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        try
        {
            Verify(versions);
        }
        catch (ArtifactIntegrityException)
        {
            return ArtifactResults.Fail<IReadOnlyList<ArtifactSnapshot>>(ArtifactFailure.Integrity);
        }

        IReadOnlyList<ArtifactSnapshot> snapshots = versions.Select(version => ToSnapshot(artifact, version)).ToArray();
        return ArtifactResults.Ok(snapshots);
    }

    /// <summary>Copies the latest verified version into a new artifact with title suffix <c> (copy)</c> and version 1.</summary>
    /// <param name="artifactId">The source artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The copy, or a typed failure.</returns>
    public async Task<ArtifactResult<ArtifactSnapshot>> CopyAsync(string artifactId, CancellationToken cancellationToken)
    {
        var latest = await ReadAsync(artifactId, await LatestNumberAsync(artifactId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        if (!latest.IsSuccess || latest.Value is null)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(latest.Failure ?? ArtifactFailure.NotFound);
        }

        var source = latest.Value;
        var title = SuffixCopy(source.Title);
        if (title is null)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.TitleInvalid);
        }

        return await MutateAsync(
            mutation => Create(
                mutation,
                source.Kind,
                title,
                source.Content.ToArray(),
                source.SessionId,
                source.Language,
                source.MediaType,
                changeNote: null,
                ArtifactAuthor.Agent,
                new ArtifactDerivation(source.ArtifactId, source.Number, source.ContentSha256)),
            source.SessionId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Hard-deletes the artifact, its versions and its grants.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task DeleteAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        return _store.MutateAsync(
            mutation =>
            {
                mutation.Delete(artifactId);
                return true;
            },
            cancellationToken).AsTask();
    }

    private async Task<int> LatestNumberAsync(string artifactId, CancellationToken cancellationToken)
    {
        var versions = await _store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        return versions.Count == 0 ? 0 : versions[^1].Number;
    }

    private async Task<ArtifactResult<ArtifactSnapshot>> MutateAsync(
        Func<IArtifactMutation, ArtifactResult<ArtifactSnapshot>> mutation,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReserveWrite(sessionId))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.QuotaExceeded);
        }

        var result = await _store.MutateAsync(mutation, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            ReleaseWrite(sessionId);
        }

        return result;
    }

    private ArtifactResult<ArtifactSnapshot> Create(
        IArtifactMutation mutation,
        ArtifactKind kind,
        string title,
        byte[] content,
        string? sessionId,
        string? language,
        string? mediaType,
        string? changeNote,
        ArtifactAuthor author,
        ArtifactDerivation? derivedFrom)
    {
        if (!IsTitleValid(title))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.TitleInvalid);
        }

        if (!TryMedia(kind, language, mediaType, out var resolvedLanguage, out var resolvedType))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.MediaInvalid);
        }

        if (!IsNoteValid(changeNote))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.TooLarge);
        }

        if (content.Length > Cap(kind))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.TooLarge);
        }

        var usage = mutation.Usage();
        if (usage.ArtifactCount >= _options.MaxArtifacts || usage.TotalBytes + content.Length > _options.MaxAccountBytes)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.QuotaExceeded);
        }

        var id = Guid.CreateVersion7().ToString("D");
        var hash = Hash(content);
        var createdAt = _timeProvider.GetUtcNow();
        var artifact = new ArtifactRecord(id, kind, title, resolvedType, resolvedLanguage, sessionId, derivedFrom);
        var version = new ArtifactVersionRecord(id, 1, content.ToArray(), hash, null, null, author, createdAt, changeNote);
        mutation.Insert(artifact, version);
        return ArtifactResults.Ok(ToSnapshot(artifact, version));
    }

    private ArtifactResult<ArtifactSnapshot> Update(
        IArtifactMutation mutation,
        string artifactId,
        int baseVersion,
        byte[] content,
        string? changeNote,
        ArtifactAuthor author)
    {
        var artifact = mutation.Find(artifactId);
        if (artifact is null)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.NotFound);
        }

        if (author == ArtifactAuthor.User && artifact.Kind == ArtifactKind.Image)
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.MediaInvalid);

        var versions = mutation.Versions(artifactId);
        if (versions.Count == 0)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.Integrity);
        }

        try
        {
            Verify(versions);
        }
        catch (ArtifactIntegrityException)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.Integrity);
        }

        var latest = versions[^1];
        var hash = Hash(content);
        if (string.Equals(hash, latest.ContentSha256, StringComparison.Ordinal))
        {
            return ArtifactResults.Ok(ToSnapshot(artifact, latest));
        }

        if (baseVersion != latest.Number)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.StaleBase(latest.Number));
        }

        if (!IsNoteValid(changeNote) || content.Length > Cap(artifact.Kind))
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.TooLarge);
        }

        if (versions.Count >= _options.MaxVersions || mutation.Usage().TotalBytes + content.Length > _options.MaxAccountBytes)
        {
            return ArtifactResults.Fail<ArtifactSnapshot>(ArtifactFailure.QuotaExceeded);
        }

        var next = new ArtifactVersionRecord(
            artifactId,
            latest.Number + 1,
            content.ToArray(),
            hash,
            latest.Number,
            latest.ContentSha256,
            author,
            _timeProvider.GetUtcNow(),
            changeNote);
        mutation.Append(next);
        return ArtifactResults.Ok(ToSnapshot(artifact, next));
    }

    private bool ReserveWrite(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return true;
        }

        var now = _timeProvider.GetUtcNow();
        lock (_writes)
        {
            if (!_writesBySession.TryGetValue(sessionId, out var window))
            {
                window = new Queue<DateTimeOffset>();
                _writesBySession.Add(sessionId, window);
            }

            while (window.Count > 0 && now - window.Peek() >= TimeSpan.FromMinutes(1))
            {
                _ = window.Dequeue();
            }

            if (window.Count >= _options.MaxWritesPerMinutePerSession)
            {
                return false;
            }

            window.Enqueue(now);
            return true;
        }
    }

    private void ReleaseWrite(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        lock (_writes)
        {
            if (_writesBySession.TryGetValue(sessionId, out var window) && window.Count > 0)
            {
                var kept = window.Where((_, index) => index < window.Count - 1).ToArray();
                window.Clear();
                foreach (var instant in kept)
                {
                    window.Enqueue(instant);
                }
            }
        }
    }

    private int Cap(ArtifactKind kind) => kind switch
    {
        ArtifactKind.Image => _options.MaxImageBytes,
        ArtifactKind.Svg => _options.MaxSvgBytes,
        ArtifactKind.Mermaid => _options.MaxMermaidBytes,
        ArtifactKind.Markdown or ArtifactKind.Code or ArtifactKind.Html or ArtifactKind.Csv => _options.MaxTextBytes,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static bool TryMedia(ArtifactKind kind, string? language, string? mediaType, out string? resolvedLanguage, out string resolvedType)
    {
        resolvedLanguage = null;
        if (kind == ArtifactKind.Image)
        {
            if (mediaType is null || !ImageTypes.Contains(mediaType))
            {
                resolvedType = string.Empty;
                return false;
            }

            resolvedType = mediaType;
            return true;
        }

        if (kind == ArtifactKind.Code)
        {
            if (language is null || language.Length is < 1 or > 32 || language.Any(static character => !IsLanguageChar(character)))
            {
                resolvedType = string.Empty;
                return false;
            }

            resolvedLanguage = language;
        }

        resolvedType = kind switch
        {
            ArtifactKind.Markdown => "text/markdown",
            ArtifactKind.Code => "text/plain",
            ArtifactKind.Html => "text/html",
            ArtifactKind.Svg => "image/svg+xml",
            ArtifactKind.Mermaid => "text/vnd.mermaid",
            ArtifactKind.Csv => "text/csv",
            _ => string.Empty,
        };
        return resolvedType.Length > 0;
    }

    private static bool IsLanguageChar(char character) =>
        character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '#' or '.' or '_' or '-';

    internal static bool IsTitleValid(string title)
    {
        if (title.Length is < 1 or > 120)
        {
            return false;
        }

        foreach (var character in title)
        {
            if (char.IsControl(character) || char.IsSurrogate(character) || IsBidi(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBidi(char character) =>
        character is (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    private static bool IsNoteValid(string? changeNote) => changeNote is null || changeNote.Length <= 280;

    private static string? SuffixCopy(string title)
    {
        const string Suffix = " (copy)";
        var room = 120 - Suffix.Length;
        var stem = title.Length <= room ? title : title[..room];
        var suffixed = string.Concat(stem, Suffix);
        return IsTitleValid(suffixed) ? suffixed : null;
    }

    private static void Verify(IReadOnlyList<ArtifactVersionRecord> versions)
    {
        if (versions.Count == 0)
        {
            throw new ArtifactIntegrityException();
        }

        string? previous = null;
        for (var index = 0; index < versions.Count; index++)
        {
            var version = versions[index];
            if (version.Number != index + 1)
            {
                throw new ArtifactIntegrityException();
            }

            if (!string.Equals(Hash(version.Content.Span), version.ContentSha256, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }

            if (version.Number == 1)
            {
                if (version.ParentNumber is not null || version.ParentContentSha256 is not null)
                {
                    throw new ArtifactIntegrityException();
                }
            }
            else if (version.ParentNumber != version.Number - 1
                || !string.Equals(version.ParentContentSha256, previous, StringComparison.Ordinal))
            {
                throw new ArtifactIntegrityException();
            }

            previous = version.ContentSha256;
        }
    }

    private static string Hash(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static ArtifactSnapshot ToSnapshot(ArtifactRecord artifact, ArtifactVersionRecord version) => new()
    {
        ArtifactId = artifact.Id,
        Kind = artifact.Kind,
        Title = artifact.Title,
        MediaType = artifact.MediaType,
        Language = artifact.Language,
        SessionId = artifact.SessionId,
        DerivedFrom = artifact.DerivedFrom,
        Number = version.Number,
        Content = version.Content.ToArray(),
        ContentSha256 = version.ContentSha256,
        ParentNumber = version.ParentNumber,
        ParentContentSha256 = version.ParentContentSha256,
        Author = version.Author,
        CreatedAt = version.CreatedAt,
        ChangeNote = version.ChangeNote,
    };
}
