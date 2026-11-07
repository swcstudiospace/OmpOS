namespace Autonogrammer.Presentation.Dispatch;

/// <summary>Maps a host failure to the phase the surfaces show for it. A feature that is off is Disabled, never a spinner.</summary>
public static class DispatchPhaseMap
{
    /// <summary>Gets the phase for a host failure kind.</summary>
    /// <param name="kind">The failure kind.</param>
    /// <returns>The phase.</returns>
    public static DispatchPhase ForHostFailure(DispatchHostFailureKind kind) => kind switch
    {
        DispatchHostFailureKind.NotConfigured => DispatchPhase.Disabled,
        DispatchHostFailureKind.Disabled => DispatchPhase.Disabled,
        DispatchHostFailureKind.SignedOut => DispatchPhase.SignedOut,
        DispatchHostFailureKind.Unavailable => DispatchPhase.Unreachable,
        DispatchHostFailureKind.Rejected => DispatchPhase.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
