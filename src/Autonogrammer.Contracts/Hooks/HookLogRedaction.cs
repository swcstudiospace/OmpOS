using System.Text.RegularExpressions;

namespace Autonogrammer.Contracts.Hooks;

/// <summary>
/// Removes the values that look like credentials from text a hook printed before the text is shown in the run log or sent in a stream
/// event. It is a safety net, not a guarantee: it finds bearer tokens, <c>Authorization</c> headers, common provider key prefixes, JSON web
/// tokens and the value of an assignment whose name says secret, token, password or key.
/// </summary>
public static partial class HookLogRedaction
{
    /// <summary>The text that replaces a removed value.</summary>
    public const string Marker = "[redacted]";

    private const int MatchTimeoutMilliseconds = 250;

    /// <summary>Redacts credential-looking values in <paramref name="text"/>.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The text with the values replaced by <see cref="Marker"/>; null for null input.</returns>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        try
        {
            var result = AuthorizationHeader().Replace(text, "${head}" + Marker);
            result = BearerToken().Replace(result, "${head}" + Marker);
            result = Assignment().Replace(result, "${head}" + Marker);
            result = KnownKeyPrefix().Replace(result, Marker);
            result = JsonWebToken().Replace(result, Marker);
            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            return Marker;
        }
    }

    [GeneratedRegex(@"(?i)(?<head>authorization\s*[:=]\s*)\S+(?:\s+\S+)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?i)(?<head>bearer\s+)[A-Za-z0-9._~+/=-]{8,}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex BearerToken();

    [GeneratedRegex("""(?i)(?<head>[A-Za-z0-9_.-]*(?:secret|token|passwd|password|api[_-]?key|private[_-]?key|access[_-]?key)[A-Za-z0-9_.-]*["']?\s*[:=]\s*["']?)[^\s"',;&]+""", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, MatchTimeoutMilliseconds)]
    private static partial Regex Assignment();

    [GeneratedRegex(@"\b(?:sk-[A-Za-z0-9_-]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|xox[abprs]-[A-Za-z0-9-]{10,}|AKIA[0-9A-Z]{16}|AIza[0-9A-Za-z_-]{30,})", RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex KnownKeyPrefix();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", RegexOptions.CultureInvariant, MatchTimeoutMilliseconds)]
    private static partial Regex JsonWebToken();
}
