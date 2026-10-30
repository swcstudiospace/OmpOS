using Autonogrammer.Api.Options;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Renders a prompt for log output: verbatim only when <c>Autonogrammer:Debug:LogPrompts</c> is true,
/// otherwise a length-only <c>&lt;redacted&gt;</c> marker. User text is never concatenated
/// into control or log messages through any other path.
/// </summary>
internal static class PromptRedactor
{
    /// <summary>Formats a prompt for safe logging.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="logPrompts">Whether verbatim prompt logging is enabled.</param>
    /// <returns>The prompt verbatim, or a length-only redaction marker.</returns>
    public static string Format(string prompt, bool logPrompts)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return logPrompts ? prompt : $"<redacted len={prompt.Length}>";
    }

    /// <summary>Formats a prompt for safe logging using the bound debug options.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <param name="options">The debug options.</param>
    /// <returns>The prompt verbatim, or a length-only redaction marker.</returns>
    public static string Format(string prompt, DebugOptions options)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        return Format(prompt, options.LogPrompts);
    }
}
