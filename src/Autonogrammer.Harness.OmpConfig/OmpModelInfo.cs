namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// One model row of <c>omp models --json</c>. omp 18.12.6 rows carry no <c>api</c> member: the transport of a model is not
/// reported by the listing.
/// </summary>
/// <param name="ProviderId">The provider that serves the model, for example <c>openai</c>.</param>
/// <param name="ModelId">The model id within the provider.</param>
/// <param name="Selector">The <c>provider/model-id</c> selector omp accepts for roles and for the ACP model option.</param>
/// <param name="DisplayName">The human-readable name, or the model id when omp reports none.</param>
/// <param name="Kind">The catalog kind, for example <c>chat</c>.</param>
/// <param name="ContextWindow">The context window in tokens, or null when omp reports none.</param>
/// <param name="MaxTokens">The most output tokens, or null when omp reports none.</param>
/// <param name="Reasoning">True when the model supports reasoning.</param>
/// <param name="ThinkingLevels">The thinking levels the model accepts, empty when it has none.</param>
/// <param name="InputKinds">The input kinds the model accepts, for example <c>text</c> and <c>image</c>.</param>
/// <param name="Cost">The reported price, or null when omp reports none.</param>
/// <param name="PricingStatus">How omp classifies the price, for example <c>fixed</c> or <c>unknown</c>, or null.</param>
public sealed record OmpModelInfo(
    string ProviderId,
    string ModelId,
    string Selector,
    string DisplayName,
    string Kind,
    int? ContextWindow,
    int? MaxTokens,
    bool Reasoning,
    IReadOnlyList<string> ThinkingLevels,
    IReadOnlyList<string> InputKinds,
    OmpModelCost? Cost,
    string? PricingStatus)
{
    /// <summary>Gets a value indicating whether the model accepts image input.</summary>
    public bool AcceptsImages => InputKinds.Contains("image", StringComparer.Ordinal);
}
