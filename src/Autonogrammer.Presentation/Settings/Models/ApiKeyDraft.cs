using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// The masked entry for one provider's API key. The typed key lives only in <see cref="Entry"/>; the card takes it once to hand to
/// the source and clears it whether the source succeeds or fails. The key never appears in <see cref="ToString"/>, in a
/// notification argument (they carry property names only), in <see cref="Error"/> or in an announcement; a head shows
/// <see cref="MaskedPreview"/> and binds the field's text to <see cref="Entry"/> with a password mask.
/// </summary>
public sealed class ApiKeyDraft : ObservableModel
{
    private const int MaxPreviewLength = 12;

    private string _entry = string.Empty;
    private bool _isSaving;

    /// <summary>Initializes a new instance of the <see cref="ApiKeyDraft"/> class.</summary>
    /// <param name="providerId">The provider the key is for.</param>
    /// <param name="providerName">The provider's display name.</param>
    /// <param name="isAutonogrammerManaged">True when Autonogrammer wrote the provider to models.yml, so Autonogrammer keeps its key; false when omp keeps it.</param>
    internal ApiKeyDraft(string providerId, string providerName, bool isAutonogrammerManaged)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(providerName);
        ProviderId = providerId;
        ProviderName = providerName;
        KeyStorageNote = ModelsCopy.KeyStorageNote(isAutonogrammerManaged);
    }

    /// <summary>Gets the provider the key is for.</summary>
    public string ProviderId { get; }

    /// <summary>Gets the provider's display name.</summary>
    public string ProviderName { get; }

    /// <summary>Gets the sentence that says where this provider's key is kept: omp's credential database, or Autonogrammer's own key store for a provider Autonogrammer wrote.</summary>
    public string KeyStorageNote { get; }

    /// <summary>Gets the accessible name of the key field.</summary>
    public string FieldLabel => ModelsCopy.ApiKeyFieldLabel(ProviderName);

    /// <summary>
    /// Gets or sets the typed key, the only place it lives. A head binds the masked field to it. It is empty after every save
    /// attempt, successful or not, and after <see cref="Clear"/>.
    /// </summary>
    public string Entry
    {
        get => _entry;
        set
        {
            if (SetProperty(ref _entry, value ?? string.Empty))
            {
                NotifyEntryDerived();
            }
        }
    }

    /// <summary>Gets one bullet per typed character, up to a short limit, so the field can show that something is typed without the key.</summary>
    public string MaskedPreview => new('\u2022', Math.Min(_entry.Length, MaxPreviewLength));

    /// <summary>Gets a value indicating whether anything is typed.</summary>
    public bool HasEntry => _entry.Length > 0;

    /// <summary>Gets the inline problem with the typed entry, worded without quoting it, or null.</summary>
    public string? Error => CustomProviderRules.ValidateApiKey(_entry);

    /// <summary>Gets a value indicating whether the entry can be saved: something is typed and nothing is wrong with it.</summary>
    public bool IsValid => _entry.Trim().Length > 0 && Error is null;

    /// <summary>Gets a value indicating whether a save is in progress.</summary>
    public bool IsSaving
    {
        get => _isSaving;
        internal set => SetProperty(ref _isSaving, value);
    }

    /// <summary>Clears the typed key.</summary>
    public void Clear() => Entry = string.Empty;

    /// <summary>Describes the draft without the key.</summary>
    /// <returns>The provider and whether anything is typed.</returns>
    public override string ToString() => $"ApiKeyDraft({ProviderId}, {(HasEntry ? "entry present" : "empty")})";

    /// <summary>Returns the trimmed key for the single hand-over to the source.</summary>
    /// <returns>The key.</returns>
    internal string TakeKey() => _entry.Trim();

    private void NotifyEntryDerived()
    {
        NotifyPropertyChanged(nameof(MaskedPreview));
        NotifyPropertyChanged(nameof(HasEntry));
        NotifyPropertyChanged(nameof(Error));
        NotifyPropertyChanged(nameof(IsValid));
    }
}
