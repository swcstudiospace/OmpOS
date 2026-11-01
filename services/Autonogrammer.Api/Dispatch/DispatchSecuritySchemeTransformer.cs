using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Autonogrammer.Api.Dispatch;

/// <summary>
/// Declares <c>components.securitySchemes.DispatchDevice</c> and adds that scheme to the connect operation only.
/// No other operation gains a security entry from this transformer.
/// </summary>
internal sealed class DispatchSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    /// <summary>Gets the scheme name.</summary>
    public const string SchemeName = "DispatchDevice";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The device credential. Accepted only on the connect route.",
        };

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (path, item) in document.Paths)
        {
            if (item.Operations is null)
            {
                continue;
            }

            foreach (var (method, operation) in item.Operations)
            {
                if (IsConnect(path, method))
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement
                        {
                            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
                        },
                    ];
                }
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsConnect(string path, HttpMethod method) =>
        method == HttpMethod.Get
        && path.Contains("/dispatch/devices/", StringComparison.Ordinal)
        && path.EndsWith("/connect", StringComparison.Ordinal);
}
