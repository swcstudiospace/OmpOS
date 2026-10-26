using System.Globalization;
using System.Runtime.CompilerServices;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The draft of one custom provider being added or edited. Every entry is checked inline by <see cref="CustomProviderRules"/>: an
/// entry's error shows once the user has changed that entry (or after <see cref="ShowAllErrors"/>), so a blank new form does not
/// open covered in errors. The draft is dirty while it differs from its starting values; it can be saved only while
/// <see cref="IsValid"/>. It holds no key: the key of a custom provider is added on the saved provider's card.
/// </summary>
public sealed class CustomProviderDraft : ObservableModel
{
    private readonly IReadOnlyList<string> _takenIds;
    private readonly Values _baseline;
    private Field _shown;
    private string _displayName;
    private string _id;
    private CustomProviderProtocol _protocol;
    private string _endpointText;
    private CustomProviderAuth _auth;
    private string _modelsText;
    private CustomProviderCapabilities _capabilities;
    private bool _allowRemotePlainHttp;

    /// <summary>Initializes a new instance of the <see cref="CustomProviderDraft"/> class.</summary>
    /// <param name="takenIds">The ids of every provider the new id must not equal (not the edited provider's own id).</param>
    /// <param name="editing">The saved provider being edited, or null for a new one.</param>
    internal CustomProviderDraft(IEnumerable<string> takenIds, CustomProviderForm? editing)
        : this(takenIds, editing, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomProviderDraft"/> class for a new provider that starts from a template. The
    /// template's values are the draft's starting values, so the draft is clean until the user changes an entry.
    /// </summary>
    /// <param name="takenIds">The ids of every provider the new id must not equal.</param>
    /// <param name="template">The template to start from.</param>
    internal CustomProviderDraft(IEnumerable<string> takenIds, ProviderTemplate template)
        : this(takenIds, null, template)
    {
    }

    private CustomProviderDraft(IEnumerable<string> takenIds, CustomProviderForm? editing, ProviderTemplate? template)
    {
        ArgumentNullException.ThrowIfNull(takenIds);
        _takenIds = [.. takenIds];
        EditingId = editing?.Id;

        _displayName = editing?.DisplayName ?? template?.DisplayName ?? string.Empty;
        _id = editing?.Id ?? (template is null ? string.Empty : UniqueId(template.Id, _takenIds));
        _protocol = editing?.Protocol ?? template?.Protocol ?? CustomProviderProtocol.OpenAiCompatible;
        _endpointText = editing?.BaseEndpoint.OriginalString ?? template?.BaseEndpoint?.OriginalString ?? string.Empty;
        _auth = editing?.Auth ?? template?.Auth ?? CustomProviderAuth.ApiKey;
        _modelsText = editing is null ? string.Empty : string.Join('\n', editing.Models);
        _capabilities = editing?.Capabilities ?? template?.Capabilities ?? CustomProviderCapabilities.Chat;
        _allowRemotePlainHttp = editing?.AllowRemotePlainHttp ?? false;
        TemplateNote = template is null ? null : ModelsCopy.TemplateNote(template.DisplayName, template.SourceUrl, template.RetrievedOn);
        TemplateRemark = template?.Note is { } remark && !string.IsNullOrWhiteSpace(remark) ? remark : null;
        _baseline = Current();
    }

    [Flags]
    private enum Field
    {
        None = 0,
        DisplayName = 1 << 0,
        Id = 1 << 1,
        Endpoint = 1 << 2,
        Models = 1 << 3,
        All = DisplayName | Id | Endpoint | Models,
    }

    /// <summary>Gets the id of the provider being edited, or null while a new provider is being added.</summary>
    public string? EditingId { get; }

    /// <summary>Gets a value indicating whether the draft edits a saved provider. Its id then cannot change.</summary>
    public bool IsEditing => EditingId is not null;

    /// <summary>Gets the note that says which template the form started from and where the template was verified, or null for a form that did not start from a template.</summary>
    public string? TemplateNote { get; }

    /// <summary>Gets the template's own advice (for example to start the server first), or null.</summary>
    public string? TemplateRemark { get; }

    /// <summary>Gets the heading of the form.</summary>
    public string Title => IsEditing ? ModelsCopy.DraftTitleEdit : ModelsCopy.DraftTitleAdd;

    /// <summary>Gets the hint under the endpoint entry for the chosen protocol.</summary>
    public string EndpointHint => ModelsCopy.EndpointHint(_protocol);

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => Edit(ref _displayName, value, Field.DisplayName);
    }

