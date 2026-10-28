using System.Text.RegularExpressions;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// The wording and the failure rules of the container and WSL environments: what is wrong inside a container or a distribution and how to fix it, and the rules
/// that turn the exit code and stderr text of <c>podman</c> or <c>wsl.exe</c> into a <see cref="RemoteDiagnosis"/>. The kinds that exist for ssh too (directory,
/// omp, Bun) get texts that name the container or the distribution, not a remote machine. Every text is plain and holds no secret.
/// </summary>
public static partial class ContainerDiagnosisCatalog
{
    private const int CommandNotInvokableExitCode = 126;
    private const int CommandNotFoundExitCode = 127;

    /// <summary>Creates the diagnosis of a kind for a container or a WSL distribution.</summary>
    /// <param name="kind">What is wrong.</param>
    /// <param name="environment">Where: <see cref="EnvironmentKind.Container"/> or <see cref="EnvironmentKind.Wsl"/>.</param>
    /// <param name="detail">The first lines the failing tool printed, already free of secrets, or <see langword="null"/>.</param>
    /// <param name="severity">The severity; <see langword="null"/> uses <see cref="DiagnosisSeverity.Error"/>.</param>
    /// <param name="subject">A name the text mentions (a directory, a version, a user or the container), or <see langword="null"/>.</param>
    /// <returns>The diagnosis.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="environment"/> is neither a container nor a WSL environment.</exception>
    public static RemoteDiagnosis Create(RemoteDiagnosisKind kind, EnvironmentKind environment, string? detail = null, DiagnosisSeverity? severity = null, string? subject = null)
    {
        if (environment is not (EnvironmentKind.Container or EnvironmentKind.Wsl))
        {
            throw new ArgumentOutOfRangeException(nameof(environment), environment, "Only container and WSL environments have these diagnoses.");
        }

        var where = environment == EnvironmentKind.Wsl ? "in the WSL distribution" : "inside the container";
        var shortWhere = environment == EnvironmentKind.Wsl ? "the distribution" : "the container";
        var name = string.IsNullOrWhiteSpace(subject) ? "the configured one" : subject;
        (string Summary, string Fix)? own = kind switch
        {
            RemoteDiagnosisKind.DirectoryMissing => (
                $"The workspace folder {name} does not exist {where}.",
                $"Create the folder {where} or choose another workspace folder in the profile. It must be an absolute path. A dev container usually mounts the project at /workspaces/<folder>."),
            RemoteDiagnosisKind.DirectoryNotWritable => (
                $"The user {where} cannot write to {name}.",
                $"Give the user write access to the folder, or choose a user that owns it. A rootless podman container maps your own user to root {where}; set the Remote user to the one that owns the project files."),
            RemoteDiagnosisKind.OmpMissing => (
                $"omp was not found {where}.",
                $"Install omp {where} (the dev container definition is the right place), or set its full path in the profile (omp path). A non-interactive shell often has a shorter PATH than a login shell; OmpOS also looks in ~/.bun/bin, ~/.local/bin and /usr/local/bin."),
            RemoteDiagnosisKind.OmpTooOld => (
                $"The omp {where} is older than {name}.",
                $"Update omp {where} the same way you installed it, then test the environment again."),
            RemoteDiagnosisKind.BunMissing => (
                $"omp needs Bun and {shortWhere} has none on its PATH.",
                $"Install Bun {where} (https://bun.sh) for the same user, or make sure ~/.bun/bin exists. omp starts through '#!/usr/bin/env bun'."),
            RemoteDiagnosisKind.UnsupportedPlatform => (
                $"OmpOS has only verified omp on Linux x86_64; {shortWhere} reports {name}.",
                "The environment may work. Report anything that does not."),
            RemoteDiagnosisKind.RequiredToolMissing => (
                $"There is no '{name}' command {where}, which OmpOS needs.",
                $"Install the standard tool {where} (coreutils provides mktemp) and test the environment again."),
            RemoteDiagnosisKind.ShellNotPosix => (
                $"The shell {where} did not answer a POSIX shell test.",
                $"OmpOS runs 'sh -c' {where}. Make sure /bin/sh exists and is a POSIX shell."),
            _ => null,
        };

        var (summary, fix) = own ?? TextOf(RemoteDiagnosisCatalog.Create(kind, subject: subject));
        return new RemoteDiagnosis
        {
            Kind = kind,
            Severity = severity ?? DiagnosisSeverity.Error,
            Summary = summary,
            FixText = fix,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail,
        };
    }

