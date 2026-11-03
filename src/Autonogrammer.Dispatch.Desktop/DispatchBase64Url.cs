namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Strict base64url decoding for the signed payload and signature fields. Padding is optional; any other character is refused.</summary>
internal static class DispatchBase64Url
{
    /// <summary>Decodes a base64url string.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="bytes">The decoded bytes when the text is valid.</param>
    /// <returns>True when the text decodes.</returns>
    public static bool TryDecode(string text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.Length % 4 == 1)
        {
            return false;
        }

        foreach (var ch in text)
        {
            if (!(ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_'))
            {
                return false;
            }
        }

        var padded = text.Replace('-', '+').Replace('_', '/');
        var remainder = padded.Length % 4;
        if (remainder != 0)
        {
            padded += new string('=', 4 - remainder);
        }

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
