using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;

namespace OmpOS.Harness.AgentLoop.Tests.Connection;

/// <summary>Builds connection reports and plugin lists for the connection tests and writes the report where the source reads it.</summary>
internal static class ConnectionTestSupport
{
    /// <summary>Gets every tool name the built-in plugins are expected to register.</summary>
    public static IReadOnlyList<string> AllTools { get; } = [.. OmpPluginCatalog.BuiltIn.SelectMany(static plugin => plugin.ExpectedTools)];

    /// <summary>Gets every command name the built-in plugins are expected to register.</summary>
    public static IReadOnlyList<string> AllCommands { get; } = [.. OmpPluginCatalog.BuiltIn.SelectMany(static plugin => plugin.ExpectedCommands)];

    /// <summary>Returns the descriptors with the given identifiers, in the order given.</summary>
    /// <param name="ids">The plugin identifiers.</param>
    /// <returns>The descriptors.</returns>
    public static IReadOnlyList<OmpPluginDescriptor> Plugins(params string[] ids) =>
        [.. ids.Select(static id => OmpPluginCatalog.BuiltIn.Single(plugin => string.Equals(plugin.Id, id, StringComparison.Ordinal)))];

    /// <summary>Renders a connection report as the omp plugin writes it.</summary>
    /// <param name="tools">The tool names.</param>
    /// <param name="commands">The command names.</param>
    /// <param name="commandsAvailable">The value of <c>commandsAvailable</c>.</param>
    /// <param name="version">The schema version.</param>
    /// <returns>The JSON text.</returns>
    public static string Report(IEnumerable<string> tools, IEnumerable<string> commands, bool commandsAvailable = true, int version = 1) =>
        "{\"v\":" + version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ",\"at\":\"2026-10-26T12:00:00.000Z\",\"pid\":4242,\"mode\":\"tui\",\"omp\":{\"version\":null},\"tools\":" + Names(tools)
        + ",\"commands\":" + Names(commands) + ",\"commandsAvailable\":" + (commandsAvailable ? "true" : "false") + "}";

    /// <summary>Renders a report that names everything every built-in plugin is expected to register.</summary>
    /// <returns>The JSON text.</returns>
    public static string FullReport() => Report(AllTools, AllCommands);

    /// <summary>Writes the connection report of a scope, creating its directory.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The file content.</param>
    public static void Write(StageObservationScope scope, string content) => Write(scope, System.Text.Encoding.UTF8.GetBytes(content));

    /// <summary>Writes the connection report of a scope, creating its directory.</summary>
    /// <param name="scope">The scope.</param>
    /// <param name="content">The file content.</param>
    public static void Write(StageObservationScope scope, byte[] content)
    {
        var path = SessionStateLayout.ConnectionFile(scope.SessionStateDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static string Names(IEnumerable<string> names) => "[" + string.Join(",", names.Select(static name => "\"" + name + "\"")) + "]";
}
