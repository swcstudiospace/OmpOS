namespace Autonogrammer.Contracts.Sandboxes;

/// <summary>
/// Isolation strength of a sandbox.
/// </summary>
public enum Isolation
{
    /// <summary>OS process boundary.</summary>
    Process,

    /// <summary>Container boundary (podman).</summary>
    Container,

    /// <summary>Virtual-machine boundary.</summary>
    Vm,

    /// <summary>Attested-enclave boundary.</summary>
    Enclave,
}
