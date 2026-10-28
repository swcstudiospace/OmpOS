using System.Globalization;

namespace Autonogrammer.Presentation.Settings.Models;

/// <summary>
/// The words of the Models page. Heads show these strings and never type their own, so the wording is checked in one place. No
/// member takes a key as an argument.
/// </summary>
public static class ModelsCopy
{
    /// <summary>The navigation label of the page.</summary>
    public const string PageTitle = "Models";

    /// <summary>The sentence under the title of the page.</summary>
    public const string PageDescription = "The model providers omp uses, their models and their keys.";

    /// <summary>The accessible name of the search box.</summary>
    public const string SearchLabel = "Search providers and models";

    /// <summary>The hint shown in an empty search box.</summary>
    public const string SearchHint = "Search providers and models";

    /// <summary>The text of the loading state.</summary>
    public const string LoadingText = "Loading providers from omp...";

    /// <summary>The heading of the empty state.</summary>
    public const string EmptyTitle = "No providers to show yet";

    /// <summary>The explanation of the empty state.</summary>
    public const string EmptyText =
        "omp lists only providers it has a credential for, and none was found. Add a provider from a template or define your own, or choose Add API key on a provider, and it appears here.";

    /// <summary>The heading of the error state.</summary>
    public const string ErrorTitle = "The providers could not be read";

    /// <summary>The reason shown when the source gives none.</summary>
    public const string LoadFailed = "omp's providers could not be read. Check that omp is installed and try again.";

    /// <summary>The sentence shown when the search keeps no provider.</summary>
    public const string NoMatchesText = "No provider or model matches the search.";

    /// <summary>The sentence shown when the list is not complete.</summary>
    public const string IncompleteNote =
        "omp lists only providers it has credentials for, so this list can be shorter than the providers omp supports. Add a provider from a template or define your own, or add an API key to a provider to make its models appear.";

    /// <summary>The sentence that says where the key of a provider omp knows keeps: omp holds it, Autonogrammer keeps no copy.</summary>
    public const string OmpKeyStorageNote =
        "omp keeps the key in its own credential database, unencrypted and readable only by your account. Autonogrammer hands the key to omp once and does not keep it.";

    /// <summary>The sentence that says where the key of a provider Autonogrammer wrote to models.yml keeps: Autonogrammer holds it, omp gets it per process.</summary>
    public const string AutonogrammerKeyStorageNote =
        "Autonogrammer keeps the key in secrets/secrets.json in its own state directory, unencrypted and readable only by your account, like omp's own credential database. Autonogrammer gives it to omp through an environment variable each time it starts omp. omp keeps no copy.";

    /// <summary>Returns the sentence that says where the key of a provider is kept.</summary>
    /// <param name="isAutonogrammerManaged">True when Autonogrammer wrote the provider to models.yml, so Autonogrammer holds its key.</param>
    /// <returns>The sentence.</returns>
    public static string KeyStorageNote(bool isAutonogrammerManaged) => isAutonogrammerManaged ? AutonogrammerKeyStorageNote : OmpKeyStorageNote;

    /// <summary>The notice about writing a custom provider.</summary>
    public const string CustomProviderWriteNotice =
        "A custom provider is written to models.yml in omp's configuration directory. A backup of the file is kept before each change.";

    /// <summary>The hint for a provider that signs in through omp.</summary>
    public const string SignInHint = "This provider signs in through omp. Run omp login in a terminal; Autonogrammer cannot sign in for you.";

    /// <summary>The label of the add-provider action.</summary>
    public const string AddCustomProviderLabel = "Add custom provider";

    /// <summary>The label of the add-provider entry point.</summary>
    public const string AddProviderLabel = "Add provider";

    /// <summary>The heading of the provider template list.</summary>
    public const string TemplatesHeading = "Provider templates";

    /// <summary>The sentence that explains what a provider template is.</summary>
    public const string TemplatesExplanation =
        "omp knows only its built-in providers and the providers in models.yml. Add a provider from a template or define your own: pick a template, add the model ids, save, then add the API key on the provider's card.";

