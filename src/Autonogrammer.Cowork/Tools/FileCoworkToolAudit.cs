using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>
/// Appends owner-only hash-chained JSONL at <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/tool-audit/YYYY-MM-DD.jsonl</c>.
/// Each line carries the SHA-256 of the previous line. A write failure never fails the caller. Nothing goes to the ledger.
/// </summary>
public sealed class FileCoworkToolAudit : ICoworkToolAudit, IDisposable
{
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _previousHash;
    private string? _openPath;

    /// <summary>
    /// Creates the audit.
    /// </summary>
    /// <param name="stateRoot">The application state root.</param>
    /// <param name="accountId">The account id.</param>
    /// <param name="clock">The clock used for the file date and the record time. Null uses <see cref="TimeProvider.System"/>.</param>
    public FileCoworkToolAudit(string stateRoot, string accountId, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _directory = Path.Combine(CoworkOwnerFiles.AccountDirectory(stateRoot, accountId), "tool-audit");
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Gets the directory that holds the daily files.</summary>
    public string DirectoryPath => _directory;

    /// <summary>Releases the append gate.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
#pragma warning disable MA0051 // The append, hash and mode steps stay in one method so a failed write cannot be split from the chain update.
    public async ValueTask RecordAsync(CoworkToolAuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            var now = _clock.GetUtcNow();
            var path = Path.Combine(_directory, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");
            CoworkOwnerFiles.EnsureDirectory(_directory);
            if (!string.Equals(_openPath, path, StringComparison.Ordinal))
            {
                _previousHash = ReadLastHash(path);
                _openPath = path;
            }

            var line = new CoworkAuditLine(
                Seq: NextSeq(path),
                Time: now,
                SessionId: entry.SessionId,
                Tool: entry.ToolName,
                Origin: entry.Origin.ToString(),
                Policy: entry.Policy.ToString(),
                Approval: entry.Approval?.ToString(),
                IsError: entry.IsError,
                ResultKinds: entry.ResultKinds.ToString(),
                Duration: entry.Duration.TotalMilliseconds,
                Target: entry.Target,
                Prev: _previousHash ?? new string('0', 64));
            var json = System.Text.Json.JsonSerializer.Serialize(line, CoworkToolsJsonContext.Default.CoworkAuditLine);
            var bytes = Utf8.GetBytes(json);
            var options = new FileStreamOptions
            {
                Mode = FileMode.Append,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFileMode;
            }

            var stream = new FileStream(path, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                var newline = "\n"u8.ToArray();
                await stream.WriteAsync(newline, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, OwnerFileMode);
            }

            _previousHash = Convert.ToHexString(SHA256.HashData(bytes)).ToUpperInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _ = exception;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
#pragma warning restore MA0051

    /// <summary>Recomputes the hash chain. A tampered prefix makes this return false.</summary>
    /// <param name="path">The JSONL file.</param>
    /// <returns>True when every line's prev matches the SHA-256 of the previous line.</returns>
    public static bool VerifyChain(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return true;
        }

        var expected = new string('0', 64);
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length == 0)
            {
                continue;
            }

            CoworkAuditLine? parsed;
            try
            {
                parsed = System.Text.Json.JsonSerializer.Deserialize(line, CoworkToolsJsonContext.Default.CoworkAuditLine);
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }

            if (parsed is null || !string.Equals(parsed.Prev, expected, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            expected = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(line))).ToUpperInvariant();
        }

        return true;
    }

    private static string ReadLastHash(string path)
    {
        if (!File.Exists(path))
        {
            return new string('0', 64);
        }

        string? last = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length > 0)
            {
                last = line;
            }
        }

        return last is null
            ? new string('0', 64)
            : Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(last))).ToUpperInvariant();
    }

    private static long NextSeq(string path)
    {
        if (!File.Exists(path))
        {
            return 1;
        }

        long count = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (line.Length > 0)
            {
                count++;
            }
        }

        return count + 1;
    }
}
