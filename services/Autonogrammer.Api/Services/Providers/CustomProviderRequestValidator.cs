using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Providers;
using Autonogrammer.Harness.OmpConfig;
using Autonogrammer.Harness.Providers;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Checks the body of <c>POST /providers/custom</c> and turns it into the endpoint to write. A problem sentence names the member, never the
/// value, so a response and a log never echo what the caller sent.
/// </summary>
internal static partial class CustomProviderRequestValidator
{
    /// <summary>The most model ids one endpoint may list.</summary>
    public const int MaxModels = 50;

    /// <summary>The longest display name accepted.</summary>
    public const int MaxDisplayNameLength = 80;

    /// <summary>Checks a request.</summary>
    /// <param name="request">The body.</param>
    /// <param name="owner">The caller that sends it.</param>
    /// <returns>The endpoint, or the problem.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="owner"/> is null.</exception>
    public static CustomProviderValidation Validate(CustomProviderRequest request, string owner)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(owner);
        if (!OmpProviderId.IsValid(request.Id))
        {
            return Refuse("The id must be lower-case letters, digits, '.', '_' or '-', start with a letter or digit, and be at most 64 characters.");
        }

        var id = request.Id!;
        if (request.Protocol is null)
        {
            return Refuse("The protocol must be 'openai' or 'anthropic'.");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? id : request.DisplayName.Trim();
        if (displayName.Length > MaxDisplayNameLength || displayName.Any(char.IsControl))
        {
            return Refuse($"The display name must be at most {MaxDisplayNameLength} characters and hold no control character.");
        }

        var url = CustomEndpointValidator.Validate(request.BaseAddress);
        if (url.Url is null)
        {
            return Refuse(url.Problem ?? "The base URL is not valid.");
        }

        var models = Models(request.ModelIds);
        if (models is null)
        {
            return Refuse($"List between 1 and {MaxModels} distinct model ids of letters, digits and . _ : / @ + - (at most 128 characters each).");
        }

        var keyed = request.KeyRequired ?? true;
        return new CustomProviderValidation(
            new CustomProviderRecord(owner, id, displayName, request.Protocol.Value, url.Url.AbsoluteUri.TrimEnd('/'), models, keyed ? ProviderKeyNames.EnvNameFor(id) : null),
            null);
    }

    /// <summary>Tells whether an id belongs to the catalogue or to a provider omp knows, so a custom endpoint must not take it.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>True when the id is reserved.</returns>
    public static bool IsReserved(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return CatalogueRows.Find(id) is not null
            || CatalogueRows.FindByOmpProviderId(id) is not null
            || CatalogueRows.All.Any(row => row.Routes.Any(route => string.Equals(row.BlockProviderId(route), id, StringComparison.Ordinal)));
    }

    private static List<string>? Models(IReadOnlyList<string>? ids)
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

    private static CustomProviderValidation Refuse(string problem) => new(null, problem);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._:/@+\-]{0,127}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModelIdPattern();
}
