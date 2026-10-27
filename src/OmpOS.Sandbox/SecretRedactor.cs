namespace OmpOS.Sandbox;

/// <summary>
/// Removes known secret values from text before it reaches an exception message or a log.
/// </summary>
/// <remarks>
/// Values shorter than <see cref="MinimumLength"/> characters are not tracked: redacting a one- or two-character value would
/// mangle ordinary words, and no credential is that short.
/// </remarks>
internal sealed class SecretRedactor
{
    /// <summary>The replacement text.</summary>
    internal const string Mask = "[REDACTED]";

    /// <summary>The shortest value that is tracked.</summary>
    internal const int MinimumLength = 4;

    private readonly object _gate = new();
    private readonly List<string> _secrets = [];

    /// <summary>
    /// Starts redacting a value.
    /// </summary>
    /// <param name="secret">The secret value; short or empty values are ignored.</param>
    public void Add(string? secret)
    {
        if (secret is null || secret.Length < MinimumLength)
        {
            return;
        }

        lock (_gate)
        {
            if (!_secrets.Contains(secret, StringComparer.Ordinal))
            {
                _secrets.Add(secret);
            }
        }
    }

    /// <summary>
    /// Replaces every tracked secret in a text, longest first.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each secret replaced by <see cref="Mask"/>.</returns>
    public string Redact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] snapshot;
        lock (_gate)
        {
            snapshot = [.. _secrets];
        }

        var result = text;
        foreach (var secret in snapshot.OrderByDescending(s => s.Length))
        {
            result = result.Replace(secret, Mask, StringComparison.Ordinal);
        }

        return result;
    }
}
