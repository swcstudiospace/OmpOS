using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.InMemory;

/// <summary>
/// Emits a permission request and holds the stream until the script's <see cref="InMemoryScript.PermissionResolver"/>
/// answers or <see cref="InMemoryScript.PermissionDecisionTimeout"/> elapses. Steps after the pause run once it ends.
/// </summary>
/// <param name="Request">The request to emit. Its <see cref="PermissionRequestEvent.RequestId"/> addresses the decision.</param>
public sealed record PermissionPauseStep(PermissionRequestEvent Request) : InMemoryStep;
