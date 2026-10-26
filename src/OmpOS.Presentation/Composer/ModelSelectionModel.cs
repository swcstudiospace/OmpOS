using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Composer;

/// <summary>
/// The model and provider a composer shows, and the rule for changing the model. The label reads
/// <c>Provider name / model</c>; an attempt to switch to a model the provider cannot offer, or on a provider that cannot switch,
/// leaves the active model as it was and sets a notice. The model applies nothing to the engine: a host observes
/// <see cref="ActiveModelId"/> and forwards a successful switch.
/// </summary>
public sealed class ModelSelectionModel : ObservableModel
{
    private readonly IProviderCapabilitySource _capabilities;
    private string _activeModelId;
    private string? _notice;

    /// <summary>Creates the selection for a session that runs a model on a provider.</summary>
    /// <param name="capabilities">The source that answers what the provider supports.</param>
    /// <param name="providerId">The identifier of the session's provider.</param>
    /// <param name="modelId">The identifier of the session's active model.</param>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="providerId"/> or <paramref name="modelId"/> is null, empty or white space.</exception>
    public ModelSelectionModel(IProviderCapabilitySource capabilities, string providerId, string modelId)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        _capabilities = capabilities;
        ActiveProviderId = providerId;
        _activeModelId = modelId;
    }

    /// <summary>Gets the identifier of the session's provider.</summary>
    public string ActiveProviderId { get; }

    /// <summary>Gets the identifier of the active model.</summary>
    public string ActiveModelId
    {
        get => _activeModelId;
        private set
        {
            if (SetProperty(ref _activeModelId, value))
            {
                NotifyPropertyChanged(nameof(Label));
            }
        }
    }

    /// <summary>Gets the name the composer shows for the provider: its display name, or its identifier when the source does not know it.</summary>
    public string ActiveProviderName => _capabilities.Find(ActiveProviderId)?.DisplayName ?? ActiveProviderId;

    /// <summary>Gets the text the composer shows for the active selection, the provider name and the model identifier.</summary>
    public string Label => $"{ActiveProviderName} / {ActiveModelId}";

    /// <summary>Gets the models the active provider offers, or an empty list when the source does not know the provider.</summary>
    public IReadOnlyList<string> AvailableModels => _capabilities.Find(ActiveProviderId)?.Models ?? [];

    /// <summary>Gets the text that says why the last switch was refused, or <see langword="null"/> when it was accepted or none was tried.</summary>
    public string? Notice { get => _notice; private set => SetProperty(ref _notice, value); }

    /// <summary>
    /// Switches the active model on the active provider. The switch is refused, with <see cref="Notice"/> set and the active
    /// model unchanged, when the provider is unknown, cannot switch model, or does not offer the model. An accepted switch
    /// clears the notice.
    /// </summary>
    /// <param name="modelId">The identifier of the model to switch to. Matching is ordinal.</param>
    /// <returns><see langword="true"/> when the model is now active; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="modelId"/> is null, empty or white space.</exception>
    public bool TrySwitchModel(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        var provider = _capabilities.Find(ActiveProviderId);
        if (provider is null)
        {
            Notice = $"The model cannot be changed: provider '{ActiveProviderId}' is not known.";
            return false;
        }

        if (!provider.CanSwitchModel)
        {
            Notice = $"{provider.DisplayName} does not support switching the model in a session.";
            return false;
        }

        if (!provider.Models.Contains(modelId, StringComparer.Ordinal))
        {
            Notice = $"{provider.DisplayName} does not offer the model '{modelId}'.";
            return false;
        }

        Notice = null;
        ActiveModelId = modelId;
        return true;
    }
}
