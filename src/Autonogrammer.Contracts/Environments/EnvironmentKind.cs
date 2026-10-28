using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// Where the omp engine of a session runs. The numeric values are part of the stored and wire shape and never change; a new kind is only ever appended. A kind this build does not know is read as an undefined value (see <see cref="EnvironmentKindJsonConverter"/>), never as a failure of the whole document.
/// </summary>
[JsonConverter(typeof(EnvironmentKindJsonConverter))]
public enum EnvironmentKind
{
    /// <summary>On this machine: the default. omp is a child process of the application.</summary>
    Local = 0,

    /// <summary>On another machine reached through the user's own <c>ssh</c>: omp runs there as <c>omp acp</c> and its stdio is tunnelled by ssh.</summary>
    Ssh = 1,

    /// <summary>On an Autonogrammer cloud worker reached through the hosted API. The cloud profile data is defined by the cloud wave (ADR-0020).</summary>
    Cloud = 2,

    /// <summary>Inside a local podman container (a dev container, or any running container): omp runs there as <c>omp acp</c> through <c>podman exec</c>.</summary>
    Container = 3,

    /// <summary>Inside a WSL distribution on this Windows machine: omp runs there as <c>omp acp</c> through <c>wsl.exe</c>.</summary>
    Wsl = 4,
}