    /// <summary>Gets or sets the id slug. Ignored while <see cref="IsEditing"/>.</summary>
    public string Id
    {
        get => _id;
        set
        {
            if (!IsEditing)
            {
                Edit(ref _id, value, Field.Id);
            }
        }
    }

    /// <summary>Gets or sets the protocol.</summary>
    public CustomProviderProtocol Protocol
    {
        get => _protocol;
        set
        {
            if (SetProperty(ref _protocol, value))
            {
                NotifyPropertyChanged(nameof(EndpointHint));
                NotifyDerived();
            }
        }
    }

    /// <summary>Gets or sets the base endpoint as typed.</summary>
    public string EndpointText
    {
        get => _endpointText;
        set => Edit(ref _endpointText, value, Field.Endpoint);
    }

    /// <summary>Gets or sets how the endpoint authenticates.</summary>
    public CustomProviderAuth Auth
    {
        get => _auth;
        set
        {
            if (SetProperty(ref _auth, value))
            {
                NotifyDerived();
            }
        }
    }

    /// <summary>Gets or sets the model ids as typed, one per line.</summary>
    public string ModelsText
    {
        get => _modelsText;
        set => Edit(ref _modelsText, value, Field.Models);
    }

    /// <summary>Gets or sets the routes recorded as supported.</summary>
    public CustomProviderCapabilities Capabilities
    {
        get => _capabilities;
        set
        {
            if (SetProperty(ref _capabilities, value))
            {
                NotifyPropertyChanged(nameof(SupportsChat));
                NotifyPropertyChanged(nameof(SupportsResponses));
                NotifyPropertyChanged(nameof(SupportsEmbeddings));
                NotifyDerived();
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether chat completions are recorded as supported.</summary>
    public bool SupportsChat
    {
        get => _capabilities.HasFlag(CustomProviderCapabilities.Chat);
        set => SetCapability(CustomProviderCapabilities.Chat, value);
    }

    /// <summary>Gets or sets a value indicating whether the Responses route is recorded as supported.</summary>
    public bool SupportsResponses
    {
        get => _capabilities.HasFlag(CustomProviderCapabilities.Responses);
        set => SetCapability(CustomProviderCapabilities.Responses, value);
    }

    /// <summary>Gets or sets a value indicating whether embeddings are recorded as supported.</summary>
    public bool SupportsEmbeddings
    {
        get => _capabilities.HasFlag(CustomProviderCapabilities.Embeddings);
        set => SetCapability(CustomProviderCapabilities.Embeddings, value);
    }

    /// <summary>Gets or sets a value indicating whether plain http to a non-loopback host is explicitly allowed. Off by default.</summary>
    public bool AllowRemotePlainHttp
    {
        get => _allowRemotePlainHttp;
        set
        {
            if (SetProperty(ref _allowRemotePlainHttp, value))
            {
                NotifyDerived();
            }
        }
    }

    /// <summary>Gets the inline error of the display name, or null.</summary>
    public string? DisplayNameError => Shown(Field.DisplayName, CustomProviderRules.ValidateDisplayName(_displayName));

    /// <summary>Gets the inline error of the id, or null.</summary>
    public string? IdError => Shown(Field.Id, IsEditing ? null : CustomProviderRules.ValidateId(_id, _takenIds));

    /// <summary>Gets the inline error of the endpoint, or null.</summary>
    public string? EndpointError => Shown(Field.Endpoint, CustomProviderRules.ValidateEndpoint(_endpointText, _allowRemotePlainHttp, out _));

    /// <summary>Gets the inline error of the model list, or null.</summary>
    public string? ModelsError => Shown(Field.Models, CustomProviderRules.ValidateModels(_modelsText));

    /// <summary>Gets a value indicating whether every rule is satisfied, whether or not the errors are showing yet.</summary>
    public bool IsValid =>
        CustomProviderRules.ValidateDisplayName(_displayName) is null
        && (IsEditing || CustomProviderRules.ValidateId(_id, _takenIds) is null)
        && CustomProviderRules.ValidateEndpoint(_endpointText, _allowRemotePlainHttp, out _) is null
        && CustomProviderRules.ValidateModels(_modelsText) is null;

    /// <summary>Gets a value indicating whether an inline error is showing.</summary>
    public bool HasError =>
        DisplayNameError is not null || IdError is not null || EndpointError is not null || ModelsError is not null;

    /// <summary>Gets a value indicating whether the draft differs from its starting values.</summary>
    public bool IsDirty => Current() != _baseline;

    /// <summary>Makes every entry's error show, as when the user tries to save.</summary>
    public void ShowAllErrors()
    {
        _shown = Field.All;
        NotifyDerived();
    }

    /// <summary>Builds the saved form from the draft.</summary>
    /// <returns>The form, or null while <see cref="IsValid"/> is false.</returns>
    public CustomProviderForm? TryBuildForm()
    {
        if (!IsValid || CustomProviderRules.ValidateEndpoint(_endpointText, _allowRemotePlainHttp, out var endpoint) is not null || endpoint is null)
        {
            return null;
        }

        return new CustomProviderForm(
            _displayName.Trim(),
            (IsEditing ? EditingId : _id.Trim())!,
            _protocol,
            endpoint,
            _auth,
            CustomProviderRules.ParseModels(_modelsText),
            _capabilities,
            _allowRemotePlainHttp);
    }

    private static string UniqueId(string templateId, IReadOnlyList<string> takenIds)
    {
        var baseId = templateId.Trim();
        if (!IsTaken(baseId, takenIds))
        {
            return baseId;
        }

        for (var number = 2; ; number++)
        {
            var suffix = "-" + number.ToString(CultureInfo.InvariantCulture);
            var stem = baseId.Length + suffix.Length > CustomProviderRules.MaxIdLength
                ? baseId[..(CustomProviderRules.MaxIdLength - suffix.Length)].TrimEnd('-')
                : baseId;
            var candidate = stem + suffix;
            if (!IsTaken(candidate, takenIds))
            {
                return candidate;
            }
        }
    }

    private static bool IsTaken(string id, IReadOnlyList<string> takenIds) =>
        takenIds.Any(taken => string.Equals(taken, id, StringComparison.OrdinalIgnoreCase));

    private string? Shown(Field field, string? error) => _shown.HasFlag(field) ? error : null;

    private void Edit(ref string field, string? value, Field entry, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value ?? string.Empty, propertyName))
        {
            _shown |= entry;
            NotifyDerived();
        }
    }

    private void SetCapability(CustomProviderCapabilities flag, bool on) =>
        Capabilities = on ? _capabilities | flag : _capabilities & ~flag;

    private Values Current() =>
        new(_displayName, _id, _protocol, _endpointText, _auth, _modelsText, _capabilities, _allowRemotePlainHttp);

    private void NotifyDerived()
    {
        NotifyPropertyChanged(nameof(DisplayNameError));
        NotifyPropertyChanged(nameof(IdError));
        NotifyPropertyChanged(nameof(EndpointError));
        NotifyPropertyChanged(nameof(ModelsError));
        NotifyPropertyChanged(nameof(HasError));
        NotifyPropertyChanged(nameof(IsValid));
        NotifyPropertyChanged(nameof(IsDirty));
    }

    private sealed record Values(
        string DisplayName,
        string Id,
        CustomProviderProtocol Protocol,
        string Endpoint,
        CustomProviderAuth Auth,
        string Models,
        CustomProviderCapabilities Capabilities,
        bool AllowRemotePlainHttp);
}
