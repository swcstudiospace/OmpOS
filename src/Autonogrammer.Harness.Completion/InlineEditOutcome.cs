namespace Autonogrammer.Harness.Completion;

/// <summary>The outcome of one inline edit request.</summary>
public enum InlineEditOutcome
{
    /// <summary>No provider is configured. No request was sent.</summary>
    Disabled,

    /// <summary>The provider answered without a proposal.</summary>
    NoProposal,

    /// <summary>The provider answered with a replacement for the selected range.</summary>
    Proposed,
}
