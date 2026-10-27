using System.Text;
using System.Text.Json;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// An append-only, bounded log of hook runs: <c>runs.jsonl</c> plus the previous generation <c>runs.jsonl.1</c>, together at
/// most the configured size (5 MiB by default), and the most recent records in memory for the UI. A record carries the SHA-256
/// of its input, never the input. A write that fails is counted and dropped: the log must never stop a hook.
/// </summary>
public sealed class HookRunLog : IDisposable
{
    /// <summary>The default total size of the two files, 5 MiB.</summary>
    public const long DefaultMaxBytes = 5L * 1024 * 1024;

    /// <summary>The most bytes of standard output or standard error kept in a record, 4 KiB.</summary>
    public const int ExcerptBytes = 4 * 1024;

    private readonly string? _path;
    private readonly long _generationBytes;
    private readonly int _memoryCapacity;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<HookRunRecord> _recent = new();
    private readonly Lock _recentGate = new();
    private long _currentBytes = -1;
    private long _writeFailures;

    /// <summary>
    /// Creates a log.
    /// </summary>
    /// <param name="path">The <c>runs.jsonl</c> path, or null to keep records in memory only.</param>
    /// <param name="maxBytes">The most bytes both files may hold together.</param>
    /// <param name="memoryCapacity">How many recent records <see cref="Recent"/> can return.</param>
    public HookRunLog(string? path = null, long maxBytes = DefaultMaxBytes, int memoryCapacity = 500)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 2L);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryCapacity);
        _path = path is null ? null : Path.GetFullPath(path);
        _generationBytes = maxBytes / 2;
        _memoryCapacity = memoryCapacity;
    }

    /// <summary>The number of records that could not be written to disk.</summary>
    public long WriteFailures => Interlocked.Read(ref _writeFailures);

    /// <summary>
    /// Appends a record.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the record is stored.</returns>
    public async Task AppendAsync(HookRunRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_recentGate)
        {
            _recent.Enqueue(record);
            while (_recent.Count > _memoryCapacity)
            {
                _recent.Dequeue();
            }
        }

        if (_path is null)
        {
            return;
        }

        var line = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ToLine(record), HooksJsonContext.Default.HookRunLogLine) + "\n");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            Interlocked.Increment(ref _writeFailures);
        }
        catch (UnauthorizedAccessException)
        {
            Interlocked.Increment(ref _writeFailures);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the newest records, oldest first.
    /// </summary>
    /// <param name="max">The most records to return.</param>
    /// <returns>The records.</returns>
    public IReadOnlyList<HookRunRecord> Recent(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);
        lock (_recentGate)
        {
            return [.. _recent.Skip(Math.Max(0, _recent.Count - max))];
        }
    }

    /// <summary>
    /// Cuts a stream's text to <see cref="ExcerptBytes"/> UTF-8 bytes without splitting a character.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The excerpt, or null when <paramref name="text"/> is null or empty.</returns>
    public static string? Excerpt(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (Encoding.UTF8.GetByteCount(text) <= ExcerptBytes)
        {
            return text;
        }

        var bytes = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > ExcerptBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            end += rune.Utf16SequenceLength;
        }

        return text[..end];
    }

    /// <summary>
    /// Releases the write gate.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    private async Task WriteAsync(byte[] line, CancellationToken cancellationToken)
    {
        var path = _path!;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (_currentBytes < 0)
        {
            _currentBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        if (_currentBytes > 0 && _currentBytes + line.Length > _generationBytes)
        {
            File.Move(path, path + ".1", overwrite: true);
            _currentBytes = 0;
        }

        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
        }

        _currentBytes += line.Length;
    }

    private static HookRunLogLine ToLine(HookRunRecord record) => new()
    {
        At = record.At,
        SessionId = record.SessionId,
        GenerationId = record.GenerationId,
        Event = record.EventName,
        HookId = record.HookId,
        PluginId = record.PluginId,
        Scope = record.Scope.ToString(),
        MatchedValue = record.MatchedValue,
        Status = record.Status.ToString(),
        Decision = record.Decision,
        ExitCode = record.ExitCode,
        DurationMs = record.Duration.TotalMilliseconds,
        UserMessage = record.UserMessage,
        StandardOutput = record.StandardOutputExcerpt,
        StandardError = record.StandardErrorExcerpt,
        OutputTruncated = record.OutputTruncated,
        PayloadSha256 = record.PayloadSha256,
        Note = record.Note,
    };
}
