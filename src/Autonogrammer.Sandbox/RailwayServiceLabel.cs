using System.Security.Cryptography;
using System.Text;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Builds the service name (label) that identifies a session's Railway service and volume.
/// </summary>
/// <remarks>
/// The label is the prefix plus the session id reduced to lowercase <c>[a-z0-9-]</c>. When the reduction changed the id or the
/// label would exceed <see cref="MaxLength"/> characters, a dash and 8 hex digits of the SHA-256 of the original id are appended,
/// so two different session ids never share a label (up to the 32-bit hash collision of ids that reduce to the same text).
/// </remarks>
internal static class RailwayServiceLabel
{
    /// <summary>The longest label produced, in characters.</summary>
    internal const int MaxLength = 60;

    /// <summary>The longest service name prefix the options accept, leaving room for a hashed session part.</summary>
    internal const int MaxPrefixLength = 40;

    private const int HashLength = 8;

    /// <summary>
    /// Builds the label of a session.
    /// </summary>
    /// <param name="prefix">The service name prefix; lowercase <c>[a-z0-9-]</c>, at most <see cref="MaxPrefixLength"/> characters.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The label, at most <see cref="MaxLength"/> characters.</returns>
    /// <exception cref="ArgumentException">The session id has no letter or digit.</exception>
    public static string Create(string prefix, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var sanitized = Sanitize(sessionId);
        if (sanitized.Length == 0)
        {
            throw new ArgumentException("The session id has no letter or digit usable in a service name.", nameof(sessionId));
        }

        var altered = !string.Equals(sanitized, sessionId, StringComparison.Ordinal);
        if (!altered && prefix.Length + sanitized.Length <= MaxLength)
        {
            return prefix + sanitized;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..HashLength];
        var budget = MaxLength - prefix.Length - HashLength - 1;
        var core = sanitized.Length > budget ? sanitized[..budget].TrimEnd('-') : sanitized;
        return core.Length == 0 ? prefix + hash : prefix + core + "-" + hash;
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var raw in value)
        {
            var c = char.ToLowerInvariant(raw);
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
