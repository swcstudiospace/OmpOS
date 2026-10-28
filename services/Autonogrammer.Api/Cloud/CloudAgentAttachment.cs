using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>What a run needs to talk to an agent's worker: the agent, the WebSocket endpoint of its bridge, the directory omp starts in, and the redactor for its output.</summary>
/// <param name="Agent">The agent's record as read when the run attached.</param>
/// <param name="Endpoint">The <c>ws</c> or <c>wss</c> endpoint of the worker's <c>/acp</c> route.</param>
/// <param name="WorkingDirectory">The absolute directory on the worker that omp starts in.</param>
/// <param name="Redactor">The redactor that holds the agent's secret values.</param>
internal sealed record CloudAgentAttachment(CloudAgentRecord Agent, Uri Endpoint, string WorkingDirectory, CloudSecretRedactor Redactor);