    /// <summary>The label of the action that starts a custom provider from a template.</summary>
    public const string UseTemplateLabel = "Use template";

    /// <summary>The label of the edit action.</summary>
    public const string EditLabel = "Edit";

    /// <summary>The label of the remove action.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The label that confirms a removal.</summary>
    public const string ConfirmRemoveLabel = "Remove provider";

    /// <summary>The label that keeps a provider instead of removing it.</summary>
    public const string KeepLabel = "Keep provider";

    /// <summary>The label of the add-key action.</summary>
    public const string AddApiKeyLabel = "Add API key";

    /// <summary>The label of the replace-key action.</summary>
    public const string ReplaceApiKeyLabel = "Replace API key";

    /// <summary>The label of the save-key action.</summary>
    public const string SaveKeyLabel = "Save key";

    /// <summary>The label of the cancel action.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>The label of the test action.</summary>
    public const string TestLabel = "Test connection";

    /// <summary>The text shown while a test runs.</summary>
    public const string TestingText = "Testing...";

    /// <summary>The label of the use-as-default action.</summary>
    public const string UseAsDefaultLabel = "Use as default";

    /// <summary>The label that marks the default model.</summary>
    public const string DefaultModelLabel = "Default model";

    /// <summary>The heading of the models list on a card.</summary>
    public const string ModelsHeading = "Models";

    /// <summary>The text of a card that lists no model.</summary>
    public const string NoModelsText = "omp lists no models for this provider yet.";

    /// <summary>The heading of the add form.</summary>
    public const string DraftTitleAdd = "Add custom provider";

    /// <summary>The heading of the edit form.</summary>
    public const string DraftTitleEdit = "Edit custom provider";

    /// <summary>The label of the display name entry.</summary>
    public const string FieldDisplayName = "Display name";

    /// <summary>The label of the id entry.</summary>
    public const string FieldId = "Provider id";

    /// <summary>The label of the protocol choice.</summary>
    public const string FieldProtocol = "Protocol";

    /// <summary>The label of the endpoint entry.</summary>
    public const string FieldEndpoint = "Base endpoint";

    /// <summary>The label of the authentication choice.</summary>
    public const string FieldAuth = "Authentication";

    /// <summary>The label of the model list entry.</summary>
    public const string FieldModels = "Model ids, one per line";

    /// <summary>The label of the capability group.</summary>
    public const string FieldCapabilities = "Supported routes";

    /// <summary>The label of the chat capability.</summary>
    public const string CapabilityChat = "Chat completions";

    /// <summary>The label of the responses capability.</summary>
    public const string CapabilityResponses = "Responses";

    /// <summary>The label of the embeddings capability.</summary>
    public const string CapabilityEmbeddings = "Embeddings";

    /// <summary>The label of the plain http switch.</summary>
    public const string FieldAllowPlainHttp = "Allow plain http to a remote host";

    /// <summary>The hint under the plain http switch.</summary>
    public const string AllowPlainHttpHint = "Plain http sends everything, including the key, unencrypted. Leave this off unless you trust the network.";

    /// <summary>The hint that says where the key of a custom provider goes.</summary>
    public const string CustomKeyHint = "Add the API key on the provider's card after saving.";

    /// <summary>The message for a missing display name.</summary>
    public const string ErrDisplayNameRequired = "Enter a display name.";

    /// <summary>The message for a missing id.</summary>
    public const string ErrIdRequired = "Enter a provider id.";

    /// <summary>The message for an id that is not a slug.</summary>
    public const string ErrIdInvalid = "Use lowercase letters, digits and single hyphens, at most 64 characters, for example my-gateway.";

    /// <summary>The message for an id another provider uses.</summary>
    public const string ErrIdTaken = "Another provider already uses this id.";

    /// <summary>The message for a missing endpoint.</summary>
    public const string ErrEndpointRequired = "Enter the base endpoint.";

    /// <summary>The message for an endpoint that is not an absolute address.</summary>
    public const string ErrEndpointNotAbsolute = "Enter a full address that starts with https://.";

