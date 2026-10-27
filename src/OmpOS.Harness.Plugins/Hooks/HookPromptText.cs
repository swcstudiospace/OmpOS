namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Builds the text a prompt hook sends to the model: <c>$ARGUMENTS</c> is replaced by the hook input JSON, and the input is
/// appended when the placeholder is absent (https://cursor.com/docs/hooks, "Prompt-Based Hooks").
/// </summary>
public static class HookPromptText
{
    private const string Placeholder = "$ARGUMENTS";

    /// <summary>
    /// Substitutes the input into a prompt.
    /// </summary>
    /// <param name="prompt">The hook's prompt.</param>
    /// <param name="inputJson">The hook input JSON.</param>
    /// <returns>The prompt to send.</returns>
    public static string Build(string prompt, string inputJson)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(inputJson);
        return prompt.Contains(Placeholder, StringComparison.Ordinal)
            ? prompt.Replace(Placeholder, inputJson, StringComparison.Ordinal)
            : prompt + "\n\n" + inputJson;
    }
}
