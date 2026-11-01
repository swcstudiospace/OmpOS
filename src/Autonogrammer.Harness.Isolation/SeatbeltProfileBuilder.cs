using System.Text;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Builds a Seatbelt profile: allow default, deny writes, allow writes under read-write grants, and deny reads of the deny list.
/// Paths are quoted so a grant containing a space stays one scheme string. It does not start <c>sandbox-exec</c>.
/// </summary>
public static class SeatbeltProfileBuilder
{
    /// <summary>Builds the profile text for <paramref name="policy"/>, including a trailing newline.</summary>
    /// <param name="policy">The session policy. Read-only grants are not write exceptions.</param>
    /// <returns>The profile, one rule per line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public static string Build(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var profile = new StringBuilder();
        profile.Append("(version 1)\n");
        profile.Append("(allow default)\n");
        profile.Append("(deny file-write*)\n");
        foreach (var grant in policy.Grants)
        {
            if (grant.ReadWrite && !string.IsNullOrWhiteSpace(grant.Path))
            {
                AppendAllowWrite(profile, grant.Path);
            }
        }

        if (!string.IsNullOrWhiteSpace(policy.ScratchDirectory))
        {
            AppendAllowWrite(profile, policy.ScratchDirectory);
        }

        foreach (var state in policy.StateDirectories())
        {
            AppendAllowWrite(profile, state);
        }

        if (!string.IsNullOrWhiteSpace(policy.HomeDirectory))
        {
            foreach (var denied in IsolationDeniedDirectories.ForHome(policy.HomeDirectory))
            {
                AppendDenyRead(profile, denied);
            }
        }

        foreach (var socket in IsolationDeniedDirectories.SocketLocations)
        {
            AppendDenyRead(profile, socket);
        }

        return profile.ToString();
    }

    /// <summary>Quotes a path for a Seatbelt string, escaping backslash and double quote.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The quoted path, including the surrounding quotes.</returns>
    public static string Quote(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return "\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void AppendAllowWrite(StringBuilder profile, string path) =>
        profile.Append("(allow file-write* (subpath ").Append(Quote(path)).Append("))\n");

    private static void AppendDenyRead(StringBuilder profile, string path) =>
        profile.Append("(deny file-read* (subpath ").Append(Quote(path)).Append("))\n");
}
