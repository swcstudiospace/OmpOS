using System.Text.Json;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The device's exactly-once record: the highest applied sequence and the newest 1024 command identifiers with their nonces, persisted
/// atomically before a command is acknowledged. A restart therefore never runs a command that was already received. A gap in the
/// sequence does not stall delivery; a sequence at or below the high water mark is a duplicate.
/// </summary>
public sealed class DispatchDedupeStore
{
    /// <summary>The number of recent command identifiers kept.</summary>
    public const int MaxRecentCommands = 1024;

    private const int FormatVersion = 1;

    private readonly string _path;
    private readonly Lock _gate = new();
    private readonly List<DispatchRecentCommand> _recent = [];
    private long _highWater;

    /// <summary>Opens the store, reading the persisted window when one exists.</summary>
    /// <param name="path">The state file.</param>
    /// <exception cref="InvalidDataException">The file exists but is not a readable window. It is never reset silently.</exception>
    public DispatchDedupeStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        var text = DispatchStateFile.ReadTextOrNull(_path);
        if (text is null)
        {
            return;
        }

        var document = Read(text);
        _highWater = document.HighWater;
        _recent.AddRange(document.Recent);
    }

    /// <summary>Gets the highest sequence applied.</summary>
    public long HighWater
    {
        get
        {
            lock (_gate)
            {
                return _highWater;
            }
        }
    }

    /// <summary>
    /// Decides whether a verified command runs. An <see cref="DispatchAdmission.Execute"/> result has already been persisted; the caller
    /// acknowledges and only then executes. If the write fails the exception propagates and nothing is acknowledged or executed.
    /// </summary>
    /// <param name="commandId">The command identifier.</param>
    /// <param name="nonce">The delivery nonce.</param>
    /// <param name="seq">The per-device sequence.</param>
    /// <returns>The admission decision.</returns>
    public DispatchAdmission Admit(string commandId, string nonce, long seq) => AdmitCore(commandId, nonce, seq, advanceHighWater: true);

    internal DispatchAdmission AdmitOutOfOrder(string commandId, string nonce, long seq) => AdmitCore(commandId, nonce, seq, advanceHighWater: false);

    private DispatchAdmission AdmitCore(string commandId, string nonce, long seq, bool advanceHighWater)
    {
        lock (_gate)
        {
            if (seq <= _highWater || _recent.Exists(entry => string.Equals(entry.CommandId, commandId, StringComparison.Ordinal)))
            {
                return DispatchAdmission.Duplicate;
            }

            if (_recent.Exists(entry => string.Equals(entry.Nonce, nonce, StringComparison.Ordinal)))
            {
                return DispatchAdmission.Replayed;
            }

            var nextHighWater = advanceHighWater ? Math.Max(_highWater, seq) : _highWater;
            var next = new List<DispatchRecentCommand>(_recent) { new(commandId, nonce) };
            if (next.Count > MaxRecentCommands)
            {
                next.RemoveRange(0, next.Count - MaxRecentCommands);
            }

            DispatchStateFile.WriteAtomic(_path, Write(new DispatchDedupeDocument(FormatVersion, nextHighWater, next)));
            _highWater = nextHighWater;
            _recent.Clear();
            _recent.AddRange(next);
            return DispatchAdmission.Execute;
        }
    }

    private static string Write(DispatchDedupeDocument document) =>
        JsonSerializer.Serialize(document, DispatchDesktopJson.Default.DispatchDedupeDocument);

    private static DispatchDedupeDocument Read(string text)
    {
        DispatchDedupeDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(text, DispatchDesktopJson.Default.DispatchDedupeDocument);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The dispatch dedupe window is not valid JSON.", ex);
        }

        if (document is null || document.Version != FormatVersion || document.HighWater < 0 || document.Recent.Count > MaxRecentCommands)
        {
            throw new InvalidDataException("The dispatch dedupe window has an unsupported format.");
        }

        return document;
    }
}
