namespace Autonogrammer.Parser;

/// <summary>
/// One ordered recognizer in the catch-all prompt parser. Implementations are
/// registered through DI (<c>AddOmpParser</c>) and run in ascending <see cref="Order"/>.
/// </summary>
public interface IPromptRecognizer
{
    /// <summary>Gets the ordering key; lower values run first.</summary>
    int Order { get; }

    /// <summary>Attempts to recognize the prompt.</summary>
    /// <param name="input">The normalized prompt.</param>
    /// <returns>A result when this recognizer claims the prompt; otherwise <see langword="null"/>.</returns>
    RecognizerResult? TryRecognize(PromptInput input);
}
