using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>The answer of <see cref="DevContainerResolver"/>: what the definition says, which containers belong to it, and the profile when exactly one is running.</summary>
public sealed record DevContainerResolution
{
    /// <summary>Gets what was found.</summary>
    public required DevContainerOutcome Outcome { get; init; }

    /// <summary>Gets the definition, or <see langword="null"/> when none could be read.</summary>
    public DevContainerDefinition? Definition { get; init; }

    /// <summary>Gets the containers podman lists for the folder (running or not), in order.</summary>
    public IReadOnlyList<ContainerSummary> Containers { get; init; } = [];

    /// <summary>Gets the profile of the one running container, or <see langword="null"/> unless <see cref="Outcome"/> is <see cref="DevContainerOutcome.Found"/>.</summary>
    public ContainerProfile? Profile { get; init; }

    /// <summary>Gets the sentence that says what to do next (always set when <see cref="Outcome"/> is not <see cref="DevContainerOutcome.Found"/>).</summary>
    public string? FixText { get; init; }

    /// <summary>Gets the podman diagnosis when <see cref="Outcome"/> is <see cref="DevContainerOutcome.RuntimeProblem"/>.</summary>
    public RemoteDiagnosis? Diagnosis { get; init; }
}
