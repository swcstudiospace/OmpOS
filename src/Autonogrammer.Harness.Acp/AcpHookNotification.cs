using Autonogrammer.Contracts.Hooks;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// What an Autonogrammer agent worker tells its client about the hooks of a cloud session, carried by the <c>_autonogrammer/hook</c> notification (see
/// <see cref="AcpHookNotificationFrames"/>). It is either the record of one hook run (<see cref="Run"/>) or the state of the session's hooks
/// (<see cref="State"/>), never both. A client that does not know the method ignores it, as ACP requires for an extension notification.
/// </summary>
public sealed record AcpHookNotification
{
    /// <summary>The state of a session whose project hooks are loaded and guard its tool calls.</summary>
    public const string StateActive = "active";

    /// <summary>The state of a session whose workspace has no project hooks to run.</summary>
    public const string StateNone = "none";

    /// <summary>The state of a session that has project hooks but no valid consent snapshot: none of them runs.</summary>
    public const string StateUntrusted = "untrusted";

    /// <summary>The state of a session whose workspace was not writable when it started: hooks do not run in the read-only phase.</summary>
    public const string StateNotWritable = "notWritable";

    /// <summary>The state of a session that has consented project hooks the worker cannot enforce, because the hooks extension for omp is missing.</summary>
    public const string StateUnavailable = "unavailable";

    /// <summary>Gets the hook run this notification reports, or null when it reports a state.</summary>
    public HookRunEvent? Run { get; init; }

    /// <summary>Gets the state of the session's hooks (one of the <c>State…</c> constants), or null when the notification reports a run.</summary>
    public string? State { get; init; }

    /// <summary>Gets how many project hooks the worker found and would run, when <see cref="State"/> is set.</summary>
    public int HookCount { get; init; }

    /// <summary>Gets a short reason for the state, free of paths and secrets, or null.</summary>
    public string? Reason { get; init; }
}
