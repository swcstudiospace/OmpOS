using System.Text;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// The wording of every <see cref="RemoteDiagnosis"/>: what is wrong and how to fix it, and the rules that turn the stderr text and the
/// exit code of a failed ssh run into a diagnosis. Every text is plain and holds no secret.
/// </summary>
public static class RemoteDiagnosisCatalog
{
    /// <summary>The exit code the remote bootstrap uses when it cannot create its session directory or temporary file.</summary>
    public const int BootstrapPrepareFailedExitCode = 70;

    /// <summary>The exit code the remote bootstrap uses when the working directory does not exist.</summary>
    public const int BootstrapDirectoryMissingExitCode = 71;

    private const int SshFailureExitCode = 255;
    private const int CommandNotFoundExitCode = 127;
    private const int MaxDetailLines = 3;
    private const int MaxDetailCharacters = 400;

    /// <summary>Creates the diagnosis of a kind.</summary>
    /// <param name="kind">What is wrong.</param>
    /// <param name="detail">The first lines the failing tool printed, already free of secrets, or <see langword="null"/>.</param>
    /// <param name="severity">The severity; <see langword="null"/> uses <see cref="DiagnosisSeverity.Error"/>.</param>
    /// <param name="subject">A name the text mentions (a directory or version), or <see langword="null"/>.</param>
    /// <returns>The diagnosis.</returns>
    public static RemoteDiagnosis Create(RemoteDiagnosisKind kind, string? detail = null, DiagnosisSeverity? severity = null, string? subject = null)
    {
        var (summary, fix) = TextFor(kind, subject);
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
    /// Explains a failed ssh run from its exit code and stderr text. Returns <see langword="null"/> when the failure is not recognised and the
    /// caller should report it as <see cref="RemoteDiagnosisKind.Unknown"/> with the detail.
    /// </summary>
    /// <param name="exitCode">The exit code of the ssh process.</param>
    /// <param name="standardError">What the process wrote to stderr, with secrets removed.</param>
    /// <returns>The diagnosis, or <see langword="null"/>.</returns>
    public static RemoteDiagnosis? FromFailure(int exitCode, string standardError)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        var detail = Excerpt(standardError);
        if (exitCode == SshFailureExitCode || LooksLikeSshFailure(standardError))
        {
            return ClassifySshText(standardError) is { } kind ? Create(kind, detail) : null;
        }

        return exitCode switch
        {
            BootstrapDirectoryMissingExitCode => Create(RemoteDiagnosisKind.DirectoryMissing, detail),
            BootstrapPrepareFailedExitCode => Create(RemoteDiagnosisKind.DirectoryNotWritable, detail),
            CommandNotFoundExitCode => Create(RemoteDiagnosisKind.OmpMissing, detail),
            _ => null,
        };
    }

    /// <summary>Returns the first lines of a text, cut to a bounded size, for the detail of a diagnosis.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The excerpt, or <see langword="null"/> when the text holds no visible line.</returns>
    public static string? Excerpt(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder();
        var lines = 0;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (lines > 0)
            {
                builder.Append('\n');
            }

            builder.Append(trimmed);
            lines++;
            if (lines == MaxDetailLines || builder.Length >= MaxDetailCharacters)
            {
                break;
            }
        }

        if (builder.Length == 0)
        {
            return null;
        }

