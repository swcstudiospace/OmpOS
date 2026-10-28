using System.Globalization;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote;

/// <summary>
/// The connection test of a remote environment. It runs one short non-interactive ssh command that prints what it finds as
/// <c>OMPOS_PROBE key=value</c> lines, and turns the answer (or the way ssh failed) into typed diagnoses, each with the text that fixes
/// it: ssh missing, host unreachable, host key unknown or changed, authentication failed, shell not POSIX, directory missing or not
/// writable, omp missing or too old, Bun missing, unsupported platform. It never installs anything and never answers a prompt.
/// </summary>
public sealed partial class RemotePreflight
{
    private const string ProbePrefix = "OMPOS_PROBE ";
    private const string ExpectedShellAnswer = "2";
    private const string SupportedPlatform = "Linux x86_64";

    private readonly SshClientOptions _clientOptions;
    private readonly RemotePreflightOptions _options;
    private readonly SshProcessRunner _runner;
    private readonly SshCommandBuilder _builder;

    /// <summary>Initializes a new instance of the <see cref="RemotePreflight"/> class.</summary>
    /// <param name="clientOptions">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="options">What the remote machine must provide, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    public RemotePreflight(SshClientOptions? clientOptions = null, RemotePreflightOptions? options = null, TimeProvider? clock = null)
    {
        _clientOptions = clientOptions ?? new SshClientOptions();
        _options = options ?? new RemotePreflightOptions();
        _runner = new SshProcessRunner(clock);
        _builder = new SshCommandBuilder(_clientOptions);
    }

