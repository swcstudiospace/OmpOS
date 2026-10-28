namespace OmpOS.Contracts.Environments;

/// <summary>Where the omp engine of a session runs.</summary>
public enum EnvironmentKind
{
    /// <summary>On this machine: the default. omp is a child process of the application.</summary>
    Local,

    /// <summary>On another machine reached through the user's own <c>ssh</c>: omp runs there as <c>omp acp</c> and its stdio is tunnelled by ssh.</summary>
    Ssh,

    /// <summary>On an OmpOS cloud worker reached through the hosted API. The cloud profile data is defined by the cloud wave (ADR-0020).</summary>
    Cloud,
}
