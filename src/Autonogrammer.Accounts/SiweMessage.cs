using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Autonogrammer.Accounts;

/// <summary>
/// Builds the Sign-In-With-Ethereum message that a wallet signs (EIP-4361). The service parses the <c>URI</c>, <c>Version</c> and <c>Issued At</c>
/// lines, so they are always written. Each field is validated first, because a malformed message is refused by the service after the person has
/// already signed it.
/// </summary>
public static partial class SiweMessage
{
    /// <summary>The message version the service accepts.</summary>
    public const char Version = '1';

    private const string SigningVerb = " wants you to sign in with your Ethereum account:";

    /// <summary>Builds the message exactly as EIP-4361 lays it out.</summary>
    /// <param name="fields">The validated fields.</param>
    /// <returns>The message text, with no trailing newline.</returns>
    /// <exception cref="ArgumentException">A field is missing or malformed. The message names the field, never a value.</exception>
    public static string Build(SiweFields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Validate(fields);

        var text = new StringBuilder();
        text.Append(fields.Domain).Append(SigningVerb).Append('\n');
        text.Append(fields.Address).Append('\n');
        text.Append('\n');
        if (fields.Statement is not null)
        {
            text.Append(fields.Statement).Append('\n').Append('\n');
        }

        text.Append("URI: ").Append(fields.Uri.AbsoluteUri).Append('\n');
        text.Append("Version: ").Append(Version).Append('\n');
        text.Append("Chain ID: ").Append(fields.ChainId).Append('\n');
        text.Append("Nonce: ").Append(fields.Nonce).Append('\n');
        text.Append("Issued At: ").Append(fields.IssuedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static void Validate(SiweFields fields)
    {
        if (!DomainPattern().IsMatch(fields.Domain))
        {
            throw new ArgumentException("The domain is not an authority.", nameof(fields));
        }

        if (!AddressPattern().IsMatch(fields.Address))
        {
            throw new ArgumentException("The address is not 0x followed by 40 hexadecimal digits.", nameof(fields));
        }

        if (!fields.Uri.IsAbsoluteUri || !IsAllowedUri(fields.Uri))
        {
            throw new ArgumentException("The URI must be https, or http to a loopback address.", nameof(fields));
        }

        if (!ChainIdPattern().IsMatch(fields.ChainId))
        {
            throw new ArgumentException("The chain identifier is not a positive decimal number.", nameof(fields));
        }

        if (!NoncePattern().IsMatch(fields.Nonce))
        {
            throw new ArgumentException("The nonce must be at least eight letters or digits.", nameof(fields));
        }

        if (fields.Statement is not null && (fields.Statement.Contains('\n', StringComparison.Ordinal) || fields.Statement.Contains('\r', StringComparison.Ordinal)))
        {
            throw new ArgumentException("The statement must be one line.", nameof(fields));
        }
    }

    private static bool IsAllowedUri(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        || (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && uri.IsLoopback);

    [GeneratedRegex("^[A-Za-z0-9.-]+(:[0-9]{1,5})?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex DomainPattern();

    [GeneratedRegex("^0x[0-9a-fA-F]{40}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex AddressPattern();

    [GeneratedRegex("^[1-9][0-9]{0,18}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex ChainIdPattern();

    [GeneratedRegex("^[A-Za-z0-9]{8,}$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 500)]
    private static partial Regex NoncePattern();
}
