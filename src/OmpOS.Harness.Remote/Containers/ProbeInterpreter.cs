using System.Globalization;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Turns the <c>OMPOS_PROBE key=value</c> answer of the probe script (the one the ssh preflight runs) into a <see cref="RemotePreflightReport"/> for a container
/// or a WSL distribution: workspace folder missing or not writable, <c>mktemp</c> missing, omp missing or too old, Bun missing, unsupported platform. The
/// diagnoses use the container wording of <see cref="ContainerDiagnosisCatalog"/>.
/// </summary>
internal static partial class ProbeInterpreter
{
    private const string ExpectedShellAnswer = "2";
    private const string SupportedPlatform = "Linux x86_64";

    /// <summary>Interprets the output of a probe that ran.</summary>
    /// <param name="environment">The environment the probe ran in: container or WSL.</param>
    /// <param name="workspaceRoot">The workspace folder the probe checked.</param>
    /// <param name="options">The oldest omp version accepted.</param>
    /// <param name="standardOutput">What the probe wrote to stdout.</param>
    /// <param name="standardError">What the probe wrote to stderr (for the shell diagnosis).</param>
    /// <returns>The report.</returns>
    internal static RemotePreflightReport Interpret(EnvironmentKind environment, string workspaceRoot, RemotePreflightOptions options, string standardOutput, string standardError)
    {
        var values = RemotePreflight.ParseProbe(standardOutput);
        if (!values.TryGetValue("shell", out var shell) || !string.Equals(shell, ExpectedShellAnswer, StringComparison.Ordinal))
        {
            return Failed(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.ShellNotPosix, environment, RemoteDiagnosisCatalog.Excerpt(ContainerDiagnosisCatalog.Normalise(standardError))));
        }

        var diagnoses = new List<RemoteDiagnosis>();
        var home = values.GetValueOrDefault("home");
        var platform = values.GetValueOrDefault("uname");
        var ompPath = values.GetValueOrDefault("omp");

        switch (values.GetValueOrDefault("dir"))
        {
            case "missing":
                diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryMissing, environment, subject: workspaceRoot));
                break;
            case "readonly":
                diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.DirectoryNotWritable, environment, subject: workspaceRoot));
                break;
            default:
                break;
        }

        if (string.IsNullOrEmpty(values.GetValueOrDefault("mktemp")))
        {
            diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.RequiredToolMissing, environment, subject: "mktemp"));
        }

        var ompVersion = DiagnoseOmp(environment, options, ompPath, values.GetValueOrDefault("ompversion"), diagnoses);
        if (!string.IsNullOrEmpty(platform) && !string.Equals(platform, SupportedPlatform, StringComparison.Ordinal))
        {
            diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.UnsupportedPlatform, environment, severity: DiagnosisSeverity.Warning, subject: platform));
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

    /// <summary>Makes a failed report of one diagnosis.</summary>
    /// <param name="diagnosis">The diagnosis.</param>
    /// <returns>The report.</returns>
    internal static RemotePreflightReport Failed(RemoteDiagnosis diagnosis) =>
        new() { Succeeded = false, Diagnoses = [diagnosis] };

    private static string? DiagnoseOmp(EnvironmentKind environment, RemotePreflightOptions options, string? ompPath, string? versionLine, List<RemoteDiagnosis> diagnoses)
    {
        string? ompVersion = null;
        if (string.IsNullOrEmpty(ompPath))
        {
            diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpMissing, environment));
        }
        else if (versionLine is not null && VersionPattern().Match(versionLine) is { Success: true } match)
        {
            var version = new Version(
                int.Parse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture),
                int.Parse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture));
            ompVersion = version.ToString(3);
            if (version < options.MinimumOmpVersion)
            {
                diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.OmpTooOld, environment, subject: options.MinimumOmpVersion.ToString(3)));
            }
        }
        else if (versionLine is not null && versionLine.Contains("bun", StringComparison.OrdinalIgnoreCase))
        {
            diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.BunMissing, environment, RemoteDiagnosisCatalog.Excerpt(versionLine)));
        }
        else
        {
            diagnoses.Add(ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.Unknown, environment, "omp --version printed: " + RemoteDiagnosisCatalog.Excerpt(versionLine ?? string.Empty)));
        }

        return ompVersion;
    }

    [GeneratedRegex("(?<major>\\d{1,6})\\.(?<minor>\\d{1,6})\\.(?<patch>\\d{1,6})", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();
}
