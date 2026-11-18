namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>Frames the regression tests build by hand: a model option list, its change notification and an agent message chunk.</summary>
internal static class AcpRegressionFrames
{
    /// <summary>More updates than one prompt's event buffer holds, so an unread turn overflows if its updates are kept.</summary>
    public const int TurnBufferOverflow = 5000;

    /// <summary>The model option list omp reports, with <paramref name="current"/> as the active model.</summary>
    /// <param name="current">The current model value.</param>
    /// <returns>The raw JSON array.</returns>
    public static string ModelOptions(string current) =>
        "[{\"id\":\"model\",\"name\":\"Model\",\"category\":\"model\",\"type\":\"select\",\"currentValue\":\""
        + current
        + "\",\"options\":[{\"value\":\"m/one\",\"name\":\"One\"},{\"value\":\"m/two\",\"name\":\"Two\"}]}]";

    /// <summary>A <c>config_option_update</c> notification that makes <paramref name="current"/> the active model.</summary>
    /// <param name="current">The new current model value.</param>
    /// <returns>The notification frame.</returns>
    public static byte[] ConfigUpdate(string current) =>
        AcpFixtures.Utf8(
            "{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + ScriptedOmpPeer.SessionId
            + "\",\"update\":{\"sessionUpdate\":\"config_option_update\",\"configOptions\":" + ModelOptions(current) + "}}}");

    /// <summary>An <c>agent_message_chunk</c> notification carrying <paramref name="text"/>.</summary>
    /// <param name="text">The chunk text.</param>
    /// <returns>The notification frame.</returns>
    public static byte[] Update(string text) =>
        AcpFixtures.Utf8(
            "{\"jsonrpc\":\"2.0\",\"method\":\"session/update\",\"params\":{\"sessionId\":\"" + ScriptedOmpPeer.SessionId
            + "\",\"update\":{\"sessionUpdate\":\"agent_message_chunk\",\"content\":{\"type\":\"text\",\"text\":\"" + text + "\"}}}}");
}
