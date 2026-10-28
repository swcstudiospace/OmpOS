using System.Globalization;
using System.Text;

namespace OmpOS.Workspace.Sets;

/// <summary>
/// Derives display names and identifiers for roots from their paths and keeps them unique inside a set.
/// </summary>
internal static class WorkspaceRootNaming
{
    private const string FallbackName = "root";

    /// <summary>Returns the last path segment of a local or POSIX path, or <c>root</c> for a path without one.</summary>
    /// <param name="path">The folder path.</param>
    internal static string DefaultName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var trimmed = path.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        var name = cut < 0 ? trimmed : trimmed[(cut + 1)..];
        return name.Length == 0 || name.EndsWith(':') ? FallbackName : name;
    }

    /// <summary>Returns the identifier form of a name: lowercase ASCII letters, digits and hyphens, never empty.</summary>
    /// <param name="name">The display name.</param>
    internal static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder(name.Length);
        var pendingHyphen = false;
        foreach (var character in name)
        {
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                AppendSeparator(builder, ref pendingHyphen);
                builder.Append(character);
            }
            else if (character is >= 'A' and <= 'Z')
            {
                AppendSeparator(builder, ref pendingHyphen);
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                pendingHyphen = builder.Length > 0;
            }
        }

        return builder.Length == 0 ? FallbackName : builder.ToString();
    }

    /// <summary>Returns the normal form two names are compared in: trimmed and upper-case.</summary>
    /// <param name="name">The display name.</param>
    internal static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.Trim().ToUpperInvariant();
    }

    /// <summary>Returns <paramref name="candidate"/>, or the candidate with <c>-2</c>, <c>-3</c>, and so on, so that it is not in <paramref name="taken"/>.</summary>
    /// <param name="candidate">The wanted value.</param>
    /// <param name="taken">The values already used. The result is not added to it.</param>
    /// <param name="separator">The text put between the candidate and the number.</param>
    internal static string MakeUnique(string candidate, ISet<string> taken, string separator)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(taken);
        if (!taken.Contains(candidate))
        {
            return candidate;
        }

        for (var number = 2; ; number++)
        {
            var next = candidate + separator + number.ToString(CultureInfo.InvariantCulture);
            if (!taken.Contains(next))
            {
                return next;
            }
        }
    }

    private static void AppendSeparator(StringBuilder builder, ref bool pendingHyphen)
    {
        if (pendingHyphen)
        {
            builder.Append('-');
            pendingHyphen = false;
        }
    }
}
