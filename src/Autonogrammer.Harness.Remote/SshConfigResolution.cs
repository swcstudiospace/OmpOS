using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>The outcome of resolving one alias with <c>ssh -G</c>: the configuration, or the reason there is none.</summary>
public sealed record SshConfigResolution
{
    /// <summary>Gets the configuration, or <see langword="null"/> when ssh could not produce one.</summary>
    public SshEffectiveConfig? Config { get; init; }

    /// <summary>Gets why there is no configuration, or <see langword="null"/> when <see cref="Config"/> is set.</summary>
    public RemoteDiagnosis? Failure { get; init; }
}
