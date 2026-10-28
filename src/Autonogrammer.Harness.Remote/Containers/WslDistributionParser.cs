namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>Reads the output of <c>wsl.exe --list --quiet</c>: one distribution name per line, written by wsl.exe as UTF-16 (a UTF-8 decoder leaves NUL and replacement characters).</summary>
internal static class WslDistributionParser
{
    private const int MaxDistributions = 200;

    /// <summary>Parses the text of the listing.</summary>
    /// <param name="text">The decoded output.</param>
    /// <returns>The distribution names, in order, without blanks, byte-order marks and the NUL characters of UTF-16 text.</returns>
    internal static IReadOnlyList<string> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var cleaned = new string([.. raw.Where(character => character is not ('\0' or '\r' or '\uFEFF' or '\uFFFD'))]).Trim();
            if (cleaned.Length > 0 && names.Count < MaxDistributions)
            {
                names.Add(cleaned);
            }
        }

        return names;
    }
}
