namespace Autonogrammer.Harness.Completion;

/// <summary>
/// An editor's text together with the caret position within it.
/// </summary>
/// <remarks>
/// <see cref="Caret"/> is a UTF-16 code-unit index from 0 to <see cref="Text"/> length, inclusive. A caret never falls
/// between the two halves of a surrogate pair, so inserting at it cannot produce malformed text.
/// </remarks>
public sealed record CaretBuffer
{
    /// <summary>Creates a buffer.</summary>
    /// <param name="text">The text. Not null.</param>
    /// <param name="caret">The caret index, from 0 to the text length.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="caret"/> is outside the text.</exception>
    /// <exception cref="ArgumentException"><paramref name="caret"/> falls between the two halves of a surrogate pair.</exception>
    public CaretBuffer(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(caret);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caret, text.Length);

        if (caret > 0 && caret < text.Length && char.IsHighSurrogate(text[caret - 1]) && char.IsLowSurrogate(text[caret]))
        {
            throw new ArgumentException("The caret cannot fall inside a surrogate pair.", nameof(caret));
        }

        Text = text;
        Caret = caret;
    }

    /// <summary>Gets the text.</summary>
    public string Text { get; }

    /// <summary>Gets the caret index, a UTF-16 code-unit offset into <see cref="Text"/>.</summary>
    public int Caret { get; }
}
