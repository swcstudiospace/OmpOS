namespace OmpOS.Harness.Remote.Tests;

/// <summary>How a <see cref="FakeContainerHost"/> behaves.</summary>
internal sealed record FakeContainerHostOptions
{
    /// <summary>Gets the failure mode of the fake podman (see the header of <c>fake-podman</c>); empty is a healthy runtime.</summary>
    public string Mode { get; init; } = string.Empty;

    /// <summary>Gets whether the fake podman prints a login banner on stdout before the command of an exec.</summary>
    public bool Banner { get; init; }

    /// <summary>Gets whether a fake omp is installed under the container's <c>~/.bun/bin</c>.</summary>
    public bool InstallOmp { get; init; } = true;

    /// <summary>Gets the version the fake omp prints.</summary>
    public string OmpVersion { get; init; } = "18.12.6";

    /// <summary>Gets whether the fake omp prints a non-JSON line right after its first frame.</summary>
    public bool NoiseAfterHandshake { get; init; }

    /// <summary>Gets the text a fake <c>uname -sm</c> prints, or <see langword="null"/> to use the real one.</summary>
    public string? UnameOutput { get; init; } = "Linux x86_64";

    /// <summary>Gets the state of the container (<c>running</c>, <c>exited</c>).</summary>
    public string State { get; init; } = "running";

    /// <summary>Gets the users that exist in the container, or <see langword="null"/> when every user exists.</summary>
    public IReadOnlyList<string>? Users { get; init; } = ["dev", "root"];

    /// <summary>Gets whether <c>podman start</c> fails.</summary>
    public bool StartFails { get; init; }

    /// <summary>Gets extra environment variables the wrapper sets for the fake podman and the fake omp.</summary>
    public IReadOnlyDictionary<string, string> ExtraEnvironment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
