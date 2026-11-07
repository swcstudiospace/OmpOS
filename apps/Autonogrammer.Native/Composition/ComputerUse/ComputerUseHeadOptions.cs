using Autonogrammer.Cowork;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Presentation.Cowork;

namespace Autonogrammer.Native.Composition.ComputerUse;

/// <summary>What <see cref="ComputerUseHeadComposition.Build"/> needs. No container.</summary>
internal sealed class ComputerUseHeadOptions
{
    /// <summary>Gets the platform the head is running on.</summary>
    public required ComputerPlatform Platform { get; init; }

    /// <summary>Gets the state directory for <c>computer-use/settings.json</c>.</summary>
    public required string StateDirectory { get; init; }

    /// <summary>
    /// Gets a borrowed settings store, or null to create a file store owned by the result. A supplied store remains
    /// caller-owned, even when it implements <see cref="IDisposable"/>.
    /// </summary>
    public IComputerUseSettingsStore? Settings { get; init; }

    /// <summary>Gets the Cowork account gate. The access-facts adapter reads only this.</summary>
    public required ICoworkAccountGate AccountGate { get; init; }

    /// <summary>Gets the header-end slot model, or null. Phase 69 owns the real SurfaceHost.</summary>
    public SurfaceChromeModel? Chrome { get; init; }
}
