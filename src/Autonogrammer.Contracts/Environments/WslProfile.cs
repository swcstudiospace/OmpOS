namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// How to reach a WSL distribution on this Windows machine: the distribution, the user in it and the workspace path inside it. The profile holds
/// references only and has no property that could carry a secret: Autonogrammer passes secrets to the omp in the distribution on the stdin of <c>wsl.exe</c>
/// and never stores them. Use <see cref="EnvironmentProfileValidator"/> before the profile is stored or used to start a process.
/// </summary>
public sealed record WslProfile
{
    /// <summary>Gets the distribution name, as <c>wsl.exe --list</c> prints it. It matches <see cref="ContainerProfileRules.IsValidWslDistribution"/>.</summary>
    public required string Distro { get; init; }

    /// <summary>Gets the user omp runs as in the distribution, or <see langword="null"/> for the distribution's default user.</summary>
    public string? User { get; init; }

    /// <summary>Gets the absolute POSIX path of the workspace inside the distribution: the working directory of <c>omp acp</c>.</summary>
    public required string WorkspacePath { get; init; }

    /// <summary>
    /// Gets the omp executable inside the distribution: an absolute path or a bare command name. <see langword="null"/> means <c>omp</c> resolved from the
    /// distribution's <c>PATH</c> (extended by <c>~/.bun/bin</c>, <c>~/.local/bin</c> and <c>/usr/local/bin</c>).
    /// </summary>
    public string? OmpPath { get; init; }
}
