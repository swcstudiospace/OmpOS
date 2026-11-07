using Autonogrammer.Cowork.Sessions;

namespace Autonogrammer.ArchitectureTests.CoworkFixtures;

/// <summary>Negative-control consumer for rules 2 and 5: it names a Cowork registry and a type called SessionRegistry, so both rules must report it.</summary>
internal abstract class CoworkRegistryConsumer
{
    public CoworkSessionRegistry? Registry { get; set; }

    public SessionRegistry? Forbidden { get; set; }
}
