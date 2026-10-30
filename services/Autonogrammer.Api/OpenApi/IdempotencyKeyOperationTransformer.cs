using Autonogrammer.Api.Idempotency;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Autonogrammer.Api.OpenApi;

/// <summary>
/// Documents the optional <c>Idempotency-Key</c> request header on <c>POST /sessions/{id}/prompts</c> as an OpenAPI header
/// parameter. Every other operation is left unchanged.
/// </summary>
internal sealed class IdempotencyKeyOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var description = context.Description;
        if (!string.Equals(description.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(description.RelativePath?.TrimStart('/'), "sessions/{id}/prompts", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyKeyHeader.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Description = "Optional client key that makes a prompt submission idempotent within its session. A repeat with the same key and the same prompt returns the original 202 response; the same key with a different prompt returns 409. A key still in progress returns 409. Keys are 1 to 128 visible ASCII characters.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = IdempotencyKeyHeader.MaxLength,
                Pattern = "^[!-~]{1,128}$",
            },
        });
        return Task.CompletedTask;
    }
}
