namespace Autonogrammer.Presentation.Settings.Permissions;

/// <summary>The fixed sentences on the Permissions page.</summary>
public sealed class PermissionStatementModel
{
    /// <summary>The Auto label.</summary>
    public const string AutoRules = "Auto (rules)";

    /// <summary>Network copy owned by isolation. Shown here so the page does not invent a second sentence.</summary>
    public const string NetworkUnrestricted = "network: unrestricted";

    /// <summary>Deletion protection is on in every mode.</summary>
    public const string DeleteProtectionAlwaysAsks = "delete protection always asks";

    /// <summary>A revoked grant does not change the session already running.</summary>
    public const string RevokedGrantsApplyNextSession = "revoked grants apply next session";

    /// <summary>Gets the sentences in page order.</summary>
    public IReadOnlyList<string> Statements { get; } =
    [
        AutoRules,
        NetworkUnrestricted,
        DeleteProtectionAlwaysAsks,
        RevokedGrantsApplyNextSession,
    ];
}
