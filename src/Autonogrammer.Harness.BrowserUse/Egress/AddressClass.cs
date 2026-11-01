namespace Autonogrammer.Harness.BrowserUse.Egress;

/// <summary>How an address was classified. Ambiguous literals are refused.</summary>
public enum AddressClass
{
    /// <summary>The address may be dialed.</summary>
    Allowed = 0,

    /// <summary>The address is in a denied range.</summary>
    Denied = 1,

    /// <summary>The literal has more than one reading and must not be dialed.</summary>
    Ambiguous = 2,
}
