namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The content a tool returned, and whether the host or the provider treats it as an error.</summary>
/// <param name="Content">The blocks, in order.</param>
/// <param name="IsError">Whether the result is a failure the model should read as an error.</param>
public sealed record CoworkToolResult(IReadOnlyList<CoworkContentBlock> Content, bool IsError)
{
    /// <summary>Builds a single text result.</summary>
    /// <param name="text">The text.</param>
    /// <param name="isUntrusted">Whether the text is untrusted.</param>
    /// <returns>A successful text result.</returns>
    public static CoworkToolResult Text(string text, bool isUntrusted = false) =>
        new([new CoworkTextBlock(text, isUntrusted)], false);

    /// <summary>Builds a failure the model can read. The message is fixed text, never an exception message.</summary>
    /// <param name="message">The fixed, secret-free message.</param>
    /// <returns>An error result holding that message.</returns>
    public static CoworkToolResult Failure(string message) =>
        new([new CoworkTextBlock(message, false)], true);
}
