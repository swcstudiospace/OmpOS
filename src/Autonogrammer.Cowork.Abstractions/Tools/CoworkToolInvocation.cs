using System.Text.Json;

namespace Autonogrammer.Cowork.Abstractions;

/// <summary>One call the host hands a provider. Origin comes from the lease, not from the arguments.</summary>
/// <param name="SessionId">The session id.</param>
/// <param name="CallId">The host-assigned call id.</param>
/// <param name="ToolName">The tool name.</param>
/// <param name="Arguments">The JSON object the model supplied.</param>
/// <param name="Origin">The turn origin from the lease.</param>
public sealed record CoworkToolInvocation(string SessionId, string CallId, string ToolName, JsonElement Arguments, CoworkTurnOrigin Origin);
