using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Stages;

namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>
/// The file-based steering channel of one session. A directive is written to <c>inbox/&lt;id&gt;.json</c> in the session's
/// steering directory (<see cref="SessionStateLayout.SteerDirectory"/>) through a temporary file and a rename, so the omp
/// plugin never sees a partial file; the plugin answers in <c>acks/&lt;id&gt;.json</c>. A directive is only queued when the
/// plugin's heartbeat (<c>alive.json</c>) is no older than <see cref="FileSteeringChannelOptions.HeartbeatFreshness"/>, because
/// a directive written for a process that is gone would wait forever. The channel never logs the text of a directive and
/// no exception message contains it or a path. Identifiers are a zero-padded counter and a random suffix, so the plugin, which
/// reads the inbox in file-name order, sees the directives of one channel in the order they were sent.
/// </summary>
public sealed class FileSteeringChannel : ISteeringChannel
{
    /// <summary>The most directives that may wait in the inbox. The plugin examines 64 files per pass.</summary>
    public const int MaxQueuedDirectives = 64;

    /// <summary>The longest text of a directive, in UTF-8 bytes.</summary>
    public const int MaxTextBytes = 8192;

    /// <summary>The largest directive file the plugin accepts, in bytes.</summary>
    public const int MaxFileBytes = 16 * 1024;

    private const int MaxIdLength = 64;
    private const int MaxAckBytes = 16 * 1024;
    private const int MaxAliveBytes = 4 * 1024;
    private const int RandomSuffixBytes = 5;
    private const string InboxName = "inbox";
    private const string AcksName = "acks";
    private const string AliveName = "alive.json";
    private const string SourceName = "ompos";

    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    // Non-ASCII text stays literal on disk so that 8192 bytes of text fit in the plugin's 16 KiB file limit.
    private static readonly SteeringJsonContext Wire = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    private readonly string _steerDirectory;
    private readonly TimeProvider _clock;
    private readonly FileSteeringChannelOptions _options;
    private long _counter;

    /// <summary>Creates the channel of one session.</summary>
    /// <param name="sessionDirectory">The directory returned by <see cref="SessionStateLayout.SessionDirectory"/>.</param>
    /// <param name="timeProvider">The clock for the heartbeat check, the directive time and the acknowledgement wait.</param>
    /// <param name="options">The options. The defaults when null.</param>
    /// <exception cref="ArgumentOutOfRangeException">An interval in the options is not positive.</exception>
    public FileSteeringChannel(string sessionDirectory, TimeProvider timeProvider, FileSteeringChannelOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options ?? new FileSteeringChannelOptions();
        if (_options.AckPollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.AckPollInterval, "The acknowledgement poll interval must be positive.");
        }

