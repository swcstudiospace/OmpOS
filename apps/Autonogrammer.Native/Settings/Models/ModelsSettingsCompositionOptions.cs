using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Native.Settings.Models;

/// <summary>What <see cref="ModelsSettingsComposition.Create"/> needs: where Autonogrammer keeps its own state, how omp is found and where its configuration lives, and the seams a test replaces.</summary>
internal sealed class ModelsSettingsCompositionOptions
{
    /// <summary>Gets the Autonogrammer state directory. The provider key store (<c>secrets/secrets.json</c>) and the details of custom providers (<c>custom-providers.json</c>) are kept below it.</summary>
    public required string StateRoot { get; init; }

    /// <summary>Gets the omp executable, or null to search <c>PATH</c> and the known install places. Ignored when <see cref="OmpLocator"/> is set.</summary>
    public string? OmpPath { get; init; }

    /// <summary>Gets how omp is located, or null to build it from <see cref="OmpPath"/> and the environment.</summary>
    public OmpLocatorOptions? OmpLocator { get; init; }

    /// <summary>Gets omp's agent directory (<c>models.yml</c>, <c>config.yml</c>, the credential database), or null for the one omp itself uses.</summary>
    public string? AgentDirectory { get; init; }

    /// <summary>Gets the home directory given to omp as <c>HOME</c>, or null for the host's. Intended for tests and sandboxes.</summary>
    public string? Home { get; init; }

    /// <summary>Gets the clock for command timeouts, backup names, the probe and the error mapper, or null for the system clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the handler of the HTTP client that probes local endpoints, or null for a client without redirects and cookies. A handler given here stays owned by the caller.</summary>
    public HttpMessageHandler? ProbeHandler { get; init; }

    /// <summary>Gets the time one omp invocation may run, or null for the default of 30 seconds.</summary>
    public TimeSpan? CommandTimeout { get; init; }

    /// <summary>Gets the store that keeps the key of each custom provider, or null for the owner-only file below <see cref="StateRoot"/>.</summary>
    public ISecretStore? SecretStore { get; init; }
}
