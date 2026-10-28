using System.Globalization;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Sessions;

/// <summary>
/// The persisted record of one agent session. The registry owns the records and replaces a record on every change, so a
/// value read earlier never changes. <see cref="ResumeReference"/> is the native omp session reference: it is a credential-like
/// handle, so <see cref="ToString"/> never prints it and no log line carries it.
/// </summary>
public sealed record AgentSessionRecord
{
    /// <summary>Gets the session identifier. It matches <c>^[A-Za-z0-9_.-]{1,120}$</c> and is safe as a directory name.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the session title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the persisted lifecycle.</summary>
    public required SessionLifecycle Lifecycle { get; init; }

    /// <summary>Gets the time the session was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the time of the last observed activity.</summary>
    public required DateTimeOffset LastActivity { get; init; }

    /// <summary>Gets the Graph identifier of the session's plan, or null when it has none.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the identifier of the task the session works on, or null.</summary>
    public string? TaskId { get; init; }

    /// <summary>Gets the absolute path of the workspace the session runs in, or null when it is unknown.</summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>Gets the absolute path of the directory that holds this session's private state.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>Gets the identifier of the omp session behind the ACP session, or null until the engine reports it.</summary>
    public string? AcpSessionId { get; init; }

    /// <summary>Gets the native omp session reference used to resume the session after a restart, or null. It is never logged or printed.</summary>
    public string? ResumeReference { get; init; }

    /// <summary>Gets the time of the last liveness signal from the session's engine.</summary>
    public required DateTimeOffset Heartbeat { get; init; }

    /// <summary>Gets what the session is: a chat behind the Agent Panel or an omp terminal. Defaults to <see cref="SessionKind.Chat"/>.</summary>
    public SessionKind Kind { get; init; } = SessionKind.Chat;

    /// <summary>Returns the list-row view of the record. It carries no resume reference.</summary>
    /// <returns>The summary.</returns>
    public SessionSummary ToSummary() => new()
    {
        Id = Id,
        Title = Title,
        Lifecycle = Lifecycle,
        CreatedAt = CreatedAt,
        LastActivity = LastActivity,
        GraphId = GraphId,
        TaskId = TaskId,
        WorkspaceRoot = WorkspaceRoot,
        Kind = Kind,
    };

    /// <summary>Returns a description of the record that names the resume reference only as present or absent.</summary>
    /// <returns>The description.</returns>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"AgentSessionRecord {{ Id = {Id}, Kind = {Kind}, Lifecycle = {Lifecycle}, CreatedAt = {CreatedAt:O}, LastActivity = {LastActivity:O}, GraphId = {GraphId}, TaskId = {TaskId}, AcpSessionId = {AcpSessionId}, ResumeReference = {(ResumeReference is null ? "none" : "[redacted]")} }}");
}
