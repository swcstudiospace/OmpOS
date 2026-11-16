using System.Text.Json;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Reads the desktop's <c>plugins/bundle.json</c>. A record that does not match its shape is refused whole; nothing is guessed.</summary>
public static class PluginBundleRecordReader
{
    /// <summary>Parses the bundle record.</summary>
    /// <param name="json">The text of <c>bundle.json</c>.</param>
    /// <returns>The records in file order. An empty array is an empty bundle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="InvalidDataException">The text is not a JSON array of valid plugin records, or two records name the same directory.</exception>
    public static IReadOnlyList<PluginBundleRecord> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var root = ParseRoot(json);
        var records = new List<PluginBundleRecord>(root.Count);
        var dirs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in root)
        {
            var record = ReadRecord(node as JsonObject ?? throw new InvalidDataException("A plugin bundle entry is not a JSON object."));
            if (!dirs.Add(record.Dir))
            {
                throw new InvalidDataException("The plugin bundle names the directory " + record.Dir + " twice.");
            }

            records.Add(record);
        }

        return records;
    }

    private static JsonArray ParseRoot(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The plugin bundle record is not JSON.", exception);
        }

        return node as JsonArray ?? throw new InvalidDataException("The plugin bundle record is not a JSON array.");
    }

    private static PluginBundleRecord ReadRecord(JsonObject entry)
    {
        var dir = Text(entry, "dir");
        var source = Text(entry, "source");
        var commit = Text(entry, "commit");
        if (!RemoteProvisionRules.IsValidDir(dir))
        {
            throw new InvalidDataException("A plugin bundle entry has a directory name that is not one plain path segment.");
        }

        if (!RemoteProvisionRules.IsValidSource(source))
        {
            throw new InvalidDataException("The plugin " + dir + " has a source that is neither in-repo nor an https git address.");
        }

        if (!RemoteProvisionRules.IsValidCommit(commit))
        {
            throw new InvalidDataException("The plugin " + dir + " has a commit that is not a 40-character lowercase hex name.");
        }

        return new PluginBundleRecord(dir, source, commit, Flag(entry, "licenseFile", dir), Flag(entry, "needsInstall", dir));
    }

    private static string Text(JsonObject entry, string name)
    {
        if (entry.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        throw new InvalidDataException("A plugin bundle entry has no text member " + name + ".");
    }

    private static bool Flag(JsonObject entry, string name, string dir)
    {
        if (entry.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        throw new InvalidDataException("The plugin " + dir + " has no boolean member " + name + ".");
    }
}
