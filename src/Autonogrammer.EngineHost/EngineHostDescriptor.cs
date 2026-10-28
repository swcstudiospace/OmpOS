using Autonogrammer.Contracts.Sandboxes;

namespace Autonogrammer.EngineHost;

/// <summary>
/// Describes an engine host's placement and boundary. The UI renders the real boundary from
/// these values (reusing the Contracts <see cref="Isolation"/> and <see cref="Attestation"/>
/// enums); hosts report exactly what they were constructed with.
/// </summary>
public sealed record EngineHostDescriptor
{
    /// <summary>Gets where the engine runs.</summary>
    public required EngineHostKind Kind { get; init; }

    /// <summary>Gets the isolation boundary around the engine.</summary>
    public required Isolation Isolation { get; init; }

    /// <summary>Gets the attestation evidence the boundary produces.</summary>
    public required Attestation Attestation { get; init; }
}
