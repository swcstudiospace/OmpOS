namespace OmpOS.Harness.Remote.Containers;

/// <summary>What <see cref="DevContainerResolver"/> found for a project folder.</summary>
public enum DevContainerOutcome
{
    /// <summary>The folder has no <c>devcontainer.json</c>.</summary>
    NoDefinition,

    /// <summary>The definition could not be read: it is not JSON (after comments and trailing commas), is too large, or its workspace folder is unusable.</summary>
    InvalidDefinition,

    /// <summary>podman could not be run, or could not list containers.</summary>
    RuntimeProblem,

    /// <summary>No container carries the label of this folder: nothing was created yet. OmpOS never creates one.</summary>
    NotCreated,

    /// <summary>A container exists for the folder and it is not running.</summary>
    NotRunning,

    /// <summary>More than one running container carries the label: the user must pick one.</summary>
    Ambiguous,

    /// <summary>Exactly one running container carries the label and the profile is ready.</summary>
    Found,
}
