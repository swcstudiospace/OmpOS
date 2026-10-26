using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>Creates and checks session identifiers. An identifier matches <c>^[A-Za-z0-9_.-]{1,120}$</c> and is neither <c>.</c> nor <c>..</c>, so it is safe as a directory name.</summary>
internal static class AgentSessionIds
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz234567";
    private const int RandomCharacters = 26;
    private const int MaxLength = 120;

    /// <summary>Creates an identifier of the form <c>s-</c> followed by 26 lowercase base32 characters from the system random generator.</summary>
    /// <returns>The identifier.</returns>
    public static string New()
    {
        // 17 bytes are 136 bits; 26 base32 characters use the first 130.
        Span<byte> bytes = stackalloc byte[17];
        RandomNumberGenerator.Fill(bytes);
        Span<char> characters = stackalloc char[RandomCharacters + 2];
        characters[0] = 's';
        characters[1] = '-';
        var index = 2;
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5 && index < characters.Length)
            {
                characters[index++] = Alphabet[(buffer >> (bits - 5)) & 31];
                bits -= 5;
            }

            buffer &= (1 << bits) - 1;
        }

        return new string(characters);
    }

    /// <summary>Returns true when <paramref name="id"/> is a valid session identifier.</summary>
    /// <param name="id">The candidate.</param>
    /// <returns>True when the identifier is valid.</returns>
    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxLength || id is "." or "..")
        {
            return false;
        }

        foreach (var character in id)
        {
            var allowed = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '_' or '.' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}
