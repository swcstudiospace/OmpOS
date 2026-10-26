namespace OmpOS.Harness.OmpConfig;

/// <summary>One model of a managed provider, as written to <c>models.yml</c>.</summary>
/// <param name="Id">The model id the endpoint expects. At most 200 characters, no whitespace or control characters.</param>
/// <param name="Name">The display name, or null to let omp show the id.</param>
/// <param name="Reasoning">True when the model supports reasoning.</param>
/// <param name="AcceptsImages">True when the model accepts image input besides text.</param>
/// <param name="ContextWindow">The context window in tokens, or null to let omp decide.</param>
/// <param name="MaxTokens">The most output tokens, or null to let omp decide.</param>
public sealed record OmpManagedModel(
    string Id,
    string? Name = null,
    bool Reasoning = false,
    bool AcceptsImages = false,
    int? ContextWindow = null,
    int? MaxTokens = null)
{
    /// <summary>The longest model id accepted. It equals the limit of the provider definition in the settings.</summary>
    public const int MaxIdLength = 200;
}