        return builder.Length > MaxDetailCharacters ? builder.ToString(0, MaxDetailCharacters) : builder.ToString();
    }

    private static bool LooksLikeSshFailure(string text) =>
        text.Contains("Host key verification failed", StringComparison.Ordinal)
        || text.Contains("Permission denied (", StringComparison.Ordinal)
        || text.Contains("Could not resolve hostname", StringComparison.Ordinal)
        || text.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", StringComparison.Ordinal);

    private static RemoteDiagnosisKind? ClassifySshText(string text)
    {
        if (text.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED", StringComparison.Ordinal)
            || text.Contains("Offending ", StringComparison.Ordinal))
        {
            return RemoteDiagnosisKind.HostKeyChanged;
        }

        if (text.Contains("Host key verification failed", StringComparison.Ordinal))
        {
            return RemoteDiagnosisKind.HostKeyUnknown;
        }

        if (text.Contains("Permission denied", StringComparison.Ordinal)
            || text.Contains("Too many authentication failures", StringComparison.Ordinal)
            || text.Contains("No supported authentication methods", StringComparison.Ordinal))
        {
            return RemoteDiagnosisKind.AuthFailed;
        }

        string[] unreachable =
        [
            "Could not resolve hostname",
            "Connection refused",
            "Connection timed out",
            "Operation timed out",
            "No route to host",
            "Network is unreachable",
            "Connection reset",
            "Connection closed by",
            "kex_exchange_identification",
            "banner exchange",
        ];
        foreach (var marker in unreachable)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return RemoteDiagnosisKind.HostUnreachable;
            }
        }

        return null;
    }

    private static (string Summary, string Fix) TextFor(RemoteDiagnosisKind kind, string? subject)
    {
        var name = string.IsNullOrWhiteSpace(subject) ? "the configured one" : subject;
        return kind switch
        {
            RemoteDiagnosisKind.SshMissing => (
                "The ssh client is not installed or cannot be started.",
                "Install the OpenSSH client (for example 'sudo apt install openssh-client') and make sure 'ssh' is on PATH, or set its full path in the Remote environments settings."),
            RemoteDiagnosisKind.HostUnreachable => (
                "The host cannot be reached.",
                "Check the host name, the port and your network or VPN, then try 'ssh <alias>' in a terminal. Autonogrammer gives up after a few seconds so a wrong address does not hang the app."),
            RemoteDiagnosisKind.HostKeyUnknown => (
                "The host key of this machine is not trusted yet.",
                "Open the host trust terminal (Remote environments, Trust host), compare the fingerprint with the one your administrator published and answer 'yes'. Autonogrammer never accepts a host key for you and never edits known_hosts."),
            RemoteDiagnosisKind.HostKeyChanged => (
                "The host key differs from the one stored in known_hosts.",
                "Do not connect until you know why. If the host was reinstalled on purpose, remove the old entry with 'ssh-keygen -R <host>' and trust the host again; otherwise report it, the connection may be intercepted."),
            RemoteDiagnosisKind.AuthFailed => (
                "The host refused the credentials ssh could offer without asking you.",
                "Autonogrammer connects without prompts (BatchMode). Load your key into ssh-agent ('ssh-add'), or set IdentityFile for this host in ~/.ssh/config. Password and passphrase prompts are not supported."),
            RemoteDiagnosisKind.ShellNotPosix => (
                "The login shell of the remote user does not understand POSIX shell syntax.",
                "Autonogrammer runs its commands through 'sh -c', but ssh first hands them to the login shell. Make the remote user's login shell a POSIX shell (bash, zsh or sh) for non-interactive sessions, or use another user."),
            RemoteDiagnosisKind.DirectoryMissing => (
                $"The remote workspace directory {name} does not exist.",
                "Create the directory on the remote machine or choose another remote workspace root in the profile. It must be an absolute path."),
            RemoteDiagnosisKind.DirectoryNotWritable => (
                $"The remote user cannot write to {name}.",
                "Give the remote user write access to the directory, or choose a directory the user owns. Also check that the remote home directory has free space."),
            RemoteDiagnosisKind.OmpMissing => (
                "omp was not found on the remote machine.",
                "Install omp on the remote machine, or set its full path in the profile (Remote omp path). A non-interactive ssh session often has a shorter PATH than your login shell; Autonogrammer also looks in ~/.bun/bin, ~/.local/bin and /usr/local/bin."),
            RemoteDiagnosisKind.OmpTooOld => (
                $"The omp on the remote machine is older than {name}.",
                "Update omp on the remote machine the same way you installed it, then test the connection again."),
            RemoteDiagnosisKind.BunMissing => (
                "omp needs Bun and the remote machine has none on its PATH.",
                "Install Bun on the remote machine (https://bun.sh) as the same user, or make sure ~/.bun/bin exists. omp starts through '#!/usr/bin/env bun'."),
            RemoteDiagnosisKind.UnsupportedPlatform => (
                $"Autonogrammer has only verified omp on Linux x86_64; this machine reports {name}.",
                "The connection may work. Report anything that does not. A Windows machine cannot be used as a remote environment."),
            RemoteDiagnosisKind.RequiredToolMissing => (
                $"The remote machine has no '{name}' command, which Autonogrammer needs.",
                "Install the standard tool on the remote machine (coreutils provides mktemp) and test the connection again."),
            _ => ContainerTextFor(kind, subject),
        };
    }

    private static (string Summary, string Fix) ContainerTextFor(RemoteDiagnosisKind kind, string? subject)
    {
        return kind switch
        {
            RemoteDiagnosisKind.ContainerRuntimeMissing => (
                "podman is not installed or cannot be started on this machine.",
                "Install podman (https://podman.io) or set its full path in the profile (Podman path). Autonogrammer uses podman only."),
            RemoteDiagnosisKind.ContainerRuntimeUnavailable => (
                "podman is installed but its service did not answer.",
                "Start the podman service (on macOS and Windows: 'podman machine start') and test the environment again."),
            RemoteDiagnosisKind.ContainerMissing => (
                string.IsNullOrWhiteSpace(subject) ? "The container of this environment does not exist." : $"No podman container is named {subject}.",
                "Check the name with 'podman ps -a' or pick a running container in the list. For a dev container, start it with the devcontainer CLI or podman first, then attach."),
            RemoteDiagnosisKind.ContainerNotRunning => (
                string.IsNullOrWhiteSpace(subject) ? "The container of this environment is not running." : $"The container {subject} is not running.",
                "Start it with 'podman start' (Autonogrammer offers the button) or with the devcontainer CLI, then test the environment again."),
            RemoteDiagnosisKind.ContainerUserMissing => (
                string.IsNullOrWhiteSpace(subject) ? "The user of this environment does not exist inside the container." : $"The user {subject} does not exist inside the container.",
                "Choose a user that exists in the container (see the Remote user of the profile), or leave the user empty to use the container's default user."),
            RemoteDiagnosisKind.WslMissing => (
                "wsl.exe is not available on this machine.",
                "WSL environments need Windows with the Windows Subsystem for Linux installed. Run 'wsl --install' in an administrator terminal, then test the environment again."),
            RemoteDiagnosisKind.WslDistroMissing => (
                string.IsNullOrWhiteSpace(subject) ? "The WSL distribution of this environment does not exist." : $"No WSL distribution is named {subject}.",
                "Check the name with 'wsl --list --verbose' or install the distribution with 'wsl --install -d <name>', then test the environment again."),
            RemoteDiagnosisKind.WslUserMissing => (
                string.IsNullOrWhiteSpace(subject) ? "The user of this environment does not exist in the WSL distribution." : $"The user {subject} does not exist in the WSL distribution.",
                "Choose a user that exists in the distribution, or leave the user empty to use the distribution's default user."),
            _ => (
                "The connection test failed for a reason Autonogrammer does not know.",
                "Run 'ssh <alias> true' in a terminal to see the message ssh prints, fix it and test the connection again."),
        };
    }
}
