using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Autonogrammer.Api.OpenApi;

/// <summary>
/// Declares how callers authenticate. The document names the <c>bearerAuth</c> scheme (an HTTP bearer JWT) with a document-level
/// requirement, and the <c>DispatchDevice</c> scheme that a dispatch device connection will reference. Each operation whose endpoint
/// metadata carries <see cref="IAllowAnonymous"/> gets an explicit empty <c>security</c> list; every other operation gets the 401 and
/// 403 answers of the authentication boundary. The anonymous list is derived from endpoint metadata, never from a path list.
/// </summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>Gets the name of the bearer scheme that protects the API.</summary>
    public const string BearerSchemeName = "bearerAuth";

    /// <summary>Gets the name of the scheme a dispatch device connection authenticates with.</summary>
    public const string DispatchDeviceSchemeName = "DispatchDevice";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[BearerSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A bearer token issued by the identity provider the deployment trusts (or the owner token on a single-operator deployment).",
        };
        document.Components.SecuritySchemes[DispatchDeviceSchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Description = "The long-lived credential of a paired dispatch device. It is accepted only on the device connect route and never as an access token.",
        };
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = [],
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
        if (anonymous)
        {
            operation.Security = [];
            return Task.CompletedTask;
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "The request carries no valid bearer token." });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "The caller is authenticated but not permitted to use this deployment." });
        return Task.CompletedTask;
    }
}
