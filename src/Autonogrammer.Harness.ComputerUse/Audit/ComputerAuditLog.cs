using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse.Audit;

/// <summary>
/// Owner-only JSON Lines under computer-use/audit/YYYY-MM-DD.jsonl.
/// Each line's prev is the SHA-256 of the previous line. A failed write is <see cref="DenialCode.AuditUnavailable"/>.
/// Nothing is written to cowork_events or a ledger. Thumbnails stay off unless the reader is asked, and the view has no thumbnail bytes.
/// Plan note: CoworkToolAuditSinkAdapter matches ADR-0032 D5. Re-read ICoworkToolAudit when phase 70 field names move and reconcile names without changing redaction.
/// </summary>
public sealed class ComputerAuditLog : IComputerAuditReader, IComputerUseAuditSink
{
    /// <summary>Prev value of the first line.</summary>
    public const string GenesisPrev = "0000000000000000000000000000000000000000000000000000000000000000";

    private readonly string _directory;
    private readonly TimeProvider _timeProvider;
    private readonly bool _thumbnails;
    private readonly Lock _gate = new();
    private long _seq;

    /// <summary>Creates the log. The directory is created owner-only.</summary>
    /// <param name="stateDirectory">State directory supplied by the head.</param>
    /// <param name="time">Clock for the file date and retention.</param>
    /// <param name="thumbnails">Key <c>Cowork:ComputerUse:AuditThumbnails</c>. Default off.</param>
    public ComputerAuditLog(string stateDirectory, TimeProvider time, bool thumbnails = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(stateDirectory);
        _directory = Path.Combine(stateDirectory, "computer-use", "audit");
        _timeProvider = time;
        _thumbnails = thumbnails;
        Directory.CreateDirectory(_directory);
        OwnerOnly(_directory);
    }

    /// <summary>Hashes typed text. The caller must not pass the text to <see cref="TryAppend"/>.</summary>
    /// <param name="text">Typed text. <c>Audit:TypedText</c> is Off, so only length and hash are kept.</param>
    /// <returns>Length and uppercase SHA-256.</returns>
    public static (int Length, string Sha256) HashTypedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return (text.Length, HashBytes(Encoding.UTF8.GetBytes(text)));
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryWriteAheadAsync(ComputerActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = cancellationToken;
        bool wrote = TryAppend(new ComputerAuditWrite
        {
            SessionId = request.SessionId,
            Tool = request.ToolName,
            Summary = request.ToolName,
            AppIdentity = request.Target?.AppIdentity ?? string.Empty,
            Title = string.Empty,
            Decision = "pending",
            ApprovalOrigin = request.Origin.ToString(),
            Outcome = "before",
        });
        return ValueTask.FromResult(wrote);
    }

