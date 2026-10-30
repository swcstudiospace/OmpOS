using System.Globalization;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// The connection test of a container environment. It checks, in order and each with a typed diagnosis and the text that fixes it: podman is installed
/// (<c>podman --version</c>), the container exists and is running (<c>podman container inspect --format '{{.State.Status}}'</c>; a stopped container gets the
/// <c>podman start</c> fix and <see cref="StartAsync"/> runs it when the user asks), the user exists in it, the workspace folder exists and is writable, omp
/// (and Bun) are there, and the platform is the verified one. It never installs anything, never starts a container by itself and never answers a prompt.
/// </summary>
public sealed class ContainerPreflight
{
    private const string RunningStatus = "running";

    private readonly PodmanClientOptions _clientOptions;
    private readonly RemotePreflightOptions _options;
    private readonly SshProcessRunner _runner;
    private readonly PodmanExecCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="ContainerPreflight"/> class.</summary>
    /// <param name="clientOptions">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="options">What the container must provide, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    public ContainerPreflight(PodmanClientOptions? clientOptions = null, RemotePreflightOptions? options = null, TimeProvider? clock = null)
    {
        _clientOptions = clientOptions ?? new PodmanClientOptions();
        _options = options ?? new RemotePreflightOptions();
        _runner = new SshProcessRunner(clock);
        _builder = new PodmanExecCommandBuilder(_clientOptions);
    }

    /// <summary>Tests a container environment.</summary>
    /// <param name="profile">The container. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="cancellationToken">Cancels the test and stops podman.</param>
    /// <returns>The report: what was found and what must be fixed. A failing container is a report, not an exception.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public async Task<RemotePreflightReport> RunAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);

        var version = await _runner.RunAsync(_builder.BuildVersion(profile.PodmanPath), _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (version.StartFailure is not null)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.ContainerRuntimeMissing, version.StartFailure));
        }

        var inspect = await _runner.RunAsync(_builder.BuildInspect(profile), _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (inspect.TimedOut)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.ContainerRuntimeUnavailable, TimeoutText()));
        }

        if (inspect.ExitCode != 0)
        {
            return ProbeInterpreter.Failed(ExplainFailure(profile, inspect.ExitCode, inspect.StandardError));
        }

        var status = inspect.StandardOutput.Trim();
        if (!string.Equals(status, RunningStatus, StringComparison.Ordinal))
        {
            return ProbeInterpreter.Failed(NotRunning(profile, "The container state is '" + SafeStatus(status) + "'."));
        }

        var probe = _builder.BuildProbe(profile, RemotePreflight.BuildProbeScript(ToProbeProfile(profile)));
        var result = await _runner.RunAsync(probe, _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.ContainerRuntimeMissing, result.StartFailure));
        }

        if (result.TimedOut)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.ContainerRuntimeUnavailable, TimeoutText()));
        }

        if (!RemotePreflight.ParseProbe(result.StandardOutput).ContainsKey("shell"))
        {
            return ProbeInterpreter.Failed(ExplainFailure(profile, result.ExitCode, result.StandardError));
        }

        return ProbeInterpreter.Interpret(EnvironmentKind.Container, profile.WorkspaceFolder, _options, result.StandardOutput, result.StandardError);
    }

    /// <summary>Starts a stopped container with <c>podman start</c>. Call it only when the user chose to; the connection test never does.</summary>
    /// <param name="profile">The container.</param>
    /// <param name="cancellationToken">Cancels the start and stops podman.</param>
    /// <returns>Whether podman started the container, and the diagnosis when it did not.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public async Task<ContainerStartResult> StartAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);
        var result = await _runner.RunAsync(_builder.BuildStart(profile), _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return new ContainerStartResult(false, Diagnose(RemoteDiagnosisKind.ContainerRuntimeMissing, result.StartFailure));
        }

        if (result.TimedOut)
        {
            return new ContainerStartResult(false, Diagnose(RemoteDiagnosisKind.ContainerRuntimeUnavailable, TimeoutText()));
        }

        return result.ExitCode == 0
            ? new ContainerStartResult(true, null)
            : new ContainerStartResult(false, ExplainFailure(profile, result.ExitCode, result.StandardError));
    }

    // The probe script checks the folder and omp the same way for every environment; only the folder and the omp path of the profile matter here.
    private static SshProfile ToProbeProfile(ContainerProfile profile) =>
        new() { Alias = "container", RemoteWorkspaceRoot = profile.WorkspaceFolder, RemoteOmpPath = profile.OmpPath };

    private static RemoteDiagnosis Diagnose(RemoteDiagnosisKind kind, string? detail) =>
        ContainerDiagnosisCatalog.Create(kind, EnvironmentKind.Container, RemoteDiagnosisCatalog.Excerpt(detail ?? string.Empty));

    private static RemoteDiagnosis NotRunning(ContainerProfile profile, string detail) =>
        ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.ContainerNotRunning, EnvironmentKind.Container, detail, subject: profile.Container) with
        {
            FixText = "Start it with 'podman start " + profile.Container + "' (Autonogrammer offers a Start container button) or with the devcontainer CLI, then test the environment again.",
        };

    private static string SafeStatus(string status) =>
        status.Length is > 0 and <= 32 && status.All(character => char.IsAsciiLetterOrDigit(character) || character == '-') ? status : "unknown";

    private static string TimeoutText() => "podman did not answer in time.";

    private static RemoteDiagnosis ExplainFailure(ContainerProfile profile, int exitCode, string standardError)
    {
        var diagnosis = ContainerDiagnosisCatalog.FromPodmanFailure(exitCode, standardError);
        if (diagnosis is null)
        {
            var detail = RemoteDiagnosisCatalog.Excerpt(ContainerDiagnosisCatalog.Normalise(standardError));
            return ContainerDiagnosisCatalog.Create(
                RemoteDiagnosisKind.Unknown,
                EnvironmentKind.Container,
                string.Create(CultureInfo.InvariantCulture, $"podman exited with code {exitCode}.") + (detail is null ? string.Empty : " " + detail));
        }

        return diagnosis.Kind switch
        {
            RemoteDiagnosisKind.ContainerNotRunning => NotRunning(profile, diagnosis.Detail ?? string.Empty),
            RemoteDiagnosisKind.ContainerMissing => ContainerDiagnosisCatalog.Create(diagnosis.Kind, EnvironmentKind.Container, diagnosis.Detail, subject: profile.Container),
            RemoteDiagnosisKind.ContainerUserMissing => ContainerDiagnosisCatalog.Create(diagnosis.Kind, EnvironmentKind.Container, diagnosis.Detail, subject: profile.RemoteUser),
            _ => diagnosis,
        };
    }
}
