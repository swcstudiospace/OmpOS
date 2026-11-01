using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Cowork;

/// <summary>
/// Derives the per-account Cowork folder. The name is the first 20 characters of the lower-case base32 of SHA-256 of the
/// account subject, so two accounts never share a file and the folder name never contains the subject.
/// </summary>
public static class CoworkAccountFolder
{
    /// <summary>The number of characters kept from the base32 digest.</summary>
    public const int KeyLength = 20;

    /// <summary>The directory under the state root that holds every account folder.</summary>
    public const string RootName = "cowork";

    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";

    /// <summary>Returns the folder key for an account subject. The caller is the Native head; this type does not read a session.</summary>
    /// <param name="subject">The account subject. It is hashed and is not copied into the result.</param>
    /// <returns>Twenty lower-case base32 characters.</returns>
    /// <exception cref="ArgumentException"><paramref name="subject"/> is null, empty or white space.</exception>
    public static string KeyFromSubject(string subject)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(subject));
        return Encode(hash)[..KeyLength];
    }

    /// <summary>Returns <c>&lt;stateRoot&gt;/cowork/&lt;accountFolderKey&gt;</c>.</summary>
    /// <param name="stateRoot">The state directory.</param>
    /// <param name="accountFolderKey">The key from <see cref="ICoworkAccountGate"/> or <see cref="KeyFromSubject"/>.</param>
    /// <returns>The absolute account directory.</returns>
    /// <exception cref="ArgumentException">A path is missing, or the key is not a single safe path segment.</exception>
    public static string AccountDirectory(string stateRoot, string accountFolderKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        if (!IsSafeKey(accountFolderKey))
        {
            throw new ArgumentException("The account folder key must be a single path segment.", nameof(accountFolderKey));
        }

        return Path.Combine(Path.GetFullPath(stateRoot), RootName, accountFolderKey);
    }

    private static bool IsSafeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key is "." or "..")
        {
            return false;
        }

        foreach (var character in key)
        {
            var allowed = character is >= 'a' and <= 'z' or >= '0' and <= '9';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string Encode(byte[] bytes)
    {
        Span<char> characters = stackalloc char[56];
        var index = 0;
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                characters[index++] = Alphabet[(buffer >> (bits - 5)) & 31];
                bits -= 5;
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            characters[index++] = Alphabet[(buffer << (5 - bits)) & 31];
        }

        return new string(characters[..index]);
    }
}

