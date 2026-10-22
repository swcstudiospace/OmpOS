using System.Text;
using System.Text.Json;
using OmpOS.Harness.Plugins;

namespace OmpOS.Native.Tests.Hooks;

/// <summary>Builds plugin manifests as JSON text for the hooks tests, so each test states only the hooks it needs.</summary>
internal static class HookManifests
{
    /// <summary>Builds one hook declaration as JSON.</summary>
    /// <param name="trigger">The trigger name, such as <c>beforeAction</c>.</param>
    /// <param name="command">The argument list; the first element is the executable.</param>
    /// <param name="workingDirectory">The absolute working directory.</param>
    /// <param name="blocking">Whether the hook may deny the action.</param>
    /// <returns>The hook as a JSON object.</returns>
    public static string Hook(string trigger, string[] command, string workingDirectory, bool blocking = false) =>
        "{\"trigger\":" + Text(trigger)
        + ",\"command\":[" + string.Join(',', command.Select(Text))
        + "],\"workingDirectory\":" + Text(workingDirectory)
        + ",\"blocking\":" + (blocking ? "true" : "false") + "}";

    /// <summary>Builds a plugin manifest source with no capabilities or permissions.</summary>
    /// <param name="pluginId">The plugin identifier.</param>
    /// <param name="hooks">The hooks, in manifest order, as JSON objects.</param>
    /// <returns>The manifest source.</returns>
    public static PluginManifestSource Plugin(string pluginId, params string[] hooks)
    {
        var json = "{\"id\":" + Text(pluginId)
            + ",\"version\":\"1.0.0\",\"apiVersion\":1,\"capabilities\":[],\"permissions\":[],\"hooks\":["
            + string.Join(',', hooks) + "]}";
        return new PluginManifestSource(pluginId + ".json", Encoding.UTF8.GetBytes(json));
    }

    private static string Text(string value) => "\"" + JsonEncodedText.Encode(value) + "\"";
}
