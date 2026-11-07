using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>A consistent view of the store while a compare-and-swap decision runs.</summary>
public interface IArtifactMutation
{
    /// <summary>Finds an artifact header.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>The header, or null.</returns>
    ArtifactRecord? Find(string artifactId);

    /// <summary>Lists versions in number order. The content arrays are the stored instances.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <returns>The versions.</returns>
    IReadOnlyList<ArtifactVersionRecord> Versions(string artifactId);

    /// <summary>Inserts a new artifact and its first version, plus an empty grant list.</summary>
    /// <param name="artifact">The header.</param>
    /// <param name="version">Version 1.</param>
    void Insert(ArtifactRecord artifact, ArtifactVersionRecord version);

    /// <summary>Appends a write-once version. The caller has already checked the base.</summary>
    /// <param name="version">The new version.</param>
    void Append(ArtifactVersionRecord version);

    /// <summary>Hard-deletes the artifact, every version and every grant.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    void Delete(string artifactId);

    /// <summary>Gets the account usage counters.</summary>
    /// <returns>Artifact count and total stored bytes.</returns>
    ArtifactUsage Usage();
}
