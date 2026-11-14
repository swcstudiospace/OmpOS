using System.Text;
using Autonogrammer.Cowork.Instructions;

namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// The standing block each Cowork task takes into its first send, kept per task beside the account's other records: one atomic,
/// owner-only file per session under <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/first-prompts/</c>. The shared session metadata file is not
/// touched, so its 256 KiB cap is unaffected by block size. The snapshot is taken when the task first starts and is never recalculated;
/// a restart before the send reuses it. A block is sent once: omp's acknowledgement or replay marks it acknowledged, and an unknown
/// outcome stops sends until the user resends or abandons it. Nothing here logs or exposes the block text.
/// </summary>
public sealed class CoworkFirstPromptStore
{
    /// <summary>The directory inside the account folder that holds one record per task.</summary>
    public const string DirectoryName = "first-prompts";

    /// <summary>The MIME type the block is sent with.</summary>
    public const string ResourceMimeType = "text/plain";

    /// <summary>The largest block a record holds, in UTF-8 bytes. It is the standing-context budget, 28 KiB.</summary>
    public const int MaxBlockBytes = InstructionLimits.StandingContextBytes;

    // A record can hold the block twice, and JSON escapes non-ASCII text, so the file cap is far above the block cap.
    private const int MaxFileBytes = 1024 * 1024;
    private const int MaxSessionIdLength = 128;
    private const string ResourceUriPrefix = "autonogrammer://cowork/standing-context/";
    private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    // A record is read, changed and written as a whole. One lock covers every record, so two sends of one task never interleave.
    private static readonly Lock Gate = new();

    // The run that wrote an in-flight record. A record in flight that another run left has an unknown outcome.
    private static readonly string ProcessRunId = Guid.NewGuid().ToString("N");

    private readonly string _accountDirectory;
    private readonly string _directory;

    /// <summary>Initializes a new instance of the <see cref="CoworkFirstPromptStore"/> class.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The account folder key.</param>
    public CoworkFirstPromptStore(string stateRoot, string accountFolderKey)
    {
        _accountDirectory = CoworkAccountFolder.AccountDirectory(stateRoot, accountFolderKey);
        _directory = Path.Combine(_accountDirectory, DirectoryName);
    }

    /// <summary>Gets the directory that holds the records.</summary>
    public string DirectoryPath => _directory;

    /// <summary>Returns the stable resource URI the block of one task is sent under.</summary>
    /// <param name="sessionId">The task.</param>
    /// <returns>The URI.</returns>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    public static Uri ResourceUriFor(string sessionId)
    {
        ValidateSessionId(sessionId);
        return new Uri(ResourceUriPrefix + sessionId, UriKind.Absolute);
    }

