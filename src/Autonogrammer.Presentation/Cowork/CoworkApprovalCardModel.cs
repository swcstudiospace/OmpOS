using Autonogrammer.Contracts.Controls;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>One approval card. Views render <see cref="Target"/> and <see cref="Details"/> only.</summary>
public sealed class CoworkApprovalCardModel
{
    /// <summary>Creates a card.</summary>
    /// <param name="approval">The contract record.</param>
    /// <param name="target">The host-observed target.</param>
    /// <param name="details">The host-observed details.</param>
    /// <param name="ignoredModelText">Model text that views must not render.</param>
    public CoworkApprovalCardModel(PendingToolApproval approval, string target, string details, string? ignoredModelText)
    {
        Approval = approval;
        Target = target;
        Details = details;
        IgnoredModelText = ignoredModelText;
    }

    /// <summary>Gets the contract record.</summary>
    public PendingToolApproval Approval { get; }

    /// <summary>Gets the host-observed target. This is the instruction a card shows.</summary>
    public string Target { get; }

    /// <summary>Gets the host-observed details.</summary>
    public string Details { get; }

    /// <summary>Gets model text that must not be rendered as an instruction.</summary>
    public string? IgnoredModelText { get; }

    /// <summary>Gets the accessible name of the card.</summary>
    public string AccessibleName => "Approval " + Approval.ToolName;
}
