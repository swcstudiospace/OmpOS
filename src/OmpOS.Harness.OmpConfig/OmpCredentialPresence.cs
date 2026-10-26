namespace OmpOS.Harness.OmpConfig;

/// <summary>Whether omp can resolve a credential for a provider, as <c>omp token &lt;provider&gt;</c> answers it.</summary>
public enum OmpCredentialPresence
{
    /// <summary>omp could not be asked or did not give a clear answer (not installed, timeout, unexpected exit code, invalid provider id).</summary>
    Unknown = 0,

    /// <summary><c>omp token</c> exited 0: omp resolves a credential for the provider.</summary>
    Present = 1,

    /// <summary><c>omp token</c> exited 1: omp has no active credential for the provider.</summary>
    Absent = 2,
}
