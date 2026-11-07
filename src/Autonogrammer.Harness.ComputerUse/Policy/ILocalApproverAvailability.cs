using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Policy;

/// <summary>Optional probe. When implemented and false, the gate denies with <see cref="DenialCode.NoLocalApprover"/>.</summary>
public interface ILocalApproverAvailability
{
    /// <summary><see langword="true"/> when a local user can answer.</summary>
    bool HasLocalApprover { get; }
}
