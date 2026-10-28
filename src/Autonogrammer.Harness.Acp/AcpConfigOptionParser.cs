using System.Text.Json;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Reads the <c>configOptions</c> array of an ACP response or <c>config_option_update</c> notification into typed options. The reader
/// is tolerant by design: a missing array, an entry of an unknown kind, or an entry of the wrong shape is skipped and never thrown.
/// </summary>
internal static class AcpConfigOptionParser
{
    private const string SelectKind = "select";

    /// <summary>Parses the options. Anything that is not a select-style option with a string current value is skipped.</summary>
    /// <param name="configOptions">The raw <c>configOptions</c> member, or <see langword="null"/> when the agent sent none.</param>
    /// <returns>The parsed options in wire order; empty when none could be read.</returns>
    public static IReadOnlyList<AcpConfigOption> Parse(JsonElement? configOptions)
    {
        if (configOptions is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        var parsed = new List<AcpConfigOption>();
        foreach (var entry in array.EnumerateArray())
        {
            if (TryParseOption(entry, out var option))
            {
                parsed.Add(option);
            }
        }

        return parsed;
    }

    private static bool TryParseOption(JsonElement entry, out AcpConfigOption option)
    {
        option = null!;
        if (entry.ValueKind != JsonValueKind.Object
            || ReadString(entry, "id") is not { Length: > 0 } id
            || ReadString(entry, "currentValue") is not { } currentValue)
        {
            return false;
        }

        // ACP reserves other option kinds (for example boolean toggles). Only an absent or select kind is modelled.
        if (ReadString(entry, "type") is { } kind && !string.Equals(kind, SelectKind, StringComparison.Ordinal))
        {
            return false;
        }

        var values = new List<AcpConfigOptionValue>();
        if (entry.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in options.EnumerateArray())
            {
                AddValues(item, values);
            }
        }

        option = new AcpConfigOption(id, ReadString(entry, "name"), ReadString(entry, "category"), currentValue, values);
        return true;
    }

    /// <summary>Adds one value, or every value of a group: ACP lets a select list its values flat or grouped.</summary>
    private static void AddValues(JsonElement item, List<AcpConfigOptionValue> values)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (ReadString(item, "value") is { Length: > 0 } value)
        {
            values.Add(new AcpConfigOptionValue(value, ReadString(item, "name") ?? value, ReadString(item, "description")));
            return;
        }

        if (item.TryGetProperty("options", out var group) && group.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in group.EnumerateArray())
            {
                if (member.ValueKind == JsonValueKind.Object && ReadString(member, "value") is { Length: > 0 } memberValue)
                {
                    values.Add(new AcpConfigOptionValue(memberValue, ReadString(member, "name") ?? memberValue, ReadString(member, "description")));
                }
            }
        }
    }

    private static string? ReadString(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var member) && member.ValueKind == JsonValueKind.String ? member.GetString() : null;
}
