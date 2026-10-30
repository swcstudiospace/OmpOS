namespace Autonogrammer.Harness.Providers;

/// <summary>
/// A provider the user defines at run time: the endpoint, how it authenticates, which models it serves and which routes it
/// documents. It becomes a registry entry without any code change. It holds no key material. Equality of the model list is
/// by reference; compare <see cref="Models"/> element by element.
/// </summary>
/// <param name="Id">The slug that names the provider. It must not collide with a catalogue entry or another custom provider.</param>
/// <param name="DisplayName">The name the settings view shows.</param>
/// <param name="Transport">The protocol the endpoint speaks.</param>
/// <param name="AuthType">The credential scheme the endpoint expects.</param>
/// <param name="AuthHeaderName">The header that carries the credential, or null for the scheme's usual header.</param>
/// <param name="BaseEndpoint">The base endpoint. Plain http is accepted to loopback only unless <paramref name="AllowRemotePlainHttp"/> is set.</param>
/// <param name="Models">The model ids the provider serves, in the order the user listed them.</param>
/// <param name="Capabilities">The routes the user records as supported.</param>
/// <param name="AllowRemotePlainHttp">True when the user explicitly accepts plain http to a non-loopback host.</param>
public sealed record CustomProviderDefinition(
    string Id,
    string DisplayName,
    ProviderTransport Transport,
    ProviderAuthType AuthType,
    string? AuthHeaderName,
    Uri? BaseEndpoint,
    IReadOnlyList<string> Models,
    ProviderCapabilities Capabilities,
    bool AllowRemotePlainHttp = false)
{
    /// <summary>The longest model id a definition may list.</summary>
    public const int MaxModelIdLength = 200;

    /// <summary>
    /// Builds the registry entry for this definition. The entry has kind <see cref="ProviderKind.Custom"/> and cites the
    /// verified generic pattern of its transport as its source, so it carries a source and a date like every catalogue row.
    /// The entry is built even when the definition is invalid; check <see cref="Validate"/> before using it.
    /// </summary>
    public ProviderEntry ToEntry()
    {
        var pattern = Transport == ProviderTransport.AnthropicCompatible
            ? ProviderCatalogue.GenericAnthropicCompatible
            : ProviderCatalogue.GenericOpenAiCompatible;
        return new ProviderEntry(
            Id,
            ProviderKind.Custom,
            Transport,
            AuthType,
            BaseEndpoint,
            Capabilities,
            pattern.SourceUrl,
            pattern.RetrievedOn,
            AllowRemotePlainHttp,
            AuthHeaderName);
    }

    /// <summary>
    /// Returns every rule the definition breaks, in the order the checks run: display name, then the entry rules (id,
    /// endpoint, header name), then the model list. A valid definition yields an empty error list.
    /// </summary>
    public ProviderValidationResult Validate()
    {
        var errors = new List<ProviderValidationError>();

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            errors.Add(ProviderValidationError.MissingDisplayName);
        }

        errors.AddRange(ProviderEntryValidator.Validate(ToEntry()).Errors);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var reportedInvalid = false;
        var reportedDuplicate = false;
        foreach (var model in Models)
        {
            if (!IsValidModelId(model))
            {
                if (!reportedInvalid)
                {
                    errors.Add(ProviderValidationError.InvalidModelId);
                    reportedInvalid = true;
                }
            }
            else if (!seen.Add(model) && !reportedDuplicate)
            {
                errors.Add(ProviderValidationError.DuplicateModelId);
                reportedDuplicate = true;
            }
        }

        return new ProviderValidationResult(errors);
    }

    private static bool IsValidModelId(string? model)
    {
        if (string.IsNullOrEmpty(model) || model.Length > MaxModelIdLength)
        {
            return false;
        }

        foreach (var character in model)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }
}
