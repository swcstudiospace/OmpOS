using System.Collections.Concurrent;

namespace OmpOS.Api.Cloud;

/// <summary>
/// Keeps installation tokens in memory by the repositories and permissions they were minted for. A token is returned only while it has more than
/// <see cref="RefreshMargin"/> left; a token that is closer to <c>expires_at</c> is treated as absent, so it is replaced before git or the API can
/// be refused. Tokens are never written anywhere else.
/// </summary>
internal sealed class InstallationTokenCache
{
    /// <summary>How long before <c>expires_at</c> a token stops being served and is minted again.</summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, GitCredential> _tokens = new(StringComparer.Ordinal);

    /// <summary>Builds the cache key of a request: the sorted repository names and the permissions.</summary>
    /// <param name="repositories">The repositories.</param>
    /// <param name="permissions">The permissions.</param>
    /// <returns>The key.</returns>
    public static string KeyOf(IReadOnlyList<GitRepository> repositories, GitPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(repositories);
        ArgumentNullException.ThrowIfNull(permissions);
        var names = repositories.Select(static repository => repository.Host + "/" + repository.FullName).Order(StringComparer.Ordinal);
        return string.Join(',', names) + "|" + permissions.Contents + "|" + (permissions.PullRequests ?? "-");
    }

    /// <summary>Returns a cached token that is still good.</summary>
    /// <param name="key">The key from <see cref="KeyOf"/>.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="credential">The token, when one is cached and has more than the refresh margin left.</param>
    /// <returns>True when a usable token was found.</returns>
    public bool TryGet(string key, DateTimeOffset now, out GitCredential? credential)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_tokens.TryGetValue(key, out var cached) && cached.ExpiresAt - now > RefreshMargin)
        {
            credential = cached;
            return true;
        }

        credential = null;
        return false;
    }

    /// <summary>Stores a freshly minted token.</summary>
    /// <param name="key">The key from <see cref="KeyOf"/>.</param>
    /// <param name="credential">The token.</param>
    public void Set(string key, GitCredential credential)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(credential);
        _tokens[key] = credential;
    }

    /// <summary>Returns the tokens that are cached and still good, for the redactor.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The token texts.</returns>
    public IReadOnlyList<string> CurrentTokens(DateTimeOffset now) =>
        _tokens.Values.Where(credential => credential.ExpiresAt > now).Select(static credential => credential.Token).ToList();
}
