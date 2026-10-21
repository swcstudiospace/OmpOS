using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

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

    /// <summary>
    /// Formats a value as a YAML scalar, quoting when it is not a plain scalar.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The value verbatim when plain, otherwise double-quoted with escapes.</returns>
    internal static string ToYamlScalar(string value)
    {
        if (value.Length > 0 && value.All(static c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-' or '/'))
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal)}\"";
    }
}
