using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Issues and checks 60-second HMAC render tickets bound to account, artifact, version and content hash.</summary>
internal sealed class ArtifactRenderTickets
{
    /// <summary>The longest ticket lifetime.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly IOptions<ArtifactApiOptions> _options;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="ArtifactRenderTickets"/> class.</summary>
    /// <param name="options">The ticket keys.</param>
    /// <param name="time">The clock.</param>
    public ArtifactRenderTickets(IOptions<ArtifactApiOptions> options, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        _options = options;
        _time = time;
    }

    /// <summary>Decodes a configured key. Returns null when it is shorter than 32 bytes. Never includes the value in a message.</summary>
    /// <param name="configured">The configured key text.</param>
    /// <returns>The key bytes, or null.</returns>
    public static byte[]? KeyBytes(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        try
        {
            var decoded = Convert.FromBase64String(configured);
            if (decoded.Length >= 32)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // A raw token of at least 32 bytes is also accepted.
        }

        var raw = Encoding.UTF8.GetBytes(configured);
        return raw.Length >= 32 ? raw : null;
    }

    /// <summary>Issues a ticket that expires in at most 60 seconds.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact.</param>
    /// <param name="version">The version number.</param>
    /// <param name="contentSha256">The version hash.</param>
    /// <returns>The path segment.</returns>
    public string Issue(string accountId, string artifactId, int version, string contentSha256) =>
        Issue(accountId, artifactId, version, contentSha256, _time.GetUtcNow().Add(Lifetime));

    /// <summary>Issues a ticket with an explicit expiry. Tests use a past expiry to prove refusal.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact.</param>
    /// <param name="version">The version number.</param>
    /// <param name="contentSha256">The version hash.</param>
    /// <param name="expires">The expiry.</param>
    /// <returns>The path segment.</returns>
    public string Issue(string accountId, string artifactId, int version, string contentSha256, DateTimeOffset expires)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        ArgumentNullException.ThrowIfNull(artifactId);
        ArgumentNullException.ThrowIfNull(contentSha256);
        var key = KeyBytes(_options.Value.RenderTicketKeys[0]) ?? throw new InvalidOperationException(ArtifactApiOptions.SectionName + ":RenderTicketKeys is invalid.");
        var expiry = expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var payload = string.Join('\n', accountId, artifactId, version.ToString(CultureInfo.InvariantCulture), contentSha256, expiry);
        var sig = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
        return Base64Url(payload) + "." + sig;
    }

    /// <summary>Checks a ticket. A failure carries no ticket text.</summary>
    /// <param name="ticket">The path segment.</param>
    /// <param name="accountId">The bound account.</param>
    /// <param name="artifactId">The bound artifact.</param>
    /// <param name="version">The bound version.</param>
    /// <param name="contentSha256">The bound hash.</param>
    /// <returns>True when the HMAC and the 60-second window hold.</returns>
    public bool TryOpen(string ticket, out string accountId, out string artifactId, out int version, out string contentSha256)
    {
        accountId = string.Empty;
        artifactId = string.Empty;
        version = 0;
        contentSha256 = string.Empty;
        var dot = ticket.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0 || dot == ticket.Length - 1)
        {
            return false;
        }

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Base64UrlDecode(ticket[..dot]));
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = payload.Split('\n');
        if (parts.Length != 5 || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out version) || !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiryUnix))
        {
            return false;
        }

        var expires = DateTimeOffset.FromUnixTimeSeconds(expiryUnix);
        var now = _time.GetUtcNow();
        if (expires < now || expires - now > Lifetime)
        {
            return false;
        }

        var sig = ticket[(dot + 1)..];
        foreach (var configured in _options.Value.RenderTicketKeys)
        {
            var key = KeyBytes(configured);
            if (key is null)
            {
                continue;
            }

            var expected = Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload)));
            var presented = sig.ToUpperInvariant();
            if (expected.Length == presented.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(presented)))
            {
                accountId = parts[0];
                artifactId = parts[1];
                contentSha256 = parts[3];
                return true;
            }
        }

        return false;
    }

    private static string Base64Url(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
