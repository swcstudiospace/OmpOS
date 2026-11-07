namespace Autonogrammer.Presentation.Dispatch;

/// <summary>This desktop's pairing and per-capability policy.</summary>
/// <param name="Paired">Whether this desktop is paired to the account.</param>
/// <param name="StatusText">The text that says so.</param>
/// <param name="Capabilities">The policy, one row per capability.</param>
public sealed record PairThisDeviceModel(bool Paired, string StatusText, IReadOnlyList<DispatchCapabilityPolicy> Capabilities)
{
    /// <summary>Builds the model from this desktop's state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The model.</returns>
    public static PairThisDeviceModel From(DispatchThisDevice state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new PairThisDeviceModel(
            state.Paired,
            state.Paired ? DispatchCopy.ThisDevicePaired : DispatchCopy.ThisDeviceNotPaired,
            state.Capabilities);
    }
}
