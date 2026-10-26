using OmpOS.Harness.OmpConfig;
using OmpOS.Harness.Providers;
using OmpOS.Presentation.Settings.Models;

namespace OmpOS.Native.Settings.Models;

/// <summary>
/// Maps a custom provider between its three shapes: the form of the Models page, the provider definition of the registry (the rules
/// and the details omp's file cannot hold: display name and routes) and the block OmpOS writes to omp's <c>models.yml</c> (what omp
/// reads). The block holds everything omp needs and the registry entry holds everything else, so a provider read back for editing
/// shows the values the user entered.
/// </summary>
internal static class CustomProviderMapper
{
    /// <summary>Builds the registry definition of a form, so the registry's rules can judge it.</summary>
    /// <param name="form">The form.</param>
    /// <returns>The definition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="form"/> is null.</exception>
    public static CustomProviderDefinition ToDefinition(CustomProviderForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return new CustomProviderDefinition(
            form.Id,
            form.DisplayName,
            form.Protocol == CustomProviderProtocol.AnthropicCompatible ? ProviderTransport.AnthropicCompatible : ProviderTransport.OpenAiCompatible,
            form.Auth switch
            {
                CustomProviderAuth.None => ProviderAuthType.None,
                CustomProviderAuth.Bearer => ProviderAuthType.BearerToken,
                _ => ProviderAuthType.ApiKey,
            },
            AuthHeaderName: null,
            form.BaseEndpoint,
            [.. form.Models],
            ToCapabilities(form.Capabilities),
            form.AllowRemotePlainHttp);
    }

    /// <summary>Returns the first rule a definition breaks, worded for the user.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The message, or null when the definition is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    public static string? FirstProblem(CustomProviderDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var result = definition.Validate();
        return result.IsValid ? null : Describe(result.Errors[0]);
    }

    /// <summary>Builds the block that is written to <c>models.yml</c>.</summary>
    /// <param name="form">The form.</param>
    /// <param name="keyVariable">The name of the environment variable that holds the key, used unless the form needs no authentication.</param>
    /// <returns>The block.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static OmpManagedProvider ToManaged(CustomProviderForm form, string keyVariable)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(keyVariable);
        var responsesOnly = form.Capabilities.HasFlag(CustomProviderCapabilities.Responses) && !form.Capabilities.HasFlag(CustomProviderCapabilities.Chat);
        var api = form.Protocol == CustomProviderProtocol.AnthropicCompatible
            ? OmpProviderApi.AnthropicMessages
            : responsesOnly ? OmpProviderApi.OpenAiResponses : OmpProviderApi.OpenAiCompletions;
        var keyless = form.Auth == CustomProviderAuth.None;
        return new OmpManagedProvider(
            form.Id,
            form.BaseEndpoint,
            api,
            keyless ? null : keyVariable,
            keyless,
            [.. form.Models.Select(static id => new OmpManagedModel(id))]);
    }

    /// <summary>
    /// Builds the form of a provider read from <c>models.yml</c>. What omp reads (endpoint, protocol, models, whether a key is used) comes
    /// from the block; the display name, the routes and the Bearer choice come from the registry entry when it describes the same provider,
    /// and are derived from the block otherwise.
    /// </summary>
    /// <param name="block">The block read from <c>models.yml</c>.</param>
    /// <param name="details">The registry entry with the same id, or null when there is none.</param>
    /// <returns>The form, which the page can edit and save again.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is null.</exception>
    public static CustomProviderForm ToForm(OmpManagedProvider block, CustomProviderDefinition? details)
    {
        ArgumentNullException.ThrowIfNull(block);
        var known = details is not null && string.Equals(details.Id, block.Id, StringComparison.Ordinal) ? details : null;
        var auth = block.NoAuthentication
            ? CustomProviderAuth.None
            : known?.AuthType == ProviderAuthType.BearerToken ? CustomProviderAuth.Bearer : CustomProviderAuth.ApiKey;
        var capabilities = known is not null && known.Capabilities != ProviderCapabilities.None
            ? ToCapabilities(known.Capabilities)
            : block.Api == OmpProviderApi.OpenAiResponses ? CustomProviderCapabilities.Responses : CustomProviderCapabilities.Chat;
        var plainHttpToRemote = string.Equals(block.BaseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !block.BaseUrl.IsLoopback;
        return new CustomProviderForm(
            known is not null && !string.IsNullOrWhiteSpace(known.DisplayName) ? known.DisplayName : block.Id,
            block.Id,
            block.Api == OmpProviderApi.AnthropicMessages ? CustomProviderProtocol.AnthropicCompatible : CustomProviderProtocol.OpenAiCompatible,
            block.BaseUrl,
            auth,
            [.. block.Models.Select(static model => model.Id)],
            capabilities,
            plainHttpToRemote);
    }

    private static ProviderCapabilities ToCapabilities(CustomProviderCapabilities capabilities)
    {
        var result = ProviderCapabilities.None;
        if (capabilities.HasFlag(CustomProviderCapabilities.Chat))
        {
            result |= ProviderCapabilities.ChatCompletions;
        }

        if (capabilities.HasFlag(CustomProviderCapabilities.Responses))
        {
            result |= ProviderCapabilities.Responses;
        }

        if (capabilities.HasFlag(CustomProviderCapabilities.Embeddings))
        {
            result |= ProviderCapabilities.Embeddings;
        }

        return result;
    }

    private static CustomProviderCapabilities ToCapabilities(ProviderCapabilities capabilities)
    {
        var result = CustomProviderCapabilities.None;
        if (capabilities.HasFlag(ProviderCapabilities.ChatCompletions))
        {
            result |= CustomProviderCapabilities.Chat;
        }

        if (capabilities.HasFlag(ProviderCapabilities.Responses))
        {
            result |= CustomProviderCapabilities.Responses;
        }

        if (capabilities.HasFlag(ProviderCapabilities.Embeddings))
        {
            result |= CustomProviderCapabilities.Embeddings;
        }

        return result;
    }

    private static string Describe(ProviderValidationError error) => error switch
    {
        ProviderValidationError.InvalidId => ModelsCopy.ErrIdInvalid,
        ProviderValidationError.MissingBaseEndpoint => ModelsCopy.ErrEndpointRequired,
        ProviderValidationError.BaseEndpointNotAbsolute => ModelsCopy.ErrEndpointNotAbsolute,
        ProviderValidationError.UnsupportedScheme => ModelsCopy.ErrEndpointUnsupportedScheme,
        ProviderValidationError.PlainHttpToRemoteHost => ModelsCopy.ErrEndpointPlainHttpRemote,
        ProviderValidationError.CredentialInEndpoint => ModelsCopy.ErrEndpointCredential,
        ProviderValidationError.MissingDisplayName => ModelsCopy.ErrDisplayNameRequired,
        ProviderValidationError.InvalidModelId => ModelsCopy.ErrModelInvalid,
        ProviderValidationError.DuplicateModelId => ModelsCopy.ErrModelDuplicate,
        ProviderValidationError.IdAlreadyRegistered => ModelsCopy.ErrIdTaken,
        _ => "The provider definition is not valid.",
    };
}
