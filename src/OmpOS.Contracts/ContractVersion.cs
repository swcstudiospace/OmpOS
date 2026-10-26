namespace OmpOS.Contracts;

/// <summary>
/// The current contract version, negotiated over a stable header name.
/// </summary>
public static class ContractVersion
{
    /// <summary>The current contract version (semver).</summary>
    public const string Current = "1.2.0";

    /// <summary>The header carrying the contract version.</summary>
    public const string HeaderName = "X-Ompos-Contract-Version";
}
