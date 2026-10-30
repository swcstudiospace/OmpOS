namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>What the source read: the providers, the default model, the state of omp's own configuration and the provider templates.</summary>
/// <param name="Providers">Every provider to show, in the order the source lists them.</param>
/// <param name="DefaultModel">The default model selector (<c>provider/model</c>), or null when none is set.</param>
/// <param name="Warning">A warning to show above the list (for example omp's <c>models.yml</c> validation message), or null.</param>
/// <param name="IsComplete">False when omp lists only providers it has a credential for, so the list can be shorter than what omp supports.</param>
/// <param name="Templates">The templates a custom provider can start from.</param>
/// <param name="Listings">Every provider, aggregator, coding plan and local runtime the page offers to set up, in the order the source lists them. Null means none.</param>
/// <param name="KeyWritesDisabledReason">The sentence that says why the operator has disabled key entry, or null when keys can be entered. Cards then show the sentence and disable their key field.</param>
public sealed record ModelsSnapshot(
    IReadOnlyList<ProviderInfo> Providers,
    string? DefaultModel,
    string? Warning,
    bool IsComplete,
    IReadOnlyList<ProviderTemplate> Templates,
    IReadOnlyList<ProviderListing>? Listings = null,
    string? KeyWritesDisabledReason = null)
{
    /// <summary>Initializes a new instance of the <see cref="ModelsSnapshot"/> class with no provider templates.</summary>
    /// <param name="providers">Every provider to show, in the order the source lists them.</param>
    /// <param name="defaultModel">The default model selector (<c>provider/model</c>), or null when none is set.</param>
    /// <param name="warning">A warning to show above the list, or null.</param>
    /// <param name="isComplete">False when omp lists only providers it has a credential for.</param>
    public ModelsSnapshot(IReadOnlyList<ProviderInfo> providers, string? defaultModel, string? warning, bool isComplete)
        : this(providers, defaultModel, warning, isComplete, [])
    {
    }
}