        if (_options.HeartbeatFreshness <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.HeartbeatFreshness, "The heartbeat freshness must be positive.");
        }

        _steerDirectory = SessionStateLayout.SteerDirectory(sessionDirectory);
        _clock = timeProvider;
    }

    /// <inheritdoc />
    public async Task<SteeringReceipt> SendAsync(SteeringDirective directive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directive);
        ValidateDirective(directive);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_steerDirectory))
        {
            throw new SteeringUnavailableException(
                SteeringFailureReason.NoSteerDirectory,
                "This session has no steering inbox: omp was not started for it.");
        }

        await EnsureServingAsync(cancellationToken).ConfigureAwait(false);

        var inbox = Path.Combine(_steerDirectory, InboxName);
        EnsureRoom(inbox);

        var id = NextId();
        var now = _clock.GetUtcNow();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new SteeringDirectiveWire
            {
                Version = 1,
                Id = id,
                Mode = ModeName(directive.Mode),
                Text = directive.Text,
                CreatedAt = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                Source = SourceName,
            },
            Wire.SteeringDirectiveWire);
        if (bytes.Length > MaxFileBytes)
        {
            throw new SteeringUnavailableException(
                SteeringFailureReason.InvalidDirective,
                "The steering message is too long once it is encoded.");
        }

        await WriteAtomicallyAsync(Path.Combine(inbox, id + ".json"), bytes, cancellationToken).ConfigureAwait(false);
        return new SteeringReceipt(id, directive.Mode, now);
    }

    /// <inheritdoc />
    public async Task<SteeringAck?> WaitForAckAsync(string id, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("A steering identifier is 1 to 64 letters, digits, '_' or '-'.", nameof(id));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(timeout, TimeSpan.Zero);

        var path = Path.Combine(_steerDirectory, AcksName, id + ".json");
        var deadline = _clock.GetUtcNow() + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await StageFileReader.ReadAllAsync(path, MaxAckBytes, cancellationToken).ConfigureAwait(false);
            if (read.Status == FileReadStatus.Ok && ParseAck(read.Content.Span, id) is { } ack)
            {
                return ack;
            }

            var remaining = deadline - _clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return null;
            }

            await Task.Delay(remaining < _options.AckPollInterval ? remaining : _options.AckPollInterval, _clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateDirective(SteeringDirective directive)
    {
        if (!Enum.IsDefined(directive.Mode))
        {
            throw Invalid("The steering mode is not known.");
        }

        if (string.IsNullOrWhiteSpace(directive.Text))
        {
            throw Invalid("The steering message is empty.");
        }

        if (Encoding.UTF8.GetByteCount(directive.Text) > MaxTextBytes)
        {
            throw Invalid("The steering message is longer than 8192 bytes.");
        }

        static SteeringUnavailableException Invalid(string message) => new(SteeringFailureReason.InvalidDirective, message);
    }

    private static string ModeName(SteeringDirectiveMode mode) => mode switch
    {
        SteeringDirectiveMode.Steer => "steer",
        SteeringDirectiveMode.FollowUp => "followUp",
        _ => "aside",
    };

    private static SteeringDirectiveMode? ParseMode(string? name) => name switch
    {
        "steer" => SteeringDirectiveMode.Steer,
        "followUp" => SteeringDirectiveMode.FollowUp,
        "aside" => SteeringDirectiveMode.Aside,
        _ => null,
    };

    private static SteeringAckReason? ParseReason(string? code) => code switch
    {
        null => null,
        "invalid-json" => SteeringAckReason.InvalidJson,
        "invalid-schema" => SteeringAckReason.InvalidSchema,
        "too-large" => SteeringAckReason.TooLarge,
        "bad-id" => SteeringAckReason.BadId,
        "duplicate" => SteeringAckReason.Duplicate,
        "blank-text" => SteeringAckReason.BlankText,
        "deliver-error" => SteeringAckReason.DeliverError,
        _ => SteeringAckReason.Unknown,
    };

    private static SteeringAckStatus? ParseStatus(string? name) => name switch
    {
        "delivered" => SteeringAckStatus.Delivered,
        "rejected" => SteeringAckStatus.Rejected,
        "failed" => SteeringAckStatus.Failed,
        _ => null,
    };

    private static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
        {
            return false;
        }

        foreach (var character in id)
        {
            var valid = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    // A partial, foreign or invalid acknowledgement is not an answer: the caller keeps waiting for the real one.
    private static SteeringAck? ParseAck(ReadOnlySpan<byte> utf8, string expectedId)
    {
        SteeringAckWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize(utf8, Wire.SteeringAckWire);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire is not { Version: 1 } || !string.Equals(wire.Id, expectedId, StringComparison.Ordinal) || ParseStatus(wire.Status) is not { } status)
        {
            return null;
        }

        return new SteeringAck
        {
            Id = expectedId,
            Status = status,
            Reason = ParseReason(wire.Reason),
            Mode = ParseMode(wire.Mode),
            At = TryParseTime(wire.At),
            Idle = wire.Idle,
        };
    }

    private static DateTimeOffset? TryParseTime(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private static async Task WriteAtomicallyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerFileMode;
        }

        try
        {
            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DeleteQuietly(temporary);
            throw new SteeringUnavailableException(
                SteeringFailureReason.NotServing,
                "The steering inbox of this session cannot be written.",
                ex);
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(temporary);
            throw;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A temporary file that cannot be deleted is ignored by the plugin, which reads only <id>.json files.
        }
    }

    private string NextId()
    {
        var number = Interlocked.Increment(ref _counter);
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(RandomSuffixBytes));
        return string.Create(CultureInfo.InvariantCulture, $"{number:D6}-{suffix}");
    }

    private static void EnsureRoom(string inbox)
    {
        try
        {
            var waiting = Directory.EnumerateFiles(inbox, "*.json", SearchOption.TopDirectoryOnly).Take(MaxQueuedDirectives).Count();
            if (waiting >= MaxQueuedDirectives)
            {
                throw new SteeringUnavailableException(
                    SteeringFailureReason.QueueFull,
                    "Too many steering messages are waiting for this session. Try again in a moment.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SteeringUnavailableException(
                SteeringFailureReason.NotServing,
                "The steering inbox of this session is not ready.",
                ex);
        }
    }

    private async Task EnsureServingAsync(CancellationToken cancellationToken)
    {
        var read = await StageFileReader.ReadAllAsync(Path.Combine(_steerDirectory, AliveName), MaxAliveBytes, cancellationToken).ConfigureAwait(false);
        if (read.Status == FileReadStatus.Ok && IsFresh(read.Content.Span))
        {
            return;
        }

        throw new SteeringUnavailableException(
            SteeringFailureReason.NotServing,
            "This session is not taking steering messages: its omp process is not running or has stopped answering.");
    }

    private bool IsFresh(ReadOnlySpan<byte> utf8)
    {
        SteeringAliveWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize(utf8, Wire.SteeringAliveWire);
        }
        catch (JsonException)
        {
            return false;
        }

        if (wire is not { Version: 1 } || TryParseTime(wire.LastSeen) is not { } lastSeen)
        {
            return false;
        }

        var age = _clock.GetUtcNow() - lastSeen;
        return age.Duration() <= _options.HeartbeatFreshness;
    }
}
