using OmpOS.Sandbox;

namespace OmpOS.Api.Cloud;

/// <summary>Maps the worker's HTTP base address (from the environment client) to its WebSocket endpoint: <c>http</c> becomes <c>ws</c>, <c>https</c> becomes <c>wss</c>, and the path is <c>/acp</c>. A base address with a credential, a query or another scheme is refused.</summary>
internal sealed class WorkerEndpoints : ICloudWorkerEndpoints
{
    /// <inheritdoc />
    public Uri Resolve(CloudContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        var baseUri = container.WorkerBaseUri ?? throw new InvalidOperationException("The worker has no base address.");
        if (!baseUri.IsAbsoluteUri || baseUri.UserInfo.Length > 0 || baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("The worker base address must be an absolute URI without a credential, a query or a fragment.");
        }

        var scheme = baseUri.Scheme switch
        {
            "http" => "ws",
            "https" => "wss",
            _ => throw new InvalidOperationException("The worker base address must use http or https."),
        };
        return new UriBuilder(baseUri) { Scheme = scheme, Port = baseUri.IsDefaultPort ? -1 : baseUri.Port, Path = "/acp" }.Uri;
    }
}
