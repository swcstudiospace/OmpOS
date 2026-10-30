#pragma warning disable MA0048 // The plan owns this file for the scheduling DTOs.

using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Contracts.Cowork.Scheduling;

/// <summary>
/// A scheduled-task draft the desktop editor holds. Definitions stay in the per-user automations file on this device.
/// This contract is not sent to the API and is not synced.
/// </summary>
public sealed record ScheduledTaskDraft
{
    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the instructions each run starts with.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets the surface. This reuses the phase-68 session surface.</summary>
    public SessionSurface Surface { get; init; }

    /// <summary>Gets the optional project identifier.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Gets the trusted folders on this device. They are not part of any account sync body.</summary>
    public IReadOnlyList<string> TrustedFolders { get; init; } = [];

    /// <summary>Gets how approval is handled.</summary>
    public ScheduledApprovalMode ApprovalMode { get; init; } = ScheduledApprovalMode.Manual;

    /// <summary>Gets the optional model hint.</summary>
    public string? Model { get; init; }

    /// <summary>Gets the preset the editor turns into cron.</summary>
    public ScheduledPreset Preset { get; init; } = ScheduledPreset.Daily;

    /// <summary>Gets what to do with a missed occurrence.</summary>
    public ScheduledMissedRunPolicy MissedRunPolicy { get; init; } = ScheduledMissedRunPolicy.RunOnce;

    /// <summary>Gets whether a per-session acknowledgement of Skip was recorded.</summary>
    public bool SkipAcknowledged { get; init; }
}

/// <summary>How a scheduled session treats an action that needs approval.</summary>
public enum ScheduledApprovalMode
{
    /// <summary>Ask.</summary>
    Manual,

    /// <summary>Ask when someone is present. Unattended runs still wait.</summary>
    Auto,

    /// <summary>Do not ask. Refused unless the session is filesystem-isolated.</summary>
    Skip,
}

/// <summary>The editor's schedule choices.</summary>
public enum ScheduledPreset
{
    /// <summary>Every hour.</summary>
    Hourly,

    /// <summary>Every day.</summary>
    Daily,

    /// <summary>Every week.</summary>
    Weekly,

    /// <summary>Monday to Friday.</summary>
    Weekdays,

    /// <summary>No recurring run.</summary>
    Manual,
}

/// <summary>What to do with occurrences missed while the desktop was closed.</summary>
public enum ScheduledMissedRunPolicy
{
    /// <summary>One late run inside the catch-up window.</summary>
    RunOnce,

    /// <summary>Skip anything later than the misfire window.</summary>
    Skip,
}
