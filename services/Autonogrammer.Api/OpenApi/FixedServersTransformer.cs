using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Autonogrammer.Api.OpenApi;

/// <summary>
/// Pins the document's server list to the relative root so the build-time committed
/// document and the served document are identical regardless of host or environment.
/// Also pins <c>info.version</c> to 1.1.0 (GAP-OP-1, additive minor): every operation
/// carries an <c>operationId</c> via <c>WithName</c> in source.
/// </summary>
internal sealed class FixedServersTransformer : IOpenApiDocumentTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        document.Servers = [new OpenApiServer { Url = "/" }];
        document.Info = new OpenApiInfo { Title = document.Info?.Title ?? "Autonogrammer.Api | v1", Version = "1.1.0" };
        return Task.CompletedTask;
    }
}