    /// <summary>
    /// Explains a failed podman run from its exit code and stderr text. Returns <see langword="null"/> when the failure is not recognised and the caller should
    /// report it as <see cref="RemoteDiagnosisKind.Unknown"/> with the detail.
    /// </summary>
    /// <param name="exitCode">The exit code of the podman process (125 and 255 are podman's own failures; 126 and 127 are the command's).</param>
    /// <param name="standardError">What the process wrote to stderr, with secrets removed.</param>
    /// <returns>The diagnosis, or <see langword="null"/>.</returns>
    public static RemoteDiagnosis? FromPodmanFailure(int exitCode, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        var text = Normalise(standardError);
        var detail = RemoteDiagnosisCatalog.Excerpt(text);
        if (ContainsAny(text, "no such container", "no container with name or id"))
        {
            return Create(RemoteDiagnosisKind.ContainerMissing, EnvironmentKind.Container, detail, subject: QuotedName(text));
        }

        if (ContainsAny(text, "can only create exec sessions on running containers", "container state improper", "is not running", "container is stopped"))
        {
            return Create(RemoteDiagnosisKind.ContainerNotRunning, EnvironmentKind.Container, detail, subject: QuotedName(text));
        }

        if (ContainsAny(text, "unable to find user", "no matching entries in passwd file", "unable to find group"))
        {
            return Create(RemoteDiagnosisKind.ContainerUserMissing, EnvironmentKind.Container, detail, subject: UserName(text));
        }

        if (ContainsAny(text, "cannot connect to podman", "unable to connect to podman", "podman.sock"))
        {
            return Create(RemoteDiagnosisKind.ContainerRuntimeUnavailable, EnvironmentKind.Container, detail);
        }

        if (exitCode == CommandNotFoundExitCode || (exitCode == CommandNotInvokableExitCode && ContainsAny(text, "executable file", "not found in $path", "no such file or directory")))
        {
            return Create(RemoteDiagnosisKind.OmpMissing, EnvironmentKind.Container, detail);
        }

        if (exitCode is RemoteDiagnosisCatalog.BootstrapDirectoryMissingExitCode)
        {
            return Create(RemoteDiagnosisKind.DirectoryMissing, EnvironmentKind.Container, detail);
        }

        if (exitCode is RemoteDiagnosisCatalog.BootstrapPrepareFailedExitCode)
        {
            return Create(RemoteDiagnosisKind.DirectoryNotWritable, EnvironmentKind.Container, detail);
        }

        return null;
    }

    /// <summary>
    /// Explains a failed wsl.exe run from its exit code and stderr text. The text of wsl.exe itself is UTF-16, so NUL characters left by a UTF-8 decoder are
    /// ignored. Returns <see langword="null"/> when the failure is not recognised.
    /// </summary>
    /// <param name="exitCode">The exit code of the wsl.exe process.</param>
    /// <param name="standardError">What the process wrote to stderr, with secrets removed.</param>
    /// <returns>The diagnosis, or <see langword="null"/>.</returns>
    public static RemoteDiagnosis? FromWslFailure(int exitCode, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        var text = Normalise(standardError);
        var detail = RemoteDiagnosisCatalog.Excerpt(text);
        if (ContainsAny(text, "there is no distribution with the supplied name", "no distribution with the supplied name", "distribution not found", "has no installed distributions"))
        {
            return Create(RemoteDiagnosisKind.WslDistroMissing, EnvironmentKind.Wsl, detail);
        }

        if (ContainsAny(text, "there is no user", "user not found", "invalid user", "no such user", "unable to find user"))
        {
            return Create(RemoteDiagnosisKind.WslUserMissing, EnvironmentKind.Wsl, detail);
        }

        if (ContainsAny(text, "wsl is not installed", "optional component is not enabled"))
        {
            return Create(RemoteDiagnosisKind.WslMissing, EnvironmentKind.Wsl, detail);
        }

        return exitCode switch
        {
            CommandNotFoundExitCode => Create(RemoteDiagnosisKind.OmpMissing, EnvironmentKind.Wsl, detail),
            RemoteDiagnosisCatalog.BootstrapDirectoryMissingExitCode => Create(RemoteDiagnosisKind.DirectoryMissing, EnvironmentKind.Wsl, detail),
            RemoteDiagnosisCatalog.BootstrapPrepareFailedExitCode => Create(RemoteDiagnosisKind.DirectoryNotWritable, EnvironmentKind.Wsl, detail),
            _ => null,
        };
    }

    /// <summary>Removes the NUL characters a UTF-8 decoder leaves when it reads UTF-16 text (the messages of wsl.exe) and normalises line breaks.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The cleaned text.</returns>
    internal static string Normalise(string text) =>
        text.Replace("\0", string.Empty, StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static (string Summary, string Fix) TextOf(RemoteDiagnosis diagnosis) => (diagnosis.Summary, diagnosis.FixText);

    private static bool ContainsAny(string text, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? QuotedName(string text)
    {
        var match = QuotedPattern().Match(text);
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static string? UserName(string text)
    {
        var match = UserPattern().Match(text);
        return match.Success ? match.Groups["name"].Value : null;
    }

    [GeneratedRegex("\"(?<name>[A-Za-z0-9][A-Za-z0-9._-]{0,127})\"", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedPattern();

    [GeneratedRegex("unable to find (?:user|group) (?<name>[A-Za-z0-9._-]{1,64})", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserPattern();
}
