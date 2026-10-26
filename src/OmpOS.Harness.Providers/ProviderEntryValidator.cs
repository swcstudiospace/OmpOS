using System.Text.RegularExpressions;

namespace OmpOS.Harness.Providers;

/// <summary>Checks a provider entry as a whole: its id, its base endpoint, its auth header name, its source citation and its retrieval date.</summary>
public static partial class ProviderEntryValidator
{
    private const int MaxIdLength = 64;
    private const int MaxHeaderNameLength = 64;

    /// <summary>Returns every rule the entry breaks. A valid entry yields an empty error list.</summary>
    /// <param name="entry">The entry to check.</param>
    public static ProviderValidationResult Validate(ProviderEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var errors = new List<ProviderValidationError>();

        if (entry.Id.Length > MaxIdLength || !SlugPattern().IsMatch(entry.Id))
        {
            errors.Add(ProviderValidationError.InvalidId);
        }

        errors.AddRange(ProviderEndpointValidator.Validate(entry.BaseEndpoint, entry.AllowRemotePlainHttp).Errors);

        if (entry.AuthHeaderName is not null)
        {
            if (entry.AuthHeaderName.Length is 0 or > MaxHeaderNameLength || !HeaderTokenPattern().IsMatch(entry.AuthHeaderName))
            {
                errors.Add(ProviderValidationError.InvalidAuthHeaderName);
            }

            if (entry.AuthType == ProviderAuthType.None)
            {
                errors.Add(ProviderValidationError.AuthHeaderWithoutCredential);
            }
        }

        if (entry.SourceUrl is null)
        {
            errors.Add(ProviderValidationError.MissingSourceUrl);
        }
        else if (!entry.SourceUrl.IsAbsoluteUri)
        {
            errors.Add(ProviderValidationError.SourceUrlNotAbsolute);
        }

        if (entry.RetrievedOn == default)
        {
            errors.Add(ProviderValidationError.MissingRetrievedOn);
        }

        return new ProviderValidationResult(errors);
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.NonBacktracking)]
    private static partial Regex SlugPattern();

    // RFC 9110 section 5.6.2: a header field name is a token of visible ASCII characters without separators.
    [GeneratedRegex(@"^[!#$%&'*+\-.^_`|~0-9A-Za-z]+\z", RegexOptions.NonBacktracking)]
    private static partial Regex HeaderTokenPattern();
}