    /// <summary>The message for an endpoint that carries user information, a query or a fragment.</summary>
    public const string ErrEndpointCredential = "The address must not contain a user name, password, query or fragment. Keys go on the provider's card.";

    /// <summary>The message for an endpoint with a scheme other than https or http.</summary>
    public const string ErrEndpointUnsupportedScheme = "Only https and http addresses are supported.";

    /// <summary>The message for plain http to a remote host.</summary>
    public const string ErrEndpointPlainHttpRemote = "Plain http is accepted only for localhost, 127.0.0.1 or ::1. Use https, or allow plain http to a remote host below.";

    /// <summary>The message for a bad model id.</summary>
    public const string ErrModelInvalid = "A model id must not contain spaces or control characters and must be at most 200 characters.";

    /// <summary>The message for a repeated model id.</summary>
    public const string ErrModelDuplicate = "A model id is listed more than once.";

    /// <summary>The message for an empty key entry.</summary>
    public const string ErrApiKeyRequired = "Type the API key first.";

    /// <summary>The message for a key with spaces or control characters.</summary>
    public const string ErrApiKeyCharacters = "The key must not contain spaces, line breaks or other control characters.";

    /// <summary>The message for a key that is too long.</summary>
    public const string ErrApiKeyTooLong = "The key is longer than 4096 characters.";

    /// <summary>The reason shown when a write fails and the source gives none.</summary>
    public const string ActionFailed = "omp did not accept the change. Nothing was changed.";

    /// <summary>The text shown when a test is not run for a provider that needs credentials the owner has not supplied.</summary>
    public const string TestNotRun = "Testing needs credentials the owner has not supplied; not run.";

    /// <summary>The text shown when a provider cannot be tested from here.</summary>
    public const string TestUnavailable = "This provider cannot be tested from Autonogrammer.";

    /// <summary>The text for a reachable provider.</summary>
    public const string TestReachable = "Reachable: the endpoint answered.";

    /// <summary>The text for a provider nothing answers for.</summary>
    public const string TestNotRunning = "Nothing answered at the endpoint. Start the server and check the port.";

    /// <summary>The text for a provider that refused the credential.</summary>
    public const string TestUnauthorized = "The endpoint refused the credential. Replace the API key.";

    /// <summary>The text for a test that failed.</summary>
    public const string TestFailed = "The test failed.";

    /// <summary>Returns the heading of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The heading.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="group"/> is not defined.</exception>
    public static string GroupTitle(ProviderGroup group) => group switch
    {
        ProviderGroup.Local => "Local",
        ProviderGroup.Cloud => "Cloud",
        ProviderGroup.CodingPlan => "Coding plans",
        ProviderGroup.Custom => "Custom",
        ProviderGroup.OmpBuiltIn => "Built into omp",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown provider group."),
    };

    /// <summary>Returns the sentence under the heading of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The sentence.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="group"/> is not defined.</exception>
    public static string GroupDescription(ProviderGroup group) => group switch
    {
        ProviderGroup.Local => "Runtimes on this machine. Testing probes the address and needs no key.",
        ProviderGroup.Cloud => "Hosted APIs billed per use. Each needs its own API key.",
        ProviderGroup.CodingPlan => "Subscription plans with their own coding key and published request limits.",
        ProviderGroup.Custom => "Providers you added. Autonogrammer wrote them to omp's models.yml and can edit or remove them.",
        ProviderGroup.OmpBuiltIn => "Providers omp supports by itself. Autonogrammer can add a key; it does not change the provider.",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown provider group."),
    };

    /// <summary>Returns the words for how a provider authenticates.</summary>
    /// <param name="auth">The kind.</param>
    /// <returns>The words.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="auth"/> is not defined.</exception>
    public static string AuthLabel(ProviderAuthKind auth) => auth switch
    {
        ProviderAuthKind.None => "No authentication",
        ProviderAuthKind.ApiKey => "API key",
        ProviderAuthKind.OAuth => "Sign-in through omp",
        ProviderAuthKind.Unknown => "Authentication unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(auth), auth, "Unknown authentication kind."),
    };

