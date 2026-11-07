using System.Security.Cryptography;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Creates command identifiers in the ULID shape of D-17: 26 Crockford base32 characters, a 48-bit millisecond timestamp and 80 random bits.
/// Identifiers made later sort after identifiers made earlier, because the first ten characters carry the clock.
/// </summary>
public static class DispatchCommandIds
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int TimeCharacters = 10;
    private const int RandomBytes = 10;
    private const int Length = TimeCharacters + (RandomBytes * 8 / 5);

    /// <summary>Creates a new command identifier.</summary>
    /// <param name="time">The clock the timestamp is read from.</param>
    /// <returns>The 26-character identifier.</returns>
    public static string New(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var characters = new char[Length];
        var millis = (ulong)time.GetUtcNow().ToUnixTimeMilliseconds();
        for (var index = TimeCharacters - 1; index >= 0; index--)
        {
            characters[index] = Alphabet[(int)(millis & 31)];
            millis >>= 5;
        }

        var buffer = 0UL;
        var bits = 0;
        var next = TimeCharacters;
        foreach (var value in RandomNumberGenerator.GetBytes(RandomBytes))
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                characters[next++] = Alphabet[(int)((buffer >> bits) & 31)];
            }
        }

        return new string(characters);
    }
}
