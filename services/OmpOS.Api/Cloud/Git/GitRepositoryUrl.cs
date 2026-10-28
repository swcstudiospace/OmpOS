using System.Text.RegularExpressions;

namespace OmpOS.Api.Cloud;

/// <summary>Parses and validates the clone URL of a repository. A URL with a credential, a query, a fragment, a port, another scheme than HTTPS, an unlisted host or a path that is not exactly <c>/owner/repo</c> is refused.</summary>
internal static partial class GitRepositoryUrl
{
    /// <summary>Parses a clone URL.</summary>
    /// <param name="url">The URL as the caller wrote it.</param>
    /// <param name="allowedHosts">The hosts a caller may name.</param>
    /// <param name="repository">The parsed repository, or null.</param>
    /// <param name="problem">Why the URL was refused, or null. It never repeats the URL.</param>
    /// <returns>True when the URL is acceptable.</returns>
    public static bool TryParse(string? url, IReadOnlyCollection<string> allowedHosts, out GitRepository? repository, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(allowedHosts);
        repository = null;
        if (string.IsNullOrWhiteSpace(url) || url.Length > 512 || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            problem = "The repository URL must be an absolute https URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            problem = "The repository URL must use https.";
            return false;
        }

        if (uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || !uri.IsDefaultPort)
        {
            problem = "The repository URL must not carry a credential, a port, a query or a fragment.";
            return false;
        }

        var host = uri.Host;
        if (!allowedHosts.Any(allowed => string.Equals(allowed, host, StringComparison.OrdinalIgnoreCase)))
        {
            problem = "The repository host is not allowed on this server.";
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2)
        {
            problem = "The repository URL must be https://host/owner/repository.";
            return false;
        }

        var name = segments[1].EndsWith(".git", StringComparison.Ordinal) ? segments[1][..^4] : segments[1];
        if (!Segment().IsMatch(segments[0]) || !Segment().IsMatch(name) || segments[0] is "." or ".." || name is "." or "..")
        {
            problem = "The owner and the repository name may hold only letters, digits, dots, hyphens and underscores.";
            return false;
        }

        repository = new GitRepository(host, segments[0], name);
        problem = null;
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Segment();
}
