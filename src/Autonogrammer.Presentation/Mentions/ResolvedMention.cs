namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// A mention the user accepted. It holds what is needed to show a chip and to read the content when the message is sent; the content is read then,
/// not when the chip appears, so a diff or a terminal is as fresh as the send.
/// </summary>
public sealed class ResolvedMention
{
    private readonly Func<int, CancellationToken, Task<MentionContent>> _read;

    /// <summary>Creates a resolved mention.</summary>
    /// <param name="kind">What it attaches.</param>
    /// <param name="id">The stable key inside its kind.</param>
    /// <param name="display">The name shown on the chip and written into the message.</param>
    /// <param name="location">The identifier of the embedded resource: a <c>file:</c> URI for files and folders, an <c>autonogrammer:</c> URI otherwise.</param>
    /// <param name="mimeType">The MIME type of the text.</param>
    /// <param name="sizeBytes">The size of the content in bytes when the chip was made, or -1 when it is not known.</param>
    /// <param name="truncated">Whether the content is known to be longer than the cap the source will apply.</param>
    /// <param name="read">Reads the content, cut to the given number of bytes.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public ResolvedMention(
        MentionKind kind,
        string id,
        string display,
        Uri location,
        string mimeType,
        long sizeBytes,
        bool truncated,
        Func<int, CancellationToken, Task<MentionContent>> read)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(mimeType);
        ArgumentNullException.ThrowIfNull(read);

        Kind = kind;
        Id = id;
        Display = display;
        Location = location;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        Truncated = truncated;
        _read = read;
    }

    /// <summary>Gets what the mention attaches.</summary>
    public MentionKind Kind { get; }

    /// <summary>Gets the stable key inside <see cref="Kind"/>.</summary>
    public string Id { get; }

    /// <summary>Gets the name shown on the chip and written into the message after the trigger.</summary>
    public string Display { get; }

    /// <summary>Gets the identifier of the embedded resource.</summary>
    public Uri Location { get; }

    /// <summary>Gets the MIME type of the text.</summary>
    public string MimeType { get; }

    /// <summary>Gets the size of the content in bytes when the chip was made, or -1 when it was not known.</summary>
    public long SizeBytes { get; }

    /// <summary>Gets whether the content is known to be longer than the cap the source applies.</summary>
    public bool Truncated { get; }

    /// <summary>Gets the trigger that writes this mention: <c>#</c> for a pull request, <c>@</c> for the rest.</summary>
    public MentionTrigger Trigger => Kind == MentionKind.PullRequest ? MentionTrigger.Hash : MentionTrigger.At;

    /// <summary>Gets the text of the mention as it appears in the message, for example <c>@src/App.cs</c>.</summary>
    public string Token => MentionParser.Format(Trigger, Display);

    /// <summary>Reads the content now.</summary>
    /// <param name="maxBytes">The byte cap of the text.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content.</returns>
    /// <exception cref="MentionUnavailableException">The source can no longer provide the content.</exception>
    public Task<MentionContent> ReadAsync(int maxBytes, CancellationToken cancellationToken) => _read(maxBytes, cancellationToken);
}
