using System.Text.Json.Serialization;
using Autonogrammer.Api.Endpoints;
using Autonogrammer.Contracts.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Autonogrammer.Api.Serialization;

/// <summary>
/// Source-generated JSON metadata for the API's own request/response DTOs.
/// Contract shapes come from <c>AutonogrammerJsonContext</c>; both resolvers are chained
/// into the HTTP JSON options so no reflection serialization is used.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(CreateSessionRequest))]
[JsonSerializable(typeof(PromptRequest))]
[JsonSerializable(typeof(PromptAcceptedResponse))]
[JsonSerializable(typeof(Session[]))]
internal sealed partial class ApiJsonContext : JsonSerializerContext
{
}
