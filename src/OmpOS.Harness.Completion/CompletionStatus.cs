namespace OmpOS.Harness.Completion;

/// <summary>The outcome of one completion request.</summary>
public enum CompletionStatus
{
    /// <summary>No provider is configured. No request was sent.</summary>
    Disabled,

    /// <summary>The provider answered without a suggestion.</summary>
    NoSuggestion,

    /// <summary>The provider answered with a suggestion to show at the caret.</summary>
    Suggested,
}
