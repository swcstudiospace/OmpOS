namespace OmpOS.Harness.Providers;

/// <summary>A single rule a provider entry or endpoint can break.</summary>
public enum ProviderValidationError
{
    /// <summary>The id is not a lowercase slug of at most 64 characters.</summary>
    InvalidId = 0,

    /// <summary>The base endpoint is missing.</summary>
    MissingBaseEndpoint,

    /// <summary>The base endpoint is a relative reference, not an absolute URI.</summary>
    BaseEndpointNotAbsolute,

    /// <summary>The endpoint scheme is neither https nor http.</summary>
    UnsupportedScheme,

    /// <summary>Plain http targets a host that is not loopback, and the entry did not allow it.</summary>
    PlainHttpToRemoteHost,

    /// <summary>The base endpoint carries user information, a query or a fragment, where a credential could sit.</summary>
    CredentialInEndpoint,

    /// <summary>The source URL is missing.</summary>
    MissingSourceUrl,

    /// <summary>The source URL is a relative reference, not an absolute URI.</summary>
    SourceUrlNotAbsolute,

    /// <summary>The retrieval date is not set.</summary>
    MissingRetrievedOn,

    /// <summary>The authentication header name is not an HTTP header token of at most 64 characters.</summary>
    InvalidAuthHeaderName,

    /// <summary>An authentication header name is set although the auth type sends no credential.</summary>
    AuthHeaderWithoutCredential,

    /// <summary>The display name is empty or blank.</summary>
    MissingDisplayName,

    /// <summary>A model id is blank, contains whitespace or a control character, or is longer than 200 characters.</summary>
    InvalidModelId,

    /// <summary>The model list names one model id more than once.</summary>
    DuplicateModelId,

    /// <summary>The id is already taken by a catalogue entry or by another registered provider.</summary>
    IdAlreadyRegistered,
}