    /// <summary>Tests a remote environment.</summary>
    /// <param name="profile">The remote machine. It must pass <see cref="EnvironmentProfileValidator"/>.</param>
    /// <param name="cancellationToken">Cancels the test and stops ssh.</param>
    /// <returns>The report: what was found and what must be fixed. A failing machine is a report, not an exception.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public async Task<RemotePreflightReport> RunAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var client = await _runner.RunAsync(
            new SshInvocation { FileName = _clientOptions.SshPath, Arguments = ["-V"] },
            _clientOptions.CommandTimeout,
            cancellationToken).ConfigureAwait(false);
        if (client.StartFailure is not null)
        {
            return Failed(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.SshMissing, client.StartFailure));
        }

        var probe = _builder.BuildProbe(profile, BuildProbeScript(profile));
        var result = await _runner.RunAsync(probe, _clientOptions.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return Failed(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.SshMissing, result.StartFailure));
        }

        if (result.TimedOut)
        {
            var seconds = ((int)_clientOptions.CommandTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            return Failed(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.HostUnreachable, "The connection test did not finish within " + seconds + " seconds."));
        }

        if (result.ExitCode == 255)
        {
            return Failed(RemoteDiagnosisCatalog.FromFailure(result.ExitCode, result.StandardError)
                ?? RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.Unknown, RemoteDiagnosisCatalog.Excerpt(result.StandardError)));
        }

        return Interpret(profile, result);
    }

    private static RemotePreflightReport Failed(RemoteDiagnosis diagnosis) =>
        new() { Succeeded = false, Diagnoses = [diagnosis] };

    private static string BuildProbeScript(SshProfile profile)
    {
        var root = PosixQuoting.Quote(profile.RemoteWorkspaceRoot);
        var omp = PosixQuoting.Quote(profile.RemoteOmpPath ?? "omp");
        return SshCommandBuilder.PathSetup
            + "; p() { printf 'OMPOS_PROBE %s=%s\\n' \"$1\" \"$2\"; }"
            + "; p shell \"$((1+1))\""
            + "; p home \"$HOME\""
            + "; p uname \"$(uname -sm 2>/dev/null)\""
            + $"; if [ -d {root} ]; then if [ -w {root} ]; then p dir ok; else p dir readonly; fi; else p dir missing; fi"
            + "; p mktemp \"$(command -v mktemp 2>/dev/null)\""
            + $"; p omp \"$(command -v {omp} 2>/dev/null)\""
            + $"; if command -v {omp} >/dev/null 2>&1; then p ompversion \"$({omp} --version 2>&1 | head -n 1)\"; fi"
            + "; p bun \"$(command -v bun 2>/dev/null)\"";
    }

    private static Dictionary<string, string> ParseProbe(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith(ProbePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var pair = line[ProbePrefix.Length..];
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        return values;
    }

    private RemotePreflightReport Interpret(SshProfile profile, SshProcessResult result)
    {
        var values = ParseProbe(result.StandardOutput);
        if (!values.TryGetValue("shell", out var shell) || !string.Equals(shell, ExpectedShellAnswer, StringComparison.Ordinal))
        {
            return Failed(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.ShellNotPosix, RemoteDiagnosisCatalog.Excerpt(result.StandardError)));
        }

        var diagnoses = new List<RemoteDiagnosis>();
        var home = values.GetValueOrDefault("home");
        var platform = values.GetValueOrDefault("uname");
        var ompPath = values.GetValueOrDefault("omp");
        var versionLine = values.GetValueOrDefault("ompversion");

        switch (values.GetValueOrDefault("dir"))
        {
            case "missing":
                diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryMissing, subject: profile.RemoteWorkspaceRoot));
                break;
            case "readonly":
                diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryNotWritable, subject: profile.RemoteWorkspaceRoot));
                break;
            default:
                break;
        }

        if (string.IsNullOrEmpty(values.GetValueOrDefault("mktemp")))
        {
            diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.RequiredToolMissing, subject: "mktemp"));
        }

        var ompVersion = DiagnoseOmp(ompPath, versionLine, diagnoses);

        if (!string.IsNullOrEmpty(platform) && !string.Equals(platform, SupportedPlatform, StringComparison.Ordinal))
        {
            diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.UnsupportedPlatform, severity: DiagnosisSeverity.Warning, subject: platform));
        }

        var ordered = diagnoses.OrderBy(diagnosis => diagnosis.Severity == DiagnosisSeverity.Error ? 0 : 1).ToList();
        return new RemotePreflightReport
        {
            Succeeded = ordered.TrueForAll(diagnosis => diagnosis.Severity != DiagnosisSeverity.Error),
            Diagnoses = ordered,
            HomeDirectory = string.IsNullOrEmpty(home) ? null : home,
            Platform = string.IsNullOrEmpty(platform) ? null : platform,
            OmpPath = string.IsNullOrEmpty(ompPath) ? null : ompPath,
            OmpVersion = ompVersion,
        };
    }

    private string? DiagnoseOmp(string? ompPath, string? versionLine, List<RemoteDiagnosis> diagnoses)
    {
        string? ompVersion = null;
        if (string.IsNullOrEmpty(ompPath))
        {
            diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpMissing));
        }
        else if (versionLine is not null && VersionPattern().Match(versionLine) is { Success: true } match)
        {
            var version = new Version(
                int.Parse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture));
            ompVersion = version.ToString(3);
            if (version < _options.MinimumOmpVersion)
            {
                diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpTooOld, subject: _options.MinimumOmpVersion.ToString(3)));
            }
        }
        else if (versionLine is not null && versionLine.Contains("bun", StringComparison.OrdinalIgnoreCase))
        {
            diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.BunMissing, RemoteDiagnosisCatalog.Excerpt(versionLine)));
        }
        else
        {
            diagnoses.Add(RemoteDiagnosisCatalog.Create(RemoteDiagnosisKind.Unknown, "omp --version printed: " + RemoteDiagnosisCatalog.Excerpt(versionLine ?? string.Empty)));
        }

        return ompVersion;
    }

    [GeneratedRegex("(?<major>\\d{1,6})\\.(?<minor>\\d{1,6})\\.(?<patch>\\d{1,6})", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();
}
