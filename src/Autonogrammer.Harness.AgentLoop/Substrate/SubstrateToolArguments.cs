using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>Builds the JSON object of an MCP tool call without reflection.</summary>
internal static class SubstrateToolArguments
{
    /// <summary>Builds an object from string, integer and boolean members. A null value is left out.</summary>
    /// <param name="members">The members in write order.</param>
    /// <returns>The object, detached from any document.</returns>
    internal static JsonElement Create(params (string Name, object? Value)[] members)
    {
        ArgumentNullException.ThrowIfNull(members);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in members)
            {
                switch (value)
                {
                    case null:
                        break;
                    case string text:
                        writer.WriteString(name, text);
                        break;
                    case int number:
                        writer.WriteNumber(name, number);
                        break;
                    case bool flag:
                        writer.WriteBoolean(name, flag);
                        break;
                    default:
                        throw new ArgumentException("Only string, integer and boolean tool arguments are supported.", nameof(members));
                }
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}
