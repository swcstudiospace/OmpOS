namespace Autonogrammer.Harness.BrowserUse.Content;

/// <summary>Classifies password, one-time-code and payment fields. Values are masked.</summary>
public static class SensitiveFieldClassifier
{
    /// <summary>The mask browser_read returns for a sensitive value.</summary>
    public const string Mask = "••••";

    /// <summary>Returns whether the field is sensitive.</summary>
    /// <param name="type">The input type.</param>
    /// <param name="autocomplete">The autocomplete token.</param>
    /// <param name="name">The field name.</param>
    /// <returns>True when typing or reading it is sensitive.</returns>
    public static bool IsSensitive(string? type, string? autocomplete, string? name)
    {
        if (string.Equals(type, "password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (autocomplete is not null
            && (autocomplete.StartsWith("cc-", StringComparison.OrdinalIgnoreCase)
                || string.Equals(autocomplete, "one-time-code", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var token = name.ToUpperInvariant();
        return token.Contains("CARD", StringComparison.Ordinal)
            || token.Contains("CVV", StringComparison.Ordinal)
            || token.Contains("SSN", StringComparison.Ordinal)
            || token.Contains("OTP", StringComparison.Ordinal);
    }

    /// <summary>Masks a value when the field is sensitive.</summary>
    /// <param name="value">The value.</param>
    /// <param name="sensitive">Whether the field is sensitive.</param>
    /// <returns>The masked or original value.</returns>
    public static string MaskIfSensitive(string value, bool sensitive) => sensitive ? Mask : value;
}
