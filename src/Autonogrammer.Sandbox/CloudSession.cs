namespace Autonogrammer.Sandbox;

/// <summary>
/// An immutable snapshot of one cloud session.
/// </summary>
/// <param name="SessionId">The session identifier.</param>
/// <param name="ContainerId">The session's single container.</param>
/// <param name="VolumeId">The session's single volume.</param>
/// <param name="State">Whether the container is running or asleep.</param>
/// <param name="BootGeneration">The container boot generation observed at the last lifecycle change.</param>
/// <param name="LastActivityAt">When the session was last opened, written to or reattached.</param>
public sealed record CloudSession(
    string SessionId,
    string ContainerId,
    string VolumeId,
    CloudSessionState State,
    int BootGeneration,
    DateTimeOffset LastActivityAt);
