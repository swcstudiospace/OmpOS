#pragma warning disable MA0048 // The plan owns this file for the starter seam and its plain-data types.

namespace Autonogrammer.Harness.Automations.Scheduling;

/// <summary>
/// Which product surface a scheduled session runs on. Values match the phase-68 session surface names. This seam carries
/// no host type: the desktop registers one starter per surface.
/// </summary>
public enum ScheduledSessionSurface
{
    /// <summary>A code session. The desktop keeps its existing session starter for this surface.</summary>
    Code,

    /// <summary>A conversation session. The desktop registers that surface's starter in a later plan.</summary>
    Cowork,
}

/// <summary>How a scheduled session treats an action that needs approval.</summary>
public enum SessionApprovalMode
{
    /// <summary>Ask. An unattended run waits and is cancelled; it is never approved on its own.</summary>
    Manual,

    /// <summary>Ask with a shorter card when someone is present. Unattended, this still waits and never approves itself.</summary>
    Auto,

    /// <summary>Do not ask. Refused unless the session is filesystem-isolated.</summary>
    Skip,
}

/// <summary>How a starter ended a start. The engine records this; it does not interpret a host.</summary>
/// <param name="Started">True when a session was started.</param>
/// <param name="SessionId">The session identifier, or null.</param>
/// <param name="Error">The failure text, or null. It never holds a secret.</param>
public sealed record ScheduledSessionStart(bool Started, string? SessionId, string? Error);

/// <summary>
/// Starts one scheduled session from plain data. The engine never references a conversation host or a code host.
/// </summary>
public interface IScheduledSessionStarter
{
    /// <summary>Gets the surface this starter accepts.</summary>
    ScheduledSessionSurface Surface { get; }

    /// <summary>Starts a session.</summary>
    /// <param name="surface">The surface. It matches <see cref="Surface"/>.</param>
    /// <param name="projectId">The optional project identifier.</param>
    /// <param name="trustedFolders">The trusted folders on this device.</param>
    /// <param name="approvalMode">How approval is handled.</param>
    /// <param name="model">The optional model hint.</param>
    /// <param name="prompt">The prompt, including host-written scheduled and actual times when the run is scheduled.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>How the start ended.</returns>
    Task<ScheduledSessionStart> StartAsync(
        ScheduledSessionSurface surface,
        string? projectId,
        IReadOnlyList<string> trustedFolders,
        SessionApprovalMode approvalMode,
        string? model,
        string prompt,
        CancellationToken cancellationToken);
}
