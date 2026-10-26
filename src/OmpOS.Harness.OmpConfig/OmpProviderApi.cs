namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The wire protocol of a managed provider, serialised to the name omp's <c>models.yml</c> uses in the <c>api</c> member.
/// </summary>
public enum OmpProviderApi
{
    /// <summary><c>openai-completions</c>: the OpenAI chat completions route (<c>/v1/chat/completions</c>).</summary>
    OpenAiCompletions = 0,

    /// <summary><c>openai-responses</c>: the OpenAI responses route (<c>/v1/responses</c>).</summary>
    OpenAiResponses = 1,

    /// <summary><c>anthropic-messages</c>: the Anthropic messages route.</summary>
    AnthropicMessages = 2,
}