    /// <summary>Returns the words for the credential state.</summary>
    /// <param name="credential">The state.</param>
    /// <returns>The words.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="credential"/> is not defined.</exception>
    public static string CredentialLabel(CredentialState credential) => credential switch
    {
        CredentialState.Present => "credential present in omp",
        CredentialState.Absent => "no credential in omp",
        CredentialState.Unknown => "credential state unknown",
        CredentialState.NotRequired => "no credential needed",
        _ => throw new ArgumentOutOfRangeException(nameof(credential), credential, "Unknown credential state."),
    };

    /// <summary>Returns the status line of a card.</summary>
    /// <param name="auth">How the provider authenticates.</param>
    /// <param name="credential">Whether omp holds a credential.</param>
    /// <returns>The line, for example "API key, no credential in omp".</returns>
    public static string StatusLine(ProviderAuthKind auth, CredentialState credential) => $"{AuthLabel(auth)}, {CredentialLabel(credential)}";

    /// <summary>Returns the label of a protocol choice.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The label.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is not defined.</exception>
    public static string ProtocolLabel(CustomProviderProtocol protocol) => protocol switch
    {
        CustomProviderProtocol.OpenAiCompatible => "OpenAI-compatible",
        CustomProviderProtocol.AnthropicCompatible => "Anthropic-compatible",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unknown protocol."),
    };

    /// <summary>Returns the label of an authentication choice of a custom provider.</summary>
    /// <param name="auth">The choice.</param>
    /// <returns>The label.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="auth"/> is not defined.</exception>
    public static string AuthChoiceLabel(CustomProviderAuth auth) => auth switch
    {
        CustomProviderAuth.None => "None",
        CustomProviderAuth.ApiKey => "API key",
        CustomProviderAuth.Bearer => "Bearer token",
        _ => throw new ArgumentOutOfRangeException(nameof(auth), auth, "Unknown authentication choice."),
    };

