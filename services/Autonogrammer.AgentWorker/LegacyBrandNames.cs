namespace Autonogrammer.AgentWorker;

/// <summary>
/// Token identifiers from before the rename. The control plane and the deployed worker keep them until both have switched together
/// (owner decision, 2026-11-03). The literal guard allows old-name literals only in files named LegacyBrandNames.cs.
/// </summary>
internal static class LegacyBrandNames
{
    /// <summary>The issuer the worker expects and the control plane signs with.</summary>
    public const string WorkerIssuer = "ompos-api";

    /// <summary>The audience every worker token is issued for.</summary>
    public const string WorkerAudience = "ompos-worker";
}
