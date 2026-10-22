namespace OmpOS.Harness.Completion;

/// <summary>
/// The result of one completion request: its status, and the suggestion text when there is one.
/// </summary>
public sealed record CompletionResult
{
    private CompletionResult(CompletionStatus status, string text)
    {
        Status = status;
        Text = text;
    }

    /// <summary>Gets the result for a request made with no provider configured.</summary>
    public static CompletionResult Disabled { get; } = new(CompletionStatus.Disabled, string.Empty);

    /// <summary>Gets the result for a provider that answered without a suggestion.</summary>
    public static CompletionResult NoSuggestion { get; } = new(CompletionStatus.NoSuggestion, string.Empty);

    /// <summary>Creates the result for a provider suggestion.</summary>
    /// <param name="text">The suggested text. Not null or empty.</param>
    /// <returns>A result with status <see cref="CompletionStatus.Suggested"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null or empty.</exception>
    public static CompletionResult Suggested(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        return new CompletionResult(CompletionStatus.Suggested, text);
    }

    /// <summary>Gets the status of the request.</summary>
    public CompletionStatus Status { get; }

    /// <summary>Gets the suggestion text. Empty unless <see cref="Status"/> is <see cref="CompletionStatus.Suggested"/>.</summary>
    public string Text { get; }
}
