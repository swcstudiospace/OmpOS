using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// Network policy rule.
/// </summary>
public sealed class NetworkPolicyRule
{
    /// <summary>Gets or sets the destination.</summary>
    [JsonPropertyName("destination")]
    public string Destination { get; set; } = string.Empty;

    /// <summary>Gets or sets the port.</summary>
    [JsonPropertyName("port")]
    public string Port { get; set; } = string.Empty;

    /// <summary>Gets or sets the protocol.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>Gets or sets the action.</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Returns the rule as a YAML flow mapping (e.g. <c>{destination: example.com, port: 443, ...}</c>).
    /// </summary>
    /// <returns>The rule rendered as a single-line YAML mapping.</returns>
    public override string ToString()
    {
        return $"{{destination: {ToYamlScalar(Destination)}, port: {ToYamlScalar(Port)}, protocol: {ToYamlScalar(Protocol)}, action: {ToYamlScalar(Action)}}}";
    }

    private static readonly HashSet<string> ReservedPlainScalars = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "yes", "no", "on", "off", "-",
    };

    /// <summary>
    /// Formats a value as a YAML scalar, quoting when it is not a plain scalar.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The value verbatim when plain, otherwise double-quoted with escapes.</returns>
    internal static string ToYamlScalar(string value)
    {
        if (IsPlainYamlScalar(value))
        {
            return value;
        }

        return $"\"{EscapeForDoubleQuotes(value)}\"";
    }

    private static bool IsPlainYamlScalar(string value)
    {
        return value.Length > 0
            && !ReservedPlainScalars.Contains(value)
            && value.All(static c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '/');
    }

    private static string EscapeForDoubleQuotes(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20 || c == 0x7F)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
