namespace OmpOS.Presentation.Settings.Models;

/// <summary>
/// The rules a custom provider draft must satisfy. They mirror the rules the provider registry applies when it loads a custom
/// provider (display name present; id a unique lowercase slug; https, or http to loopback unless remote plain http is allowed; no
/// user information, query or fragment in the endpoint; model ids of at most 200 characters without white space or control
/// characters and without duplicates), so a draft the page accepts is one the registry accepts. Each check returns the message for the user, or null.
/// </summary>
public static class CustomProviderRules
{
    /// <summary>The longest id slug.</summary>
    public const int MaxIdLength = 64;

    /// <summary>The longest model id.</summary>
    public const int MaxModelIdLength = 200;

    /// <summary>The longest API key the page passes on.</summary>
    public const int MaxApiKeyLength = 4096;

    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1"];

    /// <summary>Checks the display name.</summary>
    /// <param name="displayName">The name as typed.</param>
    /// <returns>The message, or null when the name is acceptable.</returns>
    public static string? ValidateDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? ModelsCopy.ErrDisplayNameRequired : null;

    /// <summary>Checks the id: a lowercase slug of at most 64 characters that no other provider uses.</summary>
    /// <param name="id">The id as typed.</param>
    /// <param name="takenIds">The ids of every other provider; compared without regard to case.</param>
    /// <returns>The message, or null when the id is acceptable.</returns>
    public static string? ValidateId(string? id, IEnumerable<string> takenIds)
    {
        ArgumentNullException.ThrowIfNull(takenIds);

        var text = id?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return ModelsCopy.ErrIdRequired;
        }

        if (!IsSlug(text))
        {
            return ModelsCopy.ErrIdInvalid;
        }

        return takenIds.Any(taken => string.Equals(taken, text, StringComparison.OrdinalIgnoreCase)) ? ModelsCopy.ErrIdTaken : null;
    }

    /// <summary>Checks the base endpoint and, when it is acceptable, returns it.</summary>
    /// <param name="endpointText">The endpoint as typed.</param>
    /// <param name="allowRemotePlainHttp">True when plain http to a non-loopback host is explicitly allowed.</param>
    /// <param name="endpoint">The parsed endpoint when the result is null; otherwise null.</param>
    /// <returns>The message, or null when the endpoint is acceptable.</returns>
    public static string? ValidateEndpoint(string? endpointText, bool allowRemotePlainHttp, out Uri? endpoint)
    {
        endpoint = null;
        var text = endpointText?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return ModelsCopy.ErrEndpointRequired;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed))
        {
            return ModelsCopy.ErrEndpointNotAbsolute;
        }

        if (parsed.UserInfo.Length > 0 || parsed.Query.Length > 0 || parsed.Fragment.Length > 0)
        {
            return ModelsCopy.ErrEndpointCredential;
        }

        if (string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            endpoint = parsed;
            return null;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            return ModelsCopy.ErrEndpointUnsupportedScheme;
        }

        if (allowRemotePlainHttp || IsLoopback(parsed))
        {
            endpoint = parsed;
            return null;
        }

        return ModelsCopy.ErrEndpointPlainHttpRemote;
    }

    /// <summary>Splits the model list text into ids: one per line, each trimmed, blank lines dropped.</summary>
    /// <param name="modelsText">The text as typed.</param>
    /// <returns>The ids in the order typed.</returns>
    public static IReadOnlyList<string> ParseModels(string? modelsText)
    {
        if (string.IsNullOrEmpty(modelsText))
        {
            return [];
        }

        var ids = new List<string>();
        foreach (var line in modelsText.Split('\n'))
        {
            var id = line.Trim();
            if (id.Length > 0)
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>Checks the model ids: each at most 200 characters with no white space or control character, and no id twice.</summary>
    /// <param name="modelsText">The text as typed.</param>
    /// <returns>The message, or null when the list is acceptable.</returns>
    public static string? ValidateModels(string? modelsText)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hasDuplicate = false;
        foreach (var id in ParseModels(modelsText))
        {
            if (!IsValidModelId(id))
            {
                return ModelsCopy.ErrModelInvalid;
            }

            if (!seen.Add(id))
            {
                hasDuplicate = true;
            }
        }

        return hasDuplicate ? ModelsCopy.ErrModelDuplicate : null;
    }

    /// <summary>Checks an API key entry without ever quoting it.</summary>
    /// <param name="entry">The key as typed.</param>
    /// <returns>The message, or null when the entry can be passed on (or is empty, which is not an error but cannot be saved).</returns>
    public static string? ValidateApiKey(string? entry)
    {
        var text = entry?.Trim() ?? string.Empty;
        if (text.Length > MaxApiKeyLength)
        {
            return ModelsCopy.ErrApiKeyTooLong;
        }

        foreach (var character in text)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                return ModelsCopy.ErrApiKeyCharacters;
            }
        }

        return null;
    }

    private static bool IsSlug(string text)
    {
        if (text.Length > MaxIdLength || text[0] == '-' || text[^1] == '-')
        {
            return false;
        }

        var previousDash = false;
        foreach (var character in text)
        {
            if (character == '-')
            {
                if (previousDash)
                {
                    return false;
                }

                previousDash = true;
                continue;
            }

            previousDash = false;
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidModelId(string id)
    {
        if (id.Length > MaxModelIdLength)
        {
            return false;
        }

        foreach (var character in id)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLoopback(Uri endpoint)
    {
        // Uri.Host may carry the brackets of an IPv6 literal ("[::1]"); compare the bare address in either form.
        var host = endpoint.Host;
        if (host.Length > 2 && host[0] == '[' && host[^1] == ']')
        {
            host = host[1..^1];
        }

        return Array.Exists(LoopbackHosts, name => string.Equals(name, host, StringComparison.OrdinalIgnoreCase));
    }
}
