using System.Text.RegularExpressions;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// The rules for a secret's name and value. A secret's name is the name of the environment variable it becomes in the worker, so a name that
/// would let a caller override the worker's own settings or the dynamic loader is refused.
/// </summary>
internal static partial class CloudSecretNames
{
    /// <summary>The longest accepted name.</summary>
    public const int MaximumNameLength = 64;

    /// <summary>The shortest accepted value. A shorter value would be redacted out of ordinary words.</summary>
    public const int MinimumValueLength = 4;

    /// <summary>The longest accepted value.</summary>
    public const int MaximumValueLength = 8192;

    private static readonly string[] ReservedPrefixes = ["AUTONOGRAMMER_", "WORKER_", "RAILWAY_", "LD_", "DYLD_", "BUN_", "NODE_", "DOTNET_", "ASPNETCORE_", "GIT_"];

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "HOME", "PATH", "PORT", "SHELL", "USER", "LOGNAME", "PWD", "TMPDIR", "IFS", "ENV", "BASH_ENV", "TERM",
        "OMP_AUTH_BROKER_URL", "OMP_AUTH_BROKER_TOKEN",
    };

    /// <summary>Returns whether <paramref name="name"/> is a usable secret name.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns>True for 1 to 64 letters, digits and underscores that do not start with a digit and are not reserved.</returns>
    public static bool IsValid(string? name)
    {
        if (name is null || name.Length is 0 or > MaximumNameLength || !NamePattern().IsMatch(name))
        {
            return false;
        }

        if (ReservedNames.Contains(name))
        {
            return false;
        }

        return !Array.Exists(ReservedPrefixes, prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns whether <paramref name="value"/> is a usable secret value: within the length bounds and free of NUL and other control characters.</summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>True when the value may be stored.</returns>
    public static bool IsValidValue(string? value)
    {
        if (value is null || value.Length < MinimumValueLength || value.Length > MaximumValueLength)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsControl(c) && c is not ('\n' or '\r' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NamePattern();
}
