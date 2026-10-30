using System.Text.Json.Serialization;

namespace Autonogrammer.Api.Cloud;

/// <summary>Source-generated JSON metadata for the GitHub REST shapes in <see cref="GitHubWire"/>. Unknown members in a response are ignored.</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GitHubWire.InstallationTokenRequest))]
[JsonSerializable(typeof(GitHubWire.InstallationTokenResponse))]
[JsonSerializable(typeof(GitHubWire.InstallationResponse))]
[JsonSerializable(typeof(GitHubWire.RepositoryResponse))]
[JsonSerializable(typeof(GitHubWire.ContentResponse))]
[JsonSerializable(typeof(GitHubWire.CreatePullRequestBody))]
[JsonSerializable(typeof(GitHubWire.PullRequestResponse))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext
{
}
