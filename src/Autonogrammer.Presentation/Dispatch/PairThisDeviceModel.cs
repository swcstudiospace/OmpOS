namespace Autonogrammer.Presentation.Dispatch;

/// <summary>This desktop's pairing, its pause state and its per-capability policy.</summary>
/// <param name="Paired">Whether this desktop is paired to the account.</param>
/// <param name="Paused">Whether Dispatch is paused on this desktop.</param>
/// <param name="StatusText">The text that says so.</param>
/// <param name="Capabilities">The policy, one row per capability.</param>
public sealed record PairThisDeviceModel(bool Paired, bool Paused, string StatusText, IReadOnlyList<DispatchCapabilityPolicy> Capabilities)
{
    /// <summary>Builds the model from this desktop's state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The model.</returns>
    public static PairThisDeviceModel From(DispatchThisDevice state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new PairThisDeviceModel(
            state.Paired,
            state.Paused,
            state.Paired ? DispatchCopy.ThisDevicePaired : DispatchCopy.ThisDeviceNotPaired,
            state.Capabilities);
    }

    /// <summary>Builds the model for a desktop whose state could not be read: it is not shown as paired and has no policy rows.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The model, whose status text says what failed.</returns>
    public static PairThisDeviceModel Unavailable(DispatchHostException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new PairThisDeviceModel(false, false, DispatchCopy.ForPhase(DispatchPhaseMap.ForHostFailure(failure.Kind)), []);
    }
}
