using System.Text;

namespace OmpOS.Harness.AgentLoop.Tests.Stages;

/// <summary>Loads the trimmed vendor files under <c>TestData/Stages</c> and builds variants of them.</summary>
internal static class StageFixtures
{
    /// <summary>Gets the poll interval the tests build observers with.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    /// <summary>Reads a fixture file.</summary>
    /// <param name="name">The file name under <c>TestData/Stages</c>.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", "Stages", name));

    /// <summary>Reads a fixture file as text.</summary>
    /// <param name="name">The file name under <c>TestData/Stages</c>.</param>
    /// <returns>The text.</returns>
    public static string Text(string name) => Encoding.UTF8.GetString(Bytes(name));

    /// <summary>Encodes text as UTF-8.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Builds one <c>events.jsonl</c> line, with a terminator, for a run event.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="payloadJson">The payload object as JSON text.</param>
    /// <returns>The line bytes.</returns>
    public static byte[] EventLine(string type, string payloadJson) =>
        Utf8("{\"ts\": \"2026-09-15T19:55:13Z\", \"type\": \"" + type + "\", \"source\": \"swarm_run\", \"correlation_id\": null, \"task_id\": null, \"payload\": " + payloadJson + ", \"msg_id\": \"00000000000000000000000000000009\"}\n");
}
