using System.Text.RegularExpressions;

namespace OmpOS.Harness.OmpConfig;

/// <summary>The rules a managed provider must satisfy before it is written to <c>models.yml</c>.</summary>
internal static partial class OmpManagedProviderValidator
{
    /// <summary>The longest base URL accepted.</summary>
    public const int MaxBaseUrlLength = 2048;

    /// <summary>The longest environment variable name accepted.</summary>
    public const int MaxEnvNameLength = 128;

    /// <summary>The longest display name accepted.</summary>
    public const int MaxModelNameLength = 200;

    /// <summary>The most models one provider may list.</summary>
    public const int MaxModels = 500;

    /// <summary>Tells whether <paramref name="name"/> is an environment variable name: <c>[A-Z][A-Z0-9_]*</c>.</summary>
    /// <param name="name">The name; null is not valid.</param>
    /// <returns>True when the name is acceptable.</returns>
    public static bool IsEnvName(string? name) =>
        name is not null && name.Length <= MaxEnvNameLength && EnvNamePattern().IsMatch(name);

    /// <summary>Checks every rule and returns the first one the provider breaks.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>A plain sentence naming the broken rule, or null when the provider is acceptable.</returns>
    public static string? Validate(OmpManagedProvider provider)
    {
        if (!OmpProviderId.IsValid(provider.Id))
        {
            return "The provider id must be lower-case letters, digits, '.', '_' or '-', start with a letter or digit, and be at most 64 characters.";
        }

        if (!IsAcceptableBaseUrl(provider.BaseUrl))
        {
            return "The base URL must be an http or https address without user name, password, query or fragment.";
        }

        if (OmpProviderApiNames.ToYaml(provider.Api) is null)
        {
            return "The provider protocol is not one omp supports here.";
        }

        var authentication = AuthenticationProblem(provider);
        if (authentication is not null)
        {
            return authentication;
        }

        var discovery = DiscoveryProblem(provider);
        return discovery ?? ModelsProblem(provider.Models, provider.Discovery is not null);
    }

    private static string? DiscoveryProblem(OmpManagedProvider provider)
    {
        if (provider.Discovery is not null && !OmpManagedDiscovery.IsKnownType(provider.Discovery.Type))
        {
            return "The model discovery type is not one omp supports.";
        }

        if (provider.KeyHeaderName is not null && (provider.NoAuthentication || !HeaderNamePattern().IsMatch(provider.KeyHeaderName)))
        {
            return "The key header must be a valid header name and needs an API key environment variable.";
        }

        return provider.BearerAuthHeader && provider.NoAuthentication
            ? "A Bearer header needs an API key environment variable."
            : null;
    }

    private static string? AuthenticationProblem(OmpManagedProvider provider)
    {
        if (provider.NoAuthentication && provider.ApiKeyEnvName is not null)
        {
            return "Choose either an API key environment variable or no authentication, not both.";
        }

        if (provider.NoAuthentication)
        {
            return null;
        }

        if (provider.ApiKeyEnvName is null)
        {
            return "A provider needs the name of the environment variable that holds its API key, or must be marked as needing no authentication.";
        }

        return IsEnvName(provider.ApiKeyEnvName)
            ? null
            : "The API key setting must be the NAME of an environment variable, such as MY_PROVIDER_API_KEY. Literal keys and !command forms are not supported.";
    }

    private static string? ModelsProblem(IReadOnlyList<OmpManagedModel> models, bool discovered)
    {
        if (discovered && (models is null || models.Count == 0))
        {
            return null;
        }

        if (models is null || models.Count == 0)
        {
            return "A provider needs at least one model.";
        }

        if (models.Count > MaxModels)
        {
            return $"A provider may list at most {MaxModels} models.";
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            if (model is null || !IsModelId(model.Id))
            {
                return "A model id must be 1 to 200 characters without spaces or control characters.";
            }

            if (!seen.Add(model.Id))
            {
                return "A model id appears twice.";
            }

            if (model.Name is not null && (model.Name.Length is 0 or > MaxModelNameLength || !OmpYamlScalar.IsPlain(model.Name)))
            {
                return "A model name must be 1 to 200 characters on one line.";
            }

            if (model.ContextWindow is <= 0 || model.MaxTokens is <= 0)
            {
                return "A context window and a token limit must be positive.";
            }
        }

        return null;
    }

    private static bool IsModelId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > OmpManagedModel.MaxIdLength || !OmpYamlScalar.IsPlain(id))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAcceptableBaseUrl(Uri? baseUrl)
    {
        if (baseUrl is null || !baseUrl.IsAbsoluteUri)
        {
            return false;
        }

        var text = baseUrl.OriginalString;
        if (text.Length > MaxBaseUrlLength || !OmpYamlScalar.IsPlain(text))
        {
            return false;
        }

        foreach (var character in text)
        {
            // Printable ASCII only, and none of the characters a URL may not contain. Hosts with non-ASCII names use punycode.
            if (character <= ' ' || character > '~' || "\"<>\\^`{|}".Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (text.Contains('?', StringComparison.Ordinal) || text.Contains('#', StringComparison.Ordinal))
        {
            return false;
        }

        return (string.Equals(baseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) &&
            baseUrl.UserInfo.Length == 0 &&
            !string.IsNullOrEmpty(baseUrl.Host);
    }

    // RFC 9110 section 5.6.2: a header field name is a token of visible ASCII characters without separators.
    [GeneratedRegex("^[!#$%&'*+\\-.^_`|~0-9A-Za-z]{1,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HeaderNamePattern();

    [GeneratedRegex("^[A-Z][A-Z0-9_]*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvNamePattern();
}
