using System.Text.Json;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Reads the <c>availableCommands</c> array of an <c>available_commands_update</c> notification into typed commands. The reader is tolerant
/// by design: a missing array, an entry of the wrong shape or an entry without a name is skipped and never thrown, and a name the agent
/// repeats keeps its first entry.
/// </summary>
internal static class AcpAvailableCommandParser
{
    /// <summary>Parses the commands. Anything that is not an object with a non-empty string <c>name</c> is skipped.</summary>
    /// <param name="availableCommands">The raw <c>availableCommands</c> member, or <see langword="null"/> when the agent sent none.</param>
    /// <returns>The parsed commands in wire order; empty when none could be read.</returns>
    public static IReadOnlyList<AcpAvailableCommand> Parse(JsonElement? availableCommands)
    {
        if (availableCommands is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var parsed = new List<AcpAvailableCommand>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || ReadString(entry, "name") is not { Length: > 0 } name || !seen.Add(name))
            {
                continue;
            }

            string? hint = null;
            if (entry.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
            {
                hint = ReadString(input, "hint");
            }

            parsed.Add(new AcpAvailableCommand(name, ReadString(entry, "description"), hint is { Length: > 0 } ? hint : null));
        }

        return parsed;
    }

    private static string? ReadString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String ? member.GetString() : null;
}
