namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The outcome of turning a catalogue route into a managed provider: the provider, or a plain sentence that names what is missing or wrong.</summary>
/// <param name="Provider">The provider, ready for the models editor, or null when the route cannot be written yet.</param>
/// <param name="Problem">A sentence for the user when <paramref name="Provider"/> is null, or null.</param>
public sealed record OmpCatalogueBlockResult(OmpManagedProvider? Provider, string? Problem)
{
    /// <summary>Gets a value indicating whether a provider was built.</summary>
    public bool Succeeded => Provider is not null;
}
