using System.Text.RegularExpressions;
using OmpOS.Contracts.Providers;
using OmpOS.Harness.Providers;

namespace OmpOS.Web.Demo;

/// <summary>The field rules of a custom provider request (id, protocol, display name, base address and model ids), the same as the real route.</summary>
internal static partial class DemoProvidersCustomRules
{
    private const int MaxModels = 50;
    private const int MaxDisplayNameLength = 80;

    /// <summary>Validates a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="baseAddress">The canonical base address when the request is valid.</param>
    /// <param name="displayName">The display name when the request is valid.</param>
    /// <param name="models">The trimmed model ids when the request is valid.</param>
    /// <returns>The sentence that says why the request is refused, or null when it is valid.</returns>
    internal static string? Validate(CustomProviderRequest request, out string baseAddress, out string displayName, out IReadOnlyList<string> models)
    {
        ArgumentNullException.ThrowIfNull(request);
        baseAddress = string.Empty;
        displayName = string.Empty;
        models = [];
        if (request.Id is null || !IdPattern().IsMatch(request.Id))
        {
            return "The id must be lower-case letters, digits, '.', '_' or '-', start with a letter or digit, and be at most 64 characters.";
        }

        if (request.Protocol is null)
        {
            return "The protocol must be 'openai' or 'anthropic'.";
        }

        displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Id : request.DisplayName.Trim();
        if (displayName.Length > MaxDisplayNameLength || displayName.Any(char.IsControl))
        {
            return $"The display name must be at most {MaxDisplayNameLength} characters and hold no control character.";
        }

        var address = DemoProvidersEndpointCheck.Check(request.BaseAddress, out var problem);
        if (address is null)
        {
            return problem ?? "The base URL is not valid.";
        }

        baseAddress = address;
        var parsed = ParseModels(request.ModelIds);
        if (parsed is null)
        {
            return $"List between 1 and {MaxModels} distinct model ids of letters, digits and . _ : / @ + - (at most 128 characters each).";
        }

        models = parsed;
        return null;
    }

    /// <summary>Tells whether an id belongs to the catalogue, so a custom provider cannot take it.</summary>
    /// <param name="id">The id.</param>
    /// <returns>True when a catalogue row, an omp provider id or a block id of a row uses it.</returns>
    internal static bool IsReserved(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return CatalogueRows.Find(id) is not null
            || CatalogueRows.FindByOmpProviderId(id) is not null
            || CatalogueRows.All.Any(row => row.Routes.Any(route => string.Equals(row.BlockProviderId(route), id, StringComparison.Ordinal)));
    }

    /// <summary>Returns the variable the operator would set for a custom provider's key: <c>OMPOS_PROVIDER_&lt;ID&gt;_KEY</c>.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The variable name.</returns>
    internal static string KeyEnvNameFor(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return "OMPOS_PROVIDER_" + string.Concat(id.Select(static character => char.IsAsciiLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_')) + "_KEY";
    }

    private static List<string>? ParseModels(IReadOnlyList<string>? ids)
    {
        if (ids is null || ids.Count is 0 or > MaxModels)
        {
            return null;
        }

        var models = new List<string>(ids.Count);
        foreach (var raw in ids)
        {
            var id = raw?.Trim();
            if (id is null || !ModelIdPattern().IsMatch(id) || models.Contains(id, StringComparer.Ordinal))
            {
                return null;
            }

            models.Add(id);
        }

        return models;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:/@+\-]{0,127}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModelIdPattern();
}
