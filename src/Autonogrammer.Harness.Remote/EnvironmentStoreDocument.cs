using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// The persisted environment profiles. <see cref="Version"/> is bumped when the shape changes; a file with another version is treated as damaged.
/// The document holds references only (aliases, identity file paths): <see cref="EnvironmentProfile"/> has no property that could carry a secret.
/// </summary>
internal sealed class EnvironmentStoreDocument
{
    public int Version { get; set; }

    public List<EnvironmentProfile> Profiles { get; set; } = [];

    public Dictionary<string, DateTimeOffset> LastUsed { get; set; } = new(StringComparer.Ordinal);
}
