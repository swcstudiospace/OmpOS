using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Harness.AgentLoop.Sessions;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Cowork.Sessions;

/// <summary>
/// The persistent list of Cowork tasks for one account. It owns a private agent-session registry whose state root is
/// <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;</c>, so the file is <c>sessions.json</c> there and session folders are
/// <c>sessions/&lt;id&gt;</c> beside it. Atomic write, quarantine, the 200 cap and Lost recovery are the wrapped registry's.
/// Sign-out does not delete the files.
/// </summary>
public sealed class CoworkSessionRegistry
{
    /// <summary>The name of the registry file inside the account folder.</summary>
    public const string RegistryFileName = "sessions.json";

    private readonly AgentSessionRegistry _sessions;

    private CoworkSessionRegistry(AgentSessionRegistry sessions, string stateFilePath, string? readBanner)
    {
        _sessions = sessions;
        StateFilePath = stateFilePath;
        ReadBanner = readBanner;
    }

    /// <summary>Gets the full path of <c>sessions.json</c>.</summary>
    public string StateFilePath { get; }

    /// <summary>
    /// Gets the banner set when the file could not be read, or null when the file was missing or valid.
    /// The kept file is named in the banner and is not overwritten.
    /// </summary>
    public string? ReadBanner { get; }

    /// <summary>Opens the registry for the signed-in account. A missing file is an empty list.</summary>
    /// <param name="options">The state root.</param>
    /// <param name="gate">The signed-in account. A signed-out gate is refused and no file is deleted.</param>
    /// <param name="timeProvider">The clock for activity and quarantine stamps.</param>
    /// <param name="logger">Receives warnings, or null to discard them.</param>
    /// <returns>The opened registry.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="InvalidOperationException">The gate is signed out or has no folder key.</exception>
    public static CoworkSessionRegistry Open(
        CoworkSessionOptions options,
        ICoworkAccountGate gate,
        TimeProvider timeProvider,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!gate.IsSignedIn || string.IsNullOrWhiteSpace(gate.AccountFolderKey))
        {
            throw new InvalidOperationException("Cowork storage requires a signed-in account.");
        }

        var accountDirectory = CoworkAccountFolder.AccountDirectory(options.StateRoot, gate.AccountFolderKey);
        var stateFilePath = Path.Combine(accountDirectory, RegistryFileName);
        var before = QuarantineNames(accountDirectory);
        var sessions = new AgentSessionRegistry(
            stateFilePath,
            timeProvider,
            logger,
            new AgentSessionRegistryOptions { StateRoot = accountDirectory });
        var kept = QuarantineNames(accountDirectory).Except(before, StringComparer.Ordinal).FirstOrDefault();
        var banner = kept is null
            ? null
            : "Cowork tasks could not be read; the file was kept as " + kept;
        return new CoworkSessionRegistry(sessions, stateFilePath, banner);
    }

    /// <summary>Creates a task and persists it.</summary>
    /// <param name="title">The title. A blank title becomes the wrapped default; a long title is cut at 200 characters.</param>
    /// <returns>The new task row.</returns>
    /// <exception cref="CoworkSessionLimitException">The registry already holds 200 tasks.</exception>
    public CoworkRegistryRow Create(string? title)
    {
        AgentSessionRecord record;
        try
        {
            record = _sessions.Create(title, workspaceRoot: null);
        }
        catch (AgentSessionLimitException exception)
        {
            throw new CoworkSessionLimitException(exception);
        }

        return ToRow(record);
    }

    /// <summary>Sets the lifecycle of a task. <see cref="CoworkSessionStatus.WaitingForDecision"/> is not stored in this file.</summary>
    /// <param name="id">The task identifier.</param>
    /// <param name="status">The new status.</param>
    /// <returns>True when the task exists.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not a status this file stores.</exception>
    public bool SetStatus(string id, CoworkSessionStatus status)
    {
        if (status == CoworkSessionStatus.WaitingForDecision || !Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return _sessions.SetLifecycle(id, ToLifecycle(status));
    }

    /// <summary>Returns the tasks, most recently active first.</summary>
    /// <returns>A snapshot that later changes do not affect.</returns>
    public IReadOnlyList<CoworkRegistryRow> List() => [.. _sessions.SnapshotRecords().Select(ToRow)];

    /// <summary>
    /// Marks every Running task Lost. Call it once when the Cowork host first opens this registry. It does not open or rewrite any other file.
    /// </summary>
    /// <returns>The identifiers that became Lost.</returns>
    public IReadOnlyList<string> RecoverAfterRestart() => _sessions.RecoverAfterRestart();

    private static CoworkRegistryRow ToRow(AgentSessionRecord record) =>
        new(record.Id, record.Title, ToStatus(record.Lifecycle));

    private static CoworkSessionStatus ToStatus(SessionLifecycle lifecycle) => lifecycle switch
    {
        SessionLifecycle.Created => CoworkSessionStatus.Created,
        SessionLifecycle.Running => CoworkSessionStatus.Running,
        SessionLifecycle.Paused => CoworkSessionStatus.Paused,
        SessionLifecycle.Completed => CoworkSessionStatus.Completed,
        SessionLifecycle.Failed => CoworkSessionStatus.Failed,
        SessionLifecycle.Lost => CoworkSessionStatus.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle)),
    };

    private static SessionLifecycle ToLifecycle(CoworkSessionStatus status) => status switch
    {
        CoworkSessionStatus.Created => SessionLifecycle.Created,
        CoworkSessionStatus.Running => SessionLifecycle.Running,
        CoworkSessionStatus.Paused => SessionLifecycle.Paused,
        CoworkSessionStatus.Completed => SessionLifecycle.Completed,
        CoworkSessionStatus.Failed => SessionLifecycle.Failed,
        CoworkSessionStatus.Lost => SessionLifecycle.Lost,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static HashSet<string> QuarantineNames(string accountDirectory)
    {
        if (!Directory.Exists(accountDirectory))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return Directory.EnumerateFiles(accountDirectory, RegistryFileName + ".quarantined*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
    }
}

