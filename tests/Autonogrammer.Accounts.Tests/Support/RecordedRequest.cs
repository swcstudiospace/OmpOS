namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>One request the fake saw. Bodies and queries are kept so a test can assert what was sent; nothing logs them.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path below <c>/auth/v1/</c>.</param>
/// <param name="Query">The raw query, without the question mark.</param>
/// <param name="Body">The request body text.</param>
/// <param name="ApiKey">The <c>apikey</c> header, or null.</param>
/// <param name="Bearer">The bearer token, or null.</param>
internal sealed record RecordedRequest(string Method, string Path, string Query, string Body, string? ApiKey, string? Bearer)
{
    /// <summary>Tells whether the request went to a path below the auth root.</summary>
    /// <param name="path">The path to compare with, for example <c>logout</c>.</param>
    /// <returns>True when the paths are equal.</returns>
    public bool PathIs(string path) => string.Equals(Path, path, StringComparison.Ordinal);
}
