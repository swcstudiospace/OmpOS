namespace Autonogrammer.Harness.Completion;

/// <summary>
/// Applies a completion decision to an editor buffer. Both operations are pure: they return a new buffer and never
/// change their input.
/// </summary>
public static class CompletionAcceptance
{
    /// <summary>
    /// Accepts a suggestion: inserts its text at the caret and moves the caret to the end of the inserted text.
    /// </summary>
    /// <param name="buffer">The buffer the suggestion was shown at.</param>
    /// <param name="result">The completion result. Must have status <see cref="CompletionStatus.Suggested"/>.</param>
    /// <returns>A buffer with the suggestion inserted at the caret.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> or <paramref name="result"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> carries no suggestion.</exception>
    public static CaretBuffer Accept(CaretBuffer buffer, CompletionResult result)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status != CompletionStatus.Suggested)
        {
            throw new InvalidOperationException($"Only a suggested completion can be accepted; the result is {result.Status}.");
        }

        var text = buffer.Text.Insert(buffer.Caret, result.Text);
        return new CaretBuffer(text, buffer.Caret + result.Text.Length);
    }

    /// <summary>Dismisses a suggestion. The buffer is returned unchanged: dismissing inserts nothing.</summary>
    /// <param name="buffer">The buffer the suggestion was shown at.</param>
    /// <returns>The same buffer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
    public static CaretBuffer Dismiss(CaretBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return buffer;
    }
}
