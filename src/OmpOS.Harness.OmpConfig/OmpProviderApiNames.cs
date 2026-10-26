namespace OmpOS.Harness.OmpConfig;

/// <summary>Maps <see cref="OmpProviderApi"/> to and from the names omp writes in <c>models.yml</c>.</summary>
internal static class OmpProviderApiNames
{
    /// <summary>Returns the omp name of <paramref name="api"/>.</summary>
    /// <param name="api">The protocol.</param>
    /// <returns>The name, or null when <paramref name="api"/> is not a defined value.</returns>
    public static string? ToYaml(OmpProviderApi api) => api switch
    {
        OmpProviderApi.OpenAiCompletions => "openai-completions",
        OmpProviderApi.OpenAiResponses => "openai-responses",
        OmpProviderApi.AnthropicMessages => "anthropic-messages",
        _ => null,
    };

    /// <summary>Parses an omp name.</summary>
    /// <param name="name">The name as written in <c>models.yml</c>.</param>
    /// <param name="api">The protocol when the name is known.</param>
    /// <returns>True when the name is one of the three supported protocols.</returns>
    public static bool TryParse(string name, out OmpProviderApi api)
    {
        switch (name)
        {
            case "openai-completions":
                api = OmpProviderApi.OpenAiCompletions;
                return true;
            case "openai-responses":
                api = OmpProviderApi.OpenAiResponses;
                return true;
            case "anthropic-messages":
                api = OmpProviderApi.AnthropicMessages;
                return true;
            default:
                api = default;
                return false;
        }
    }
}
