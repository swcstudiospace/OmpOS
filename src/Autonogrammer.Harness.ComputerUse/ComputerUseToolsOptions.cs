using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>Dependencies for <see cref="ComputerUseToolsComposition.AddTools"/>.</summary>
public sealed class ComputerUseToolsOptions
{
    /// <summary>Clock.</summary>
    public required TimeProvider Time { get; init; }

    /// <summary>Capture backend. Not used as a second injector.</summary>
    public required IComputerUseBackend Backend { get; init; }

    /// <summary>Local approval broker.</summary>
    public required ICoworkApprovalBroker Broker { get; init; }

    /// <summary>Account facts. The provider does not read a sign-in seam.</summary>
    public required IComputerUseAccessFacts Access { get; init; }

    /// <summary>Model id for long-edge overrides, or null.</summary>
    public string? ModelId { get; init; }
}
