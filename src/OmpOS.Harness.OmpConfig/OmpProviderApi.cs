namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The wire protocol of a managed provider, serialised to the name omp's <c>models.yml</c> uses in the <c>api</c> member. The
/// values are the ones omp 18.12.6 accepts at provider level (<c>models-config-schema-bundle.ts</c>, <c>ApiSchema</c>).
/// </summary>
public enum OmpProviderApi
{
    /// <summary><c>openai-completions</c>: the OpenAI chat completions route (<c>/v1/chat/completions</c>).</summary>
    OpenAiCompletions = 0,

    /// <summary><c>openai-responses</c>: the OpenAI responses route (<c>/v1/responses</c>).</summary>
    OpenAiResponses = 1,

    /// <summary><c>anthropic-messages</c>: the Anthropic messages route.</summary>
    AnthropicMessages = 2,

    /// <summary><c>openai-codex-responses</c>: the Codex responses route.</summary>
    OpenAiCodexResponses = 3,

    /// <summary><c>azure-openai-responses</c>: the Azure OpenAI responses route.</summary>
    AzureOpenAiResponses = 4,

    /// <summary><c>bedrock-converse-stream</c>: the Amazon Bedrock Converse streaming route.</summary>
    BedrockConverseStream = 5,

    /// <summary><c>google-generative-ai</c>: the Gemini API (Google AI Studio) route.</summary>
    GoogleGenerativeAi = 6,

    /// <summary><c>google-gemini-cli</c>: the Gemini Code Assist route.</summary>
    GoogleGeminiCli = 7,

    /// <summary><c>google-vertex</c>: the Google Vertex AI route.</summary>
    GoogleVertex = 8,

    /// <summary><c>typesafe</c>: the Typesafe route.</summary>
    Typesafe = 9,

    /// <summary><c>openrouter-decisions</c>: the OpenRouter decisions (judge) route.</summary>
    OpenRouterDecisions = 10,

    /// <summary><c>openai-decisions</c>: the OpenAI decisions (judge) route.</summary>
    OpenAiDecisions = 11,

    /// <summary><c>workers-ai-systemone</c>: the Cloudflare Workers AI SystemOne route.</summary>
    WorkersAiSystemOne = 12,
}
