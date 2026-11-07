using System.Globalization;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Abstractions;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.AgentLoop.Usage;

/// <summary>
/// Appends end-of-turn usage rows to <c>usage-ledger.jsonl</c>. Each append rewrites the file atomically, owner-only, and drops
/// rows older than 90 days. The file never holds prompt text, arguments or result bytes.
/// </summary>
public sealed class FileUsageLedger
{
    /// <summary>The ledger file name inside the directory the caller supplies.</summary>
    public const string FileName = "usage-ledger.jsonl";

    /// <summary>How many days a row is kept, counting the day it was written.</summary>
    public const int RetentionDays = 90;

    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly Action<ILogger, int, Exception?> AppendedMessage =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(76070, "UsageLedgerAppended"), "Usage ledger rewrote {RowCount} rows.");

    private readonly Lock _gate = new();
    private readonly string _directory;
    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly ILogger? _logger;

    /// <summary>Creates a ledger that stores <see cref="FileName"/> in <paramref name="directory"/>.</summary>
    /// <param name="directory">The directory that holds the ledger. Created owner-only when missing.</param>
    /// <param name="clock">The clock that stamps each row and decides which rows are older than <see cref="RetentionDays"/>. Null means the system clock.</param>
    /// <param name="logger">An optional logger. It receives a row count only.</param>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is null, empty or white space.</exception>
    public FileUsageLedger(string directory, TimeProvider? clock = null, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _path = System.IO.Path.Combine(directory, FileName);
        _clock = clock ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>Gets the ledger file path.</summary>
    public string LedgerPath => _path;

    /// <summary>
    /// Appends one end-of-turn row and drops rows whose day is more than <see cref="RetentionDays"/> before today.
    /// Occupancy is stored as reported; it is not added to earlier rows.
    /// </summary>
    /// <param name="sessionId">The session the update belongs to.</param>
    /// <param name="used">The tokens the conversation occupies now. Not negative.</param>
    /// <param name="size">The context window in tokens. Not negative.</param>
    /// <param name="cost">The session-cumulative cost, or null when the engine reported none.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="used"/> or <paramref name="size"/> is negative.</exception>
    public void Append(string sessionId, long used, long size, UsageCost? cost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentOutOfRangeException.ThrowIfNegative(used);
        ArgumentOutOfRangeException.ThrowIfNegative(size);

        var row = new UsageLedgerEntry(sessionId, DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime), size, used, cost);
        lock (_gate)
        {
            var kept = ReadUnlocked().Where(entry => !IsExpired(entry.Day)).Append(row).ToList();
            WriteUnlocked(kept);
            if (_logger is not null)
            {
                AppendedMessage(_logger, kept.Count, null);
            }
        }
    }

    /// <summary>Reads every row still on disk, in append order.</summary>
    /// <returns>The rows. Empty when the file is missing.</returns>
    public IReadOnlyList<UsageLedgerEntry> Read()
    {
        lock (_gate)
        {
            return ReadUnlocked();
        }
    }

    private bool IsExpired(DateOnly day) => day < DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime).AddDays(-RetentionDays);

    private List<UsageLedgerEntry> ReadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var rows = new List<UsageLedgerEntry>();
        foreach (var line in File.ReadAllLines(_path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            rows.Add(JsonSerializer.Deserialize(line, UsageJsonContext.Default.UsageLedgerEntry) ?? throw new JsonException("A usage ledger line was null."));
        }

        return rows;
    }

    private void WriteUnlocked(IReadOnlyList<UsageLedgerEntry> rows)
    {
        EnsureDirectory();
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.Append(JsonSerializer.Serialize(row, UsageJsonContext.Default.UsageLedgerEntry));
            builder.Append('\n');
        }

        var temporary = System.IO.Path.Combine(_directory, "." + FileName + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp");
        try
        {
            WriteOwnerFile(temporary, Utf8.GetBytes(builder.ToString()));
            File.Move(temporary, _path, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_path, OwnerFileMode);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_directory, OwnerDirectoryMode);
        }
    }

    private static void WriteOwnerFile(string path, byte[] content)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFileMode;
        }

        using var stream = new FileStream(path, options);
        stream.Write(content);
        stream.Flush(flushToDisk: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerFileMode);
        }
    }
}
