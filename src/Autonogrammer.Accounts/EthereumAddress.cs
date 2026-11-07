using System.Diagnostics.CodeAnalysis;

namespace Autonogrammer.Accounts;

/// <summary>
/// Ethereum address text. The identity service keys a wallet by the address exactly as the signed message writes it, so the address is written
/// in the EIP-55 checksum form that the website's client writes. Otherwise one wallet would become two users.
/// </summary>
public static class EthereumAddress
{
    private const int HexLength = 40;
    private const int CaseOffset = 'a' - 'A';

    /// <summary>Returns the EIP-55 checksum form of a 20-byte hex address, from either case, or false when the text is not one.</summary>
    /// <param name="address">The address with its <c>0x</c> prefix.</param>
    /// <param name="checksum">The address in EIP-55 form when the result is true; null otherwise.</param>
    /// <returns>True when <paramref name="address"/> is a 20-byte hex address.</returns>
    public static bool TryToChecksumAddress(string? address, [NotNullWhen(true)] out string? checksum)
    {
        checksum = null;
        if (address is null || address.Length != HexLength + 2 || address[0] != '0' || address[1] != 'x')
        {
            return false;
        }

        var lower = new byte[HexLength];
        for (int i = 0; i < HexLength; i++)
        {
            char digit = address[i + 2];
            if (!char.IsAsciiHexDigit(digit))
            {
                return false;
            }

            lower[i] = (byte)(digit is >= 'A' and <= 'F' ? digit + CaseOffset : digit);
        }

        byte[] hash = Keccak256.Hash(lower);
        var chars = new char[HexLength + 2];
        chars[0] = '0';
        chars[1] = 'x';
        for (int i = 0; i < HexLength; i++)
        {
            int nibble = (hash[i / 2] >> (i % 2 == 0 ? 4 : 0)) & 0x0F;
            char digit = (char)lower[i];
            chars[i + 2] = char.IsAsciiLetterLower(digit) && nibble >= 8 ? (char)(digit - CaseOffset) : digit;
        }

        checksum = new string(chars);
        return true;
    }
}
