namespace OmpOS.Harness.Mcp.Config;

/// <summary>The rule for a server name. omp registers a server under its name and prefixes its tools with it, so the name is kept to a safe alphabet.</summary>
public static class McpServerNames
{
    /// <summary>Returns whether a name is 1 to 64 characters of ASCII letters, digits, <c>_</c>, <c>-</c> and <c>.</c>.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when the name is acceptable.</returns>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > McpConfigLimits.MaxNameLength)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
