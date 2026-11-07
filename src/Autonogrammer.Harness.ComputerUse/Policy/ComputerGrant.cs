using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>One grant. Key fields are session, application, tier, origin, expiry and auto-approve typing.</summary>
public sealed record ComputerGrant
{
    /// <summary>Grant id.</summary>
    public required string GrantId { get; init; }

    /// <summary>Session id.</summary>
    public required string SessionId { get; init; }

    /// <summary>Application identity.</summary>
    public required string AppIdentity { get; init; }

    /// <summary>Tier.</summary>
    public required ComputerGrantTier Tier { get; init; }

    /// <summary>Origin.</summary>
    public required ComputerActionOrigin Origin { get; init; }

    /// <summary>Expiry from the injected clock.</summary>
    public required DateTimeOffset Expiry { get; init; }

    /// <summary>Whether typing is auto-approved. Dispatch grants never set this.</summary>
    public required bool AutoApproveTyping { get; init; }

    /// <summary>Projects the L1 view.</summary>
    /// <returns>The view.</returns>
    public ComputerGrantView ToView() => new()
    {
        GrantId = GrantId,
        SessionId = SessionId,
        AppIdentity = AppIdentity,
        Tier = Tier,
        Origin = Origin,
        Expiry = Expiry,
        AutoApproveTyping = AutoApproveTyping,
    };
}