    /// <summary>Reads the record of one task. An in-flight record an earlier run left is returned as unconfirmed, and that is written back.</summary>
    /// <param name="sessionId">The task.</param>
    /// <returns>The record, or null when the task has none.</returns>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="InvalidDataException">The record is unreadable, torn, over its cap or not a valid record of this task.</exception>
    public CoworkFirstPromptRecord? Read(string sessionId)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            return ReadUnlocked(sessionId);
        }
    }

    /// <summary>
    /// Returns the record of one task, creating it in <see cref="CoworkFirstPromptState.Pending"/> from <paramref name="standingText"/> when there is none.
    /// An existing record is returned unchanged: its snapshot is never replaced by the text passed here.
    /// </summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="standingText">The block the task takes at its start, or null or empty when there is none.</param>
    /// <returns>The record, or null when the task has none and there is no block to take.</returns>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The block is over <see cref="MaxBlockBytes"/>.</exception>
    /// <exception cref="InvalidDataException">The existing record is not valid.</exception>
    public CoworkFirstPromptRecord? Ensure(string sessionId, string? standingText)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            var existing = ReadUnlocked(sessionId);
            if (existing is not null || string.IsNullOrEmpty(standingText))
            {
                return existing;
            }

            if (Encoding.UTF8.GetByteCount(standingText) > MaxBlockBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(standingText), "The standing block is over its budget.");
            }

            var created = new CoworkFirstPromptRecord(sessionId, CoworkFirstPromptState.Pending, ResourceUriFor(sessionId), ResourceMimeType, standingText, null);
            Save(created);
            return created;
        }
    }

    /// <summary>Moves a pending block to in-flight. It is written before anything leaves for the transport.</summary>
    /// <param name="sessionId">The task.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="InvalidOperationException">The task has no block, or the block is not pending.</exception>
    public void MarkInFlight(string sessionId)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            var record = RequireUnlocked(sessionId);
            if (record.State != CoworkFirstPromptState.Pending)
            {
                throw new InvalidOperationException("The task context is not waiting to be sent, so it was not sent again.");
            }

            Save(record with { State = CoworkFirstPromptState.InFlight });
        }
    }

    /// <summary>
    /// Records how an in-flight send ended. Neither transmitted nor completed: the block is pending again, so the next send carries it. Transmitted and
    /// completed: acknowledged. Every other ending is unconfirmed, because a transmitted block may have been stored. A block a replay already changed is left as it is.
    /// </summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="transmitted">Whether the prompt request was written to the transport.</param>
    /// <param name="completed">Whether omp completed the turn that carried the request.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    public void Settle(string sessionId, bool transmitted, bool completed)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            var record = ReadUnlocked(sessionId);
            if (record is null || record.State != CoworkFirstPromptState.InFlight)
            {
                return;
            }

            var next = !transmitted && !completed
                ? CoworkFirstPromptState.Pending
                : transmitted && completed ? CoworkFirstPromptState.Acknowledged : CoworkFirstPromptState.Unconfirmed;
            Save(record with { State = next });
        }
    }

    /// <summary>The user chose to send the block again. Allowed for an in-flight or unconfirmed block.</summary>
    /// <param name="sessionId">The task.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="InvalidOperationException">The task has no block, or the block is not waiting for a recovery choice.</exception>
    public void Resend(string sessionId) => Recover(sessionId, CoworkFirstPromptState.Pending);

    /// <summary>The user chose to start without the block. Allowed for an in-flight or unconfirmed block.</summary>
    /// <param name="sessionId">The task.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="InvalidOperationException">The task has no block, or the block is not waiting for a recovery choice.</exception>
    public void Abandon(string sessionId) => Recover(sessionId, CoworkFirstPromptState.Abandoned);

    /// <summary>
    /// Records the block omp replayed for the task. A replay that carries the task's block acknowledges it, so it is not sent again.
    /// The replayed bytes are kept as they arrived when they fit the budget; the stored snapshot itself is never replaced.
    /// </summary>
    /// <param name="sessionId">The task.</param>
    /// <param name="uri">The resource URI of the replayed block. A different URI changes nothing.</param>
    /// <param name="replayedText">The replayed text, exactly as received.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    /// <exception cref="ArgumentNullException">A text is null.</exception>
    public void RecordReplay(string sessionId, Uri uri, string replayedText)
    {
        ValidateSessionId(sessionId);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(replayedText);
        lock (Gate)
        {
            var record = ReadUnlocked(sessionId);
            if (record is null || !string.Equals(record.Uri.AbsoluteUri, uri.AbsoluteUri, StringComparison.Ordinal))
            {
                return;
            }

            var state = record.State is CoworkFirstPromptState.Pending or CoworkFirstPromptState.InFlight or CoworkFirstPromptState.Unconfirmed
                ? CoworkFirstPromptState.Acknowledged
                : record.State;
            var kept = record.ReplayedText
                ?? (Encoding.UTF8.GetByteCount(replayedText) <= MaxBlockBytes ? replayedText : null);
            if (state == record.State && string.Equals(kept, record.ReplayedText, StringComparison.Ordinal))
            {
                return;
            }

            Save(record with { State = state, ReplayedText = kept });
        }
    }

    /// <summary>Removes the record of one task. Used when the task is removed; a missing record is not an error.</summary>
    /// <param name="sessionId">The task.</param>
    /// <exception cref="ArgumentException">The identifier is not a safe file name.</exception>
    public void Delete(string sessionId)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            var path = PathOf(sessionId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private void Recover(string sessionId, CoworkFirstPromptState next)
    {
        ValidateSessionId(sessionId);
        lock (Gate)
        {
            var record = RequireUnlocked(sessionId);
            if (record.State is not (CoworkFirstPromptState.InFlight or CoworkFirstPromptState.Unconfirmed))
            {
                throw new InvalidOperationException("The task context was not waiting for a recovery choice.");
            }

            Save(record with { State = next });
        }
    }

    private CoworkFirstPromptRecord RequireUnlocked(string sessionId) =>
        ReadUnlocked(sessionId) ?? throw new InvalidOperationException("The task has no standing block.");

    private CoworkFirstPromptRecord? ReadUnlocked(string sessionId)
    {
        if (!File.Exists(PathOf(sessionId)))
        {
            return null;
        }

        var document = FileFor(sessionId).Read();
        var record = ToRecord(sessionId, document);
        if (record.State != CoworkFirstPromptState.InFlight || string.Equals(document.RunId, ProcessRunId, StringComparison.Ordinal))
        {
            return record;
        }

        // The send was started by an earlier run and its outcome was never recorded. It may have been transmitted, so it is unknown.
        var unknown = record with { State = CoworkFirstPromptState.Unconfirmed };
        Save(unknown);
        return unknown;
    }

    private void Save(CoworkFirstPromptRecord record)
    {
        EnsureDirectory();
        FileFor(record.SessionId).Update(document => Fill(document, record));
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_accountDirectory);
        Directory.CreateDirectory(_directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_accountDirectory, OwnerOnlyDirectory);
            File.SetUnixFileMode(_directory, OwnerOnlyDirectory);
        }
    }

    private CoworkJsonFile<CoworkFirstPromptDocument> FileFor(string sessionId) => new(
        PathOf(sessionId),
        MaxFileBytes,
        static () => new CoworkFirstPromptDocument(),
        CoworkFirstPromptJsonContext.Default.CoworkFirstPromptDocument);

    private string PathOf(string sessionId) => Path.Combine(_directory, sessionId + ".json");

    private static void Fill(CoworkFirstPromptDocument document, CoworkFirstPromptRecord record)
    {
        document.SessionId = record.SessionId;
        document.State = StateName(record.State);
        document.Uri = record.Uri.AbsoluteUri;
        document.MimeType = record.MimeType;
        document.Text = record.Text;
        document.ReplayedText = record.ReplayedText;
        document.RunId = record.State == CoworkFirstPromptState.InFlight ? ProcessRunId : null;
    }

    private static CoworkFirstPromptRecord ToRecord(string sessionId, CoworkFirstPromptDocument document)
    {
        var state = ParseState(document.State);
        var valid = string.Equals(document.SessionId, sessionId, StringComparison.Ordinal)
            && state is not null
            && string.Equals(document.Uri, ResourceUriFor(sessionId).AbsoluteUri, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(document.MimeType)
            && document.Text is not null
            && Encoding.UTF8.GetByteCount(document.Text) <= MaxBlockBytes
            && (document.ReplayedText is null || Encoding.UTF8.GetByteCount(document.ReplayedText) <= MaxBlockBytes);
        if (!valid)
        {
            throw new InvalidDataException("The task context record is not valid for its task.");
        }

        return new CoworkFirstPromptRecord(sessionId, state!.Value, ResourceUriFor(sessionId), document.MimeType!, document.Text!, document.ReplayedText);
    }

    private static string StateName(CoworkFirstPromptState state) => state switch
    {
        CoworkFirstPromptState.Pending => "pending",
        CoworkFirstPromptState.InFlight => "inFlight",
        CoworkFirstPromptState.Unconfirmed => "unconfirmed",
        CoworkFirstPromptState.Acknowledged => "acknowledged",
        CoworkFirstPromptState.Abandoned => "abandoned",
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    private static CoworkFirstPromptState? ParseState(string? name) => name switch
    {
        "pending" => CoworkFirstPromptState.Pending,
        "inFlight" => CoworkFirstPromptState.InFlight,
        "unconfirmed" => CoworkFirstPromptState.Unconfirmed,
        "acknowledged" => CoworkFirstPromptState.Acknowledged,
        "abandoned" => CoworkFirstPromptState.Abandoned,
        _ => null,
    };

    private static void ValidateSessionId(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (sessionId.Length > MaxSessionIdLength || !IsSafeName(sessionId))
        {
            throw new ArgumentException("The session identifier is not a safe file name.", nameof(sessionId));
        }
    }

    private static bool IsSafeName(string value)
    {
        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
