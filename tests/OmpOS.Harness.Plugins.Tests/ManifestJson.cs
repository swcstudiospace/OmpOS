using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Builds manifests for tests with <see cref="JsonObject"/>, so the JSON is well formed. Leaf values are parsed from their
/// JSON text, which needs no type metadata, and the tree is serialized through the test context.
/// </summary>
internal static class ManifestJson
{
    /// <summary>
    /// Builds a hook declaration.
    /// </summary>
    /// <param name="trigger">The trigger name, such as <c>beforeAction</c>.</param>
    /// <param name="command">The argument list; the first element is the executable.</param>
    /// <param name="workingDirectory">The absolute working directory.</param>
    /// <param name="blocking">Whether the hook is blocking.</param>
    public static JsonObject Hook(string trigger, string[] command, string workingDirectory, bool blocking = false) =>
        new()
        {
            ["trigger"] = Value(trigger),
            ["command"] = Strings(command),
            ["workingDirectory"] = Value(workingDirectory),
            ["blocking"] = Value(blocking),
        };

    /// <summary>
    /// Builds a manifest with no capabilities or permissions and the given hooks.
    /// </summary>
    /// <param name="id">The plugin identifier.</param>
    /// <param name="apiVersion">The declared API version.</param>
    /// <param name="hooks">The hooks, in order.</param>
    public static JsonObject Manifest(string id, int apiVersion = 1, params JsonObject[] hooks)
    {
        var hookArray = new JsonArray();
        foreach (var hook in hooks)
        {
            hookArray.Add(hook);
        }

        return new JsonObject
        {
            ["id"] = Value(id),
            ["version"] = Value("1.0.0"),
            ["apiVersion"] = Value(apiVersion),
            ["capabilities"] = new JsonArray(),
            ["permissions"] = new JsonArray(),
            ["hooks"] = hookArray,
        };
    }

    /// <summary>
    /// Wraps a manifest as a source, named after its plugin identifier.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    public static PluginManifestSource Source(JsonObject manifest)
    {
        var id = (string)manifest["id"]!;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestTestJsonContext.Default.JsonObject);
        return new PluginManifestSource($"{id}.json", bytes);
    }

    /// <summary>
    /// Builds a JSON array of strings.
    /// </summary>
    /// <param name="values">The values, in order.</param>
    public static JsonArray Strings(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(Value(value));
        }

        return array;
    }

    /// <summary>
    /// Builds a JSON string value.
    /// </summary>
    /// <param name="value">The text.</param>
    public static JsonNode Value(string value) =>
        JsonNode.Parse("\"" + JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() + "\"")!;

    /// <summary>
    /// Builds a JSON boolean value.
    /// </summary>
    /// <param name="value">The flag.</param>
    public static JsonNode Value(bool value) => JsonNode.Parse(value ? "true" : "false")!;

    /// <summary>
    /// Builds a JSON integer value.
    /// </summary>
    /// <param name="value">The number.</param>
    public static JsonNode Value(int value) => JsonNode.Parse(value.ToString(CultureInfo.InvariantCulture))!;
}
