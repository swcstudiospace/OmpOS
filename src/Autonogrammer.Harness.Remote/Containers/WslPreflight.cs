using System.Globalization;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// The connection test of a WSL environment. It checks, each with a typed diagnosis and the text that fixes it: wsl.exe is available, the distribution is installed
/// (<c>wsl.exe --list --quiet</c>), the user exists in it, the workspace folder exists and is writable, omp (and Bun) are there, and the platform is the verified one.
/// It never installs anything, never changes the distribution and never answers a prompt. WSL exists on Windows only: on another system the result is the
/// "wsl.exe is not available" diagnosis. UNVERIFIED on a real Windows machine: the exact wording of the wsl.exe error texts (the rules are in
/// <see cref="ContainerDiagnosisCatalog.FromWslFailure"/>).
/// </summary>
public sealed class WslPreflight
{
    private readonly WslClientOptions _clientOptions;
    private readonly RemotePreflightOptions _options;
    private readonly SshProcessRunner _runner;
    private readonly WslCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="WslPreflight"/> class.</summary>
    /// <param name="clientOptions">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="options">What the distribution must provide, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    public WslPreflight(WslClientOptions? clientOptions = null, RemotePreflightOptions? options = null, TimeProvider? clock = null)
    {
        _clientOptions = clientOptions ?? new WslClientOptions();
        _options = options ?? new RemotePreflightOptions();
        _runner = new SshProcessRunner(clock);
        _builder = new WslCommandBuilder(_clientOptions);
    }

    /// <summary>Tests a WSL environment.</summary>
    /// <param name="profile">The distribution. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="cancellationToken">Cancels the test and stops wsl.exe.</param>
    /// <returns>The report: what was found and what must be fixed. A failing distribution is a report, not an exception.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public async Task<RemotePreflightReport> RunAsync(WslProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);

        var list = await _runner.RunAsync(_builder.BuildList(), _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (list.StartFailure is not null)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.WslMissing, list.StartFailure));
        }

        if (list.TimedOut)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.WslMissing, TimeoutText()));
        }

        if (list.ExitCode != 0)
        {
            return ProbeInterpreter.Failed(ExplainFailure(profile, list.ExitCode, list.StandardError + "\n" + list.StandardOutput));
        }

        if (!WslDistributionParser.Parse(list.StandardOutput).Contains(profile.Distro, StringComparer.OrdinalIgnoreCase))
        {
            return ProbeInterpreter.Failed(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.WslDistroMissing, EnvironmentKind.Wsl, subject: profile.Distro));
        }

        var probe = _builder.BuildProbe(profile, RemotePreflight.BuildProbeScript(ToProbeProfile(profile)));
        var result = await _runner.RunAsync(probe, _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.WslMissing, result.StartFailure));
        }

        if (result.TimedOut)
        {
            return ProbeInterpreter.Failed(Diagnose(RemoteDiagnosisKind.Unknown, TimeoutText()));
        }

        if (!RemotePreflight.ParseProbe(result.StandardOutput).ContainsKey("shell"))
        {
            return ProbeInterpreter.Failed(ExplainFailure(profile, result.ExitCode, result.StandardError));
        }

        return ProbeInterpreter.Interpret(EnvironmentKind.Wsl, profile.WorkspacePath, _options, result.StandardOutput, result.StandardError);
    }

    private static SshProfile ToProbeProfile(WslProfile profile) =>
        new() { Alias = "wsl", RemoteWorkspaceRoot = profile.WorkspacePath, RemoteOmpPath = profile.OmpPath };

    private static RemoteDiagnosis Diagnose(RemoteDiagnosisKind kind, string? detail) =>
        ContainerDiagnosisCatalog.Create(kind, EnvironmentKind.Wsl, RemoteDiagnosisCatalog.Excerpt(detail ?? string.Empty));

    private static string TimeoutText() => "wsl.exe did not answer in time. The first command may have to start the distribution; try again.";

    private static RemoteDiagnosis ExplainFailure(WslProfile profile, int exitCode, string standardError)
    {
        var diagnosis = ContainerDiagnosisCatalog.FromWslFailure(exitCode, standardError);
        if (diagnosis is null)
        {
            var detail = RemoteDiagnosisCatalog.Excerpt(ContainerDiagnosisCatalog.Normalise(standardError));
            return ContainerDiagnosisCatalog.Create(
                RemoteDiagnosisKind.Unknown,
                EnvironmentKind.Wsl,
                string.Create(CultureInfo.InvariantCulture, $"wsl.exe exited with code {exitCode}.") + (detail is null ? string.Empty : " " + detail));
        }

        return diagnosis.Kind switch
        {
            RemoteDiagnosisKind.WslDistroMissing => ContainerDiagnosisCatalog.Create(diagnosis.Kind, EnvironmentKind.Wsl, diagnosis.Detail, subject: profile.Distro),
            RemoteDiagnosisKind.WslUserMissing => ContainerDiagnosisCatalog.Create(diagnosis.Kind, EnvironmentKind.Wsl, diagnosis.Detail, subject: profile.User),
            _ => diagnosis,
        };
    }
}
