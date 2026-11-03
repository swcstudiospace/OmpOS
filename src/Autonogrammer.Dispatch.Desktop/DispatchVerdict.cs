namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The verifier's answer for one command frame.</summary>
/// <param name="Accepted">True when the signature, identity, expiry and payload all check out.</param>
/// <param name="Refusal">The first failed check when <paramref name="Accepted"/> is false; null otherwise.</param>
public readonly record struct DispatchVerdict(bool Accepted, DispatchRefusal? Refusal)
{
    /// <summary>The accepted verdict.</summary>
    public static DispatchVerdict Accept { get; } = new(true, null);

    /// <summary>Builds a refused verdict.</summary>
    /// <param name="refusal">The failed check.</param>
    /// <returns>The refused verdict.</returns>
    public static DispatchVerdict Refuse(DispatchRefusal refusal) => new(false, refusal);
}
