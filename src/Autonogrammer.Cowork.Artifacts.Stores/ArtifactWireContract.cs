using System.Security.Cryptography;
using System.Text.Json.Serialization;


namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// The single client and server contract for artifact sync. Bodies are raw version bytes. Metadata-only sync never sends those bytes.
/// </summary>
public static partial class ArtifactWireContract
{
    /// <summary>The media type of a version body.</summary>
    public const string MediaType = "application/octet-stream";

    /// <summary>The media type of the metadata-only form used while sync is off.</summary>
    public const string MetadataMediaType = "application/json";

    /// <summary>The relative PUT path prefix. The version number is appended.</summary>
    public const string VersionsPathPrefix = "cowork/artifacts/";

    /// <summary>Header carrying the lowercase hex SHA-256 of the body, or of the version when the body is metadata only.</summary>
    public const string ContentSha256Header = "X-Artifact-Content-Sha256";

    /// <summary>Header naming the artifact identifier.</summary>
    public const string ArtifactIdHeader = "X-Artifact-Id";

    /// <summary>Header naming the version number.</summary>
    public const string VersionHeader = "X-Artifact-Version";

    /// <summary>Header naming the artifact kind.</summary>
    public const string KindHeader = "X-Artifact-Kind";

    /// <summary>Same hash for an existing number. No new write.</summary>
    public const int StatusSameHash = 200;

    /// <summary>Different bytes for an existing number. The desktop forks; nothing is merged.</summary>
    public const int StatusDivergence = 409;

    /// <summary>The body exceeds the store cap. Maps to <c>too_large</c>.</summary>
    public const int StatusTooLarge = 413;

    /// <summary>The account is rate limiting. Honour <see cref="RetryAfterHeader"/>.</summary>
    public const int StatusRateLimited = 429;

    /// <summary>The bearer was rejected. Sync stops and reports signed out.</summary>
    public const int StatusSignedOut = 401;

    /// <summary>Present on a 200 that stored nothing because the hash already matched.</summary>
    public const string SameHashHeader = "X-Artifact-Same-Hash";

    /// <summary>The standard retry hint header.</summary>
    public const string RetryAfterHeader = "Retry-After";

    /// <summary>The database and service cap, in bytes.</summary>
    public const int MaxContentBytes = 5242880;

    /// <summary>Builds the relative PUT path for one version.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <returns>The relative path.</returns>
    public static string VersionPath(string artifactId, int number) =>
        VersionsPathPrefix + artifactId + "/versions/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Hashes bytes as lowercase hex SHA-256.</summary>
    /// <param name="content">The bytes.</param>
    /// <returns>The hash.</returns>
    public static string Hash(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(content, hash);
        return Convert.ToHexStringLower(hash);
    }


    /// <summary>A grant bound to a version hash. The sync payload never includes content, a title or an account id.</summary>
    /// <param name="ArtifactId">The artifact identifier.</param>
    /// <param name="Number">The version number.</param>
    /// <param name="ContentSha256">The version hash.</param>
    /// <param name="Origins">The origins the grant allows.</param>
    /// <param name="Scope">The grant scope.</param>
    /// <param name="Device">The device the grant names.</param>
    /// <param name="GrantedAt">When the grant was bound.</param>
    internal sealed record Grant(string ArtifactId, int Number, string ContentSha256, IReadOnlyList<string> Origins, string Scope, string Device, DateTimeOffset GrantedAt);

    /// <summary>Metadata that may leave the device while sync is off. It has no body bytes.</summary>
    internal sealed class SyncMetadata
    {
        /// <summary>Gets or sets the artifact identifier.</summary>
        public required string Id { get; set; }

        /// <summary>Gets or sets the title.</summary>
        public required string Title { get; set; }

        /// <summary>Gets or sets the kind name.</summary>
        public required string Kind { get; set; }

        /// <summary>Gets or sets how many versions exist.</summary>
        public required int VersionCount { get; set; }

        /// <summary>Gets or sets the first version time.</summary>
        public required DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets the latest version time.</summary>
        public required DateTimeOffset UpdatedAt { get; set; }

        /// <summary>Gets or sets the version hashes, in number order.</summary>
        public required IReadOnlyList<string> Hashes { get; set; }

        /// <summary>Gets or sets grants bound to those hashes.</summary>
        public IReadOnlyList<Grant> Grants { get; set; } = [];
    }

    /// <summary>Source-generated JSON for the metadata-only sync form.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(SyncMetadata))]
    [JsonSerializable(typeof(List<Grant>))]
    [JsonSerializable(typeof(List<string>))]
    internal sealed partial class JsonContext : JsonSerializerContext
    {
    }
}