    /// <summary>Appends one redacted line. Clipboard contents are not accepted.</summary>
    /// <param name="write">Redacted facts.</param>
    /// <returns><see langword="false"/> when the write failed.</returns>
    public bool TryAppend(ComputerAuditWrite write)
    {
        ArgumentNullException.ThrowIfNull(write);
        try
        {
            lock (_gate)
            {
                Rotate();
                string path = PathFor(_timeProvider.GetUtcNow());
                string line = Line(write, Previous(path));
                using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
                stream.Flush(flushToDisk: true);
                OwnerOnly(path);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Writes before <paramref name="inject"/> and again after. A failed write denies.</summary>
    /// <param name="write">Redacted facts.</param>
    /// <param name="inject">Injection. It runs only after the first line is flushed.</param>
    /// <param name="cancellationToken">Token that cancels injection.</param>
    /// <returns><see cref="DenialCode.AuditUnavailable"/> when a write failed.</returns>
    public async ValueTask<DenialCode?> WriteAroundAsync(
        ComputerAuditWrite write,
        Func<CancellationToken, ValueTask> inject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(inject);
        if (!TryAppend(write with { Outcome = "before" }))
        {
            return DenialCode.AuditUnavailable;
        }

        await inject(cancellationToken).ConfigureAwait(false);
        if (!TryAppend(write with { Outcome = "after" }))
        {
            return DenialCode.AuditUnavailable;
        }

        return null;
    }

    /// <summary>Denial for a failed write.</summary>
    /// <param name="wrote">Whether the write succeeded.</param>
    /// <returns>The denial, or null.</returns>
    public static DenialCode? DenialFor(bool wrote) => wrote ? null : DenialCode.AuditUnavailable;

    /// <summary>True when every line's prev matches the SHA-256 of the previous line.</summary>
    /// <returns><see langword="false"/> when a prefix line was edited or truncated.</returns>
    public bool Verify()
    {
        lock (_gate)
        {
            foreach (string path in Directory.GetFiles(_directory, "*.jsonl"))
            {
                if (!VerifyFile(path))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<AuditRecordView>> ReadAsync(int days, bool includeThumbnails, CancellationToken cancellationToken)
    {
        _ = includeThumbnails;
        _ = _thumbnails;
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset start = new(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        start = start.AddDays(1 - days);
        List<AuditRecordView> rows = [];
        lock (_gate)
        {
            if (!Directory.Exists(_directory))
            {
                return ValueTask.FromResult<IReadOnlyList<AuditRecordView>>(rows);
            }

            foreach (string path in Directory.GetFiles(_directory, "*.jsonl"))
            {
                ReadFile(path, start, rows);
            }
        }

        return ValueTask.FromResult<IReadOnlyList<AuditRecordView>>(rows);
    }

    private static void ReadFile(string path, DateTimeOffset start, List<AuditRecordView> rows)
    {
        foreach (string line in File.ReadAllLines(path))
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement row = doc.RootElement;
            DateTimeOffset time = DateTimeOffset.Parse(row.GetProperty("time").GetString()!, CultureInfo.InvariantCulture);
            if (time < start)
            {
                continue;
            }

            rows.Add(new AuditRecordView
            {
                Seq = row.GetProperty("seq").GetInt64(),
                Time = time,
                SessionId = row.GetProperty("session").GetString()!,
                Tool = row.GetProperty("tool").GetString()!,
                Summary = row.GetProperty("summary").GetString()!,
                AppIdentity = row.GetProperty("appIdentity").GetString()!,
                TitleTruncated = row.GetProperty("title").GetString()!,
                Decision = row.GetProperty("decision").GetString()!,
                Denial = row.TryGetProperty("denial", out JsonElement denial) && denial.ValueKind == JsonValueKind.String
                    ? Enum.Parse<DenialCode>(denial.GetString()!)
                    : null,
            });
        }
    }

    private string Line(ComputerAuditWrite write, string prev)
    {
        _seq++;
        string title = write.Title.Length <= 80 ? write.Title : write.Title[..80];
        StringBuilder builder = new();
        builder.Append("{\"seq\":").Append(_seq.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"time\":").Append(Json(_timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)));
        builder.Append(",\"session\":").Append(Json(write.SessionId));
        builder.Append(",\"tool\":").Append(Json(write.Tool));
        builder.Append(",\"summary\":").Append(Json(write.Summary));
        builder.Append(",\"appIdentity\":").Append(Json(write.AppIdentity));
        builder.Append(",\"title\":").Append(Json(title));
        builder.Append(",\"decision\":").Append(Json(write.Decision));
        builder.Append(",\"denial\":").Append(write.Denial is null ? "null" : Json(write.Denial.Value.ToString()));
        builder.Append(",\"approvalOrigin\":").Append(Json(write.ApprovalOrigin));
        builder.Append(",\"outcome\":").Append(Json(write.Outcome));
        builder.Append(",\"frameSha256\":").Append(write.FrameSha256 is null ? "null" : Json(write.FrameSha256));
        builder.Append(",\"typedLength\":").Append(write.TypedLength is null ? "null" : write.TypedLength.Value.ToString(CultureInfo.InvariantCulture));
        builder.Append(",\"typedSha256\":").Append(write.TypedSha256 is null ? "null" : Json(write.TypedSha256));
        builder.Append(",\"prev\":").Append(Json(prev)).Append('}');
        return builder.ToString();
    }

    private void Rotate()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset cutoff = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero).AddDays(-30);
        foreach (string path in Directory.GetFiles(_directory, "*.jsonl"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day))
            {
                continue;
            }

            DateTimeOffset dayStart = new(day.Year, day.Month, day.Day, 0, 0, 0, TimeSpan.Zero);
            if (dayStart >= cutoff)
            {
                continue;
            }

            File.Delete(path);
            ComputerAuditWrite deleted = new()
            {
                SessionId = string.Empty,
                Tool = "retention",
                Summary = "deleted " + name,
                AppIdentity = string.Empty,
                Title = string.Empty,
                Decision = "deleted",
                ApprovalOrigin = "local",
                Outcome = "deleted",
            };
            string today = PathFor(now);
            string line = Line(deleted, Previous(today));
            File.AppendAllText(today, line + "\n");
            OwnerOnly(today);
        }
    }

    private string PathFor(DateTimeOffset time) =>
        Path.Combine(_directory, time.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".jsonl");

    private static bool VerifyFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        string prev = GenesisPrev;
        for (int i = 0; i < lines.Length; i++)
        {
            if (!PrevMatches(lines[i], prev))
            {
                return false;
            }

            prev = HashBytes(Encoding.UTF8.GetBytes(lines[i]));
        }

        return true;
    }

    private static bool PrevMatches(string line, string prev)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            return string.Equals(doc.RootElement.GetProperty("prev").GetString(), prev, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Previous(string path)
    {
        if (!File.Exists(path))
        {
            return GenesisPrev;
        }

        string[] lines = File.ReadAllLines(path);
        return lines.Length == 0 ? GenesisPrev : HashBytes(Encoding.UTF8.GetBytes(lines[^1]));
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string Json(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void OwnerOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        UnixFileMode mode = Directory.Exists(path)
            ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            : UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.SetUnixFileMode(path, mode);
    }
}
