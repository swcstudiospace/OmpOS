using Autonogrammer.Sandbox;

namespace Autonogrammer.Api.Cloud;

/// <summary>Turns a worker container into the WebSocket endpoint of its ACP bridge. A test replaces it to point at an in-process worker.</summary>
internal interface ICloudWorkerEndpoints
{
    /// <summary>Returns the <c>ws</c> or <c>wss</c> URI of the worker's <c>/acp</c> route.</summary>
    /// <param name="container">The worker container.</param>
    /// <returns>The endpoint.</returns>
    /// <exception cref="InvalidOperationException">The container has no usable base address.</exception>
    Uri Resolve(CloudContainer container);
}
