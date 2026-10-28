using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Autonogrammer.Api.OpenApi;

/// <summary>
/// Pins the document's server list to the relative root so the build-time committed
/// document and the served document are identical regardless of host or environment.
/// </summary>
internal sealed class FixedServersTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        document.Servers = [new OpenApiServer { Url = "/" }];
        return Task.CompletedTask;
    }
}