    /// <summary>Returns the hint under the endpoint entry for a protocol.</summary>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The hint.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is not defined.</exception>
    public static string EndpointHint(CustomProviderProtocol protocol) => protocol switch
    {
        CustomProviderProtocol.OpenAiCompatible => "The base address of the API, usually ending in /v1, for example https://example.com/v1.",
        CustomProviderProtocol.AnthropicCompatible => "The root address of the API, without /v1, for example https://example.com.",
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "Unknown protocol."),
    };

    /// <summary>Returns the accessible name of the key entry.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The name.</returns>
    public static string ApiKeyFieldLabel(string providerName) => $"API key for {providerName}";

    /// <summary>Returns the accessible name of the use-as-default action of one model.</summary>
    /// <param name="selector">The model.</param>
    /// <returns>The name.</returns>
    public static string UseAsDefaultName(string selector) => $"Use {selector} as default";

    /// <summary>Returns the line that cites the page a catalogue row was verified on.</summary>
    /// <param name="sourceUrl">The page.</param>
    /// <param name="retrievedOn">The date it was read.</param>
    /// <returns>The line.</returns>
    public static string SourceLine(Uri sourceUrl, DateOnly retrievedOn)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);
        return $"Source: {sourceUrl.AbsoluteUri}, read {retrievedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Returns the note at the top of a form that was started from a provider template.</summary>
    /// <param name="templateName">The template's display name.</param>
    /// <param name="sourceUrl">The page the template was verified on, or null.</param>
    /// <param name="retrievedOn">The date that page was read, or null.</param>
    /// <returns>The note, for example "Started from Ollama. Source: https://example.com/, read 2026-10-22.".</returns>
    public static string TemplateNote(string templateName, Uri? sourceUrl, DateOnly? retrievedOn)
    {
        ArgumentNullException.ThrowIfNull(templateName);
        if (sourceUrl is null)
        {
            return $"Started from {templateName}.";
        }

        var source = retrievedOn is { } date ? SourceLine(sourceUrl, date) : $"Source: {sourceUrl.AbsoluteUri}";
        return $"Started from {templateName}. {source}.";
    }

    /// <summary>Returns the accessible name of the button that starts a custom provider from one template.</summary>
    /// <param name="templateName">The template's display name.</param>
    /// <returns>The name.</returns>
    public static string UseTemplateName(string templateName) => $"Use the {templateName} template";

    /// <summary>Returns the line that says which endpoint a template starts with.</summary>
    /// <param name="endpoint">The template's base endpoint, or null when the user has to enter one.</param>
    /// <returns>The line.</returns>
    public static string TemplateEndpointText(Uri? endpoint) =>
        endpoint is null ? "You enter the endpoint." : $"Endpoint: {endpoint.OriginalString}";

    /// <summary>Returns the line of one plan tier.</summary>
    /// <param name="tierName">The tier name.</param>
    /// <param name="quotaText">The published limits; empty when none are published.</param>
    /// <returns>The line.</returns>
    public static string PlanLine(string tierName, string quotaText) =>
        string.IsNullOrWhiteSpace(quotaText) ? $"{tierName}: no request limit published" : $"{tierName}: {quotaText}";

    /// <summary>Returns the line that says how long a paid period lasts.</summary>
    /// <param name="days">The number of days.</param>
    /// <returns>The line.</returns>
    public static string ValidityLine(int days) => $"A paid period lasts {days.ToString(CultureInfo.InvariantCulture)} days.";

    /// <summary>Returns the question of the removal confirmation.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The question.</returns>
    public static string RemoveQuestion(string providerName) =>
        $"Remove {providerName}? It is deleted from models.yml; a backup of the file is kept. Keys omp holds for it are not touched.";

    /// <summary>Returns the live-region text for the number of matches.</summary>
    /// <param name="count">The number of providers shown.</param>
    /// <returns>The text.</returns>
    public static string MatchCount(int count) =>
        count == 1 ? "1 provider shown." : $"{count.ToString(CultureInfo.InvariantCulture)} providers shown.";

    /// <summary>Returns the text for a key omp accepted.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The text.</returns>
    public static string KeySaved(string providerName) => $"The API key for {providerName} was handed to omp.";

    /// <summary>Returns the text for a default model that omp now uses.</summary>
    /// <param name="selector">The model.</param>
    /// <returns>The text.</returns>
    public static string DefaultSet(string selector) => $"{selector} is now the default model.";

    /// <summary>Returns the text for a saved custom provider.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The text.</returns>
    public static string CustomSaved(string providerName) => $"{providerName} was saved to models.yml.";

    /// <summary>Returns the text for a removed custom provider.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <returns>The text.</returns>
    public static string CustomRemoved(string providerName) => $"{providerName} was removed from models.yml.";

    /// <summary>Returns the text for a list refresh that failed after a change was accepted.</summary>
    /// <param name="reason">The reason from the source; may be empty.</param>
    /// <returns>The text.</returns>
    public static string RefreshFailed(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? "The change was accepted, but the list could not be read again."
            : $"The change was accepted, but the list could not be read again: {reason}";

    /// <summary>Returns the text of a test outcome.</summary>
    /// <param name="status">What the test found.</param>
    /// <param name="message">The source's own sentence; used when it is not blank.</param>
    /// <param name="group">The group of the tested provider.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not defined.</exception>
    public static string TestText(ProviderTestStatus status, string? message, ProviderGroup group)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return status switch
        {
            ProviderTestStatus.Reachable => TestReachable,
            ProviderTestStatus.NotRunning => TestNotRunning,
            ProviderTestStatus.Unauthorized => TestUnauthorized,
            ProviderTestStatus.NotTestable => group is ProviderGroup.Cloud or ProviderGroup.CodingPlan or ProviderGroup.OmpBuiltIn ? TestNotRun : TestUnavailable,
            ProviderTestStatus.Failed => TestFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown test status."),
        };
    }

    /// <summary>Returns the live-region text of a finished test.</summary>
    /// <param name="providerName">The provider's display name.</param>
    /// <param name="text">The text of the outcome.</param>
    /// <returns>The text.</returns>
    public static string TestAnnouncement(string providerName, string text) => $"{providerName}: {text}";
}
