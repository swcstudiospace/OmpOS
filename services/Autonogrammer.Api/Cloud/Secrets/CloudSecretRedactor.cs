using System.Text.Encodings.Web;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// Replaces secret values with <c>[REDACTED]</c> in any text that leaves the control plane: run output, the stored transcript, error text, pull
/// request titles and bodies, and log text. It is the cloud counterpart of <c>PromptRedactor</c>, which redacts prompts for logs by length.
/// </summary>
/// <remarks>
/// <para>
/// A value is matched as written, as JSON-escaped text (both the default and the relaxed escaper, because tool payloads are JSON), and, for a
/// multi-line value such as a PEM key, line by line. A value that only appears transformed (base64, hex, split by the agent) is not caught: the
/// redactor is a safety net on output, not a guarantee against an agent that is told to leak a secret. Values shorter than
/// <see cref="CloudSecretNames.MinimumValueLength"/> are never stored, so none is ignored here for being short.
/// </para>
/// <para>This type holds secret values. It never prints them: <see cref="ToString"/> reports a count.</para>
/// </remarks>
internal sealed class CloudSecretRedactor
{
    /// <summary>The text that replaces a secret.</summary>
    public const string Marker = "[REDACTED]";

    private const int MinimumLineLength = 8;

    private readonly string[] _needles;

    /// <summary>Initializes a new instance of the <see cref="CloudSecretRedactor"/> class.</summary>
    /// <param name="secretValues">The secret values to remove. Blank values are ignored.</param>
    public CloudSecretRedactor(IEnumerable<string> secretValues)
    {
        ArgumentNullException.ThrowIfNull(secretValues);
        var needles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in secretValues)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            AddForms(needles, value);
            if (value.Contains('\n', StringComparison.Ordinal))
            {
                foreach (var line in value.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Length >= MinimumLineLength)
                    {
                        AddForms(needles, line);
                    }
                }
            }
        }

        // A needle that is a part of the marker would be found again inside a replacement, so it is not used.
        _needles = needles
            .Where(static needle => !Marker.Contains(needle, StringComparison.Ordinal))
            .OrderByDescending(static needle => needle.Length)
            .ThenBy(static needle => needle, StringComparer.Ordinal)
            .ToArray();
        MaximumNeedleLength = _needles.Length == 0 ? 0 : _needles[0].Length;
    }

    /// <summary>Gets a redactor that holds no secret and returns every text unchanged.</summary>
    public static CloudSecretRedactor Empty { get; } = new([]);

    /// <summary>Gets whether this redactor holds any secret.</summary>
    public bool HasSecrets => _needles.Length > 0;

    /// <summary>Gets the length of the longest text this redactor looks for. A streaming consumer holds back one character less than this between chunks.</summary>
    public int MaximumNeedleLength { get; }

    /// <summary>Removes every secret from <paramref name="text"/>.</summary>
    /// <param name="text">The text to clean. Null returns null.</param>
    /// <returns>The text with each secret replaced by <see cref="Marker"/>.</returns>
    public string? Redact(string? text)
    {
        if (text is null || _needles.Length == 0 || text.Length == 0)
        {
            return text;
        }

        var result = text;
        foreach (var needle in _needles)
        {
            result = result.Replace(needle, Marker, StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>Creates a redactor that removes the secrets of this one and <paramref name="more"/>.</summary>
    /// <param name="more">The additional secret values.</param>
    /// <returns>The combined redactor.</returns>
    public CloudSecretRedactor With(IEnumerable<string> more)
    {
        ArgumentNullException.ThrowIfNull(more);
        // The needles of this redactor are already in written and escaped form; adding them again only adds forms that are already present.
        return new CloudSecretRedactor(_needles.Concat(more));
    }

    /// <inheritdoc />
    public override string ToString() => $"CloudSecretRedactor ({_needles.Length} patterns, values not shown)";

    private static void AddForms(HashSet<string> needles, string value)
    {
        _ = needles.Add(value);
        var encoded = JavaScriptEncoder.Default.Encode(value);
        if (encoded.Length > 0)
        {
            _ = needles.Add(encoded);
        }

        var relaxed = JavaScriptEncoder.UnsafeRelaxedJsonEscaping.Encode(value);
        if (relaxed.Length > 0)
        {
            _ = needles.Add(relaxed);
        }
    }
}
