using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Maps an account to its own directory under the shared workspace base. The directory name is a SHA-256 digest of the account id, so the identity subject never
/// appears in a path and every name is one safe segment.
/// </summary>
internal static class AccountWorkspace
{
    /// <summary>Returns the directory name for an account: 64 lowercase hexadecimal characters.</summary>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The directory name.</returns>
    public static string DirectoryName(string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(accountId)));
    }

    /// <summary>Returns the account's directory under the base, or null when the base is not an absolute path.</summary>
    /// <param name="baseRoot">The configured workspace base.</param>
    /// <param name="accountId">The account identifier.</param>
    /// <returns>The account directory, or null.</returns>
    public static string? Resolve(string baseRoot, string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseRoot);
        if (!Path.IsPathFullyQualified(baseRoot))
        {
            return null;
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseRoot));
        return Path.Combine(root, DirectoryName(accountId));
    }
}
