using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cloud;

/// <summary>
/// Source-generated JSON metadata for the cloud agent shapes. Enums are written as strings. The context also serves the durable
/// store, which keeps repositories, secret names, pull requests and the error in JSON columns.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CreateCloudAgentRequest))]
[JsonSerializable(typeof(CloudAgentResponse))]
[JsonSerializable(typeof(CloudAgentListResponse))]
[JsonSerializable(typeof(CloudAgentStatus))]
[JsonSerializable(typeof(CloudRepository))]
[JsonSerializable(typeof(CloudRepository[]))]
[JsonSerializable(typeof(CloudAgentError))]
[JsonSerializable(typeof(CloudPromptRequest))]
[JsonSerializable(typeof(CloudPromptAccepted))]
[JsonSerializable(typeof(CloudPullRequest))]
[JsonSerializable(typeof(CloudPullRequest[]))]
[JsonSerializable(typeof(CreateCloudPullRequestRequest))]
[JsonSerializable(typeof(CloudSecretValueRequest))]
[JsonSerializable(typeof(CloudSecretInfo))]
[JsonSerializable(typeof(CloudSecretListResponse))]
[JsonSerializable(typeof(CloudLimitsResponse))]
[JsonSerializable(typeof(CloudAgentRecord))]
[JsonSerializable(typeof(string[]))]
public sealed partial class CloudJsonContext : JsonSerializerContext
{
}
