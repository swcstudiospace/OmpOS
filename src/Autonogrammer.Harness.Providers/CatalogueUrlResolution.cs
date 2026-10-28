namespace Autonogrammer.Harness.Providers;

/// <summary>The outcome of filling a base URL template: the URL, or a plain sentence that names what is wrong.</summary>
/// <param name="Url">The resolved absolute URL, or null when resolution failed.</param>
/// <param name="Problem">A sentence for the user when resolution failed, or null.</param>
public sealed record CatalogueUrlResolution(Uri? Url, string? Problem)
{
    /// <summary>Gets a value indicating whether resolution produced a URL.</summary>
    public bool Succeeded => Url is not null;
}
