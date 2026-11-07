using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Executor;

/// <summary>Result of one execution attempt.</summary>
/// <param name="Denial">Denial, or <see langword="null"/> when the primitives were injected.</param>
public sealed record ComputerExecutionResult(DenialCode? Denial)
{
    /// <summary><see langword="true"/> when the backend was injected.</summary>
    public bool Injected => Denial is null;
}
