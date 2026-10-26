namespace OmpOS.Api.OpenApi;

/// <summary>
/// Runtime OpenAPI document configuration. The build-time document (committed to
/// <c>docs/api/openapi.json</c>) is generated with <c>OpenApi3_1</c>; this keeps the
/// served <c>/openapi/v1.json</c> on the same version so the two stay byte-comparable.
/// </summary>
internal static class OpenApiConfig
{
    /// <summary>Adds the versioned OpenAPI document services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddOmpOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOpenApi("v1", options =>
        {
            options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;
            options.AddDocumentTransformer<FixedServersTransformer>();
            options.AddOperationTransformer<IdempotencyKeyOperationTransformer>();
        });
        return services;
    }
}
