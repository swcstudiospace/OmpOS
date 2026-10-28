using System.Globalization;
using System.Text.Json.Nodes;

namespace Autonogrammer.Harness.Acp.Tests;

/// <summary>
/// A scripted omp for the steering and option tests. It answers the captured handshake, optionally advertising
/// <c>_meta.steering.supported</c> in <c>initialize</c>, holds a <c>session/prompt</c> open until the test completes it, and answers
/// <c>_session/steering</c> and <c>session/set_config_option</c> with whatever the test scripted.
/// </summary>
internal sealed class ScriptedOmpPeer
{
    private long _promptId;

    /// <summary>Gets the sessionId that <c>session/new</c> answers with.</summary>
    public const string SessionId = AcpFixtures.CapturedSessionId;

    /// <summary>Gets or sets the value of <c>_meta.steering.supported</c> in the initialize result: null omits <c>_meta</c>, otherwise the raw JSON written for <c>supported</c>.</summary>
    public string? SteeringSupported { get; init; } = "true";

    /// <summary>Gets or sets the raw JSON <c>result</c> of every <c>_session/steering</c> request, or null to answer with <see cref="SteerError"/>.</summary>
    public string? SteerResult { get; set; }

    /// <summary>Gets or sets the JSON-RPC error code that answers <c>_session/steering</c> when <see cref="SteerResult"/> is null.</summary>
    public int SteerError { get; set; }

    /// <summary>Gets or sets the answer to <c>session/set_config_option</c>, or null to echo the captured options.</summary>
    public Func<byte[], IEnumerable<byte[]>>? OnSetConfigOption { get; set; }

    /// <summary>Gets or sets the raw JSON <c>configOptions</c> array <c>session/new</c> answers with, or null for the captured one.</summary>
    public string? ConfigOptions { get; init; }

    /// <summary>Answers one client frame.</summary>
    /// <param name="frame">The frame the client sent.</param>
    /// <returns>The frames the peer sends back.</returns>
    public IEnumerable<byte[]> Respond(byte[] frame)
    {
        switch (AcpFixtures.MethodOf(frame))
        {
            case "initialize":
                return [WithSteeringMeta(AcpFixtures.Reply(frame).Single())];
            case "session/new" when ConfigOptions is not null:
                return [Result(AcpFixtures.IdOf(frame), $"{{\"sessionId\":\"{SessionId}\",\"configOptions\":{ConfigOptions}}}")];
            case "session/prompt":
                _promptId = AcpFixtures.IdOf(frame);
                return [];
            case "_session/steering":
                return SteerResult is { } result
                    ? [Result(AcpFixtures.IdOf(frame), result)]
                    : [Error(AcpFixtures.IdOf(frame), SteerError, "Invalid params")];
            case "session/set_config_option" when OnSetConfigOption is not null:
                return OnSetConfigOption(frame);
            default:
                return AcpFixtures.Reply(frame);
        }
    }

    /// <summary>Builds the frame that ends the held prompt.</summary>
    /// <param name="stopReason">The stop reason, for example <c>end_turn</c>.</param>
    /// <returns>The prompt response.</returns>
    public byte[] EndPrompt(string stopReason) => Result(_promptId, $"{{\"stopReason\":\"{stopReason}\"}}");

    /// <summary>Builds a successful response.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="result">The raw JSON result.</param>
    /// <returns>The response frame.</returns>
    public static byte[] Result(long id, string result) =>
        AcpFixtures.Utf8(string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}"));

    /// <summary>Builds a JSON-RPC error response.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <returns>The error frame.</returns>
    public static byte[] Error(long id, int code, string message) =>
        AcpFixtures.Utf8(string.Create(CultureInfo.InvariantCulture, $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":{code},\"message\":\"{message}\"}}}}"));

    private byte[] WithSteeringMeta(byte[] initialize)
    {
        if (SteeringSupported is null)
        {
            return initialize;
        }

        var node = JsonNode.Parse(initialize)!;
        node["result"]!["_meta"] = new JsonObject { ["steering"] = new JsonObject { ["supported"] = JsonNode.Parse(SteeringSupported) } };
        return AcpFixtures.Utf8(node.ToJsonString());
    }
}
