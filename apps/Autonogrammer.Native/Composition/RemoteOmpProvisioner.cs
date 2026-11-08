using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Harness.RemoteProvision;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The desktop's Omp checks and installs of the saved ssh environments: the provisioning service over the ssh exec of each environment, with one confirmation
/// store for the head. A check reads the machine, writes nothing, and remembers the lines it planned for its environment. An install applies those
/// remembered lines once, and only when the machine still needs exactly them; a new check is needed before every install. Plugins are not provisioned here:
/// the bundle is empty. An environment with a custom remote omp path is refused, because the service manages only the default omp.
/// </summary>
internal sealed class RemoteOmpProvisioner : IRemoteOmpProvisioner
{
    private const string DefaultOmpCommand = "omp";

    private readonly IRemoteEnvironmentsHost _environments;
    private readonly Func<SshProfile, IRemoteExec> _openExec;
    private readonly InMemoryProvisionConfirmationStore _confirmations;
    private readonly RemoteProvisionService _service;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, IReadOnlyList<string>> _checkedLines = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _gate = new();

    /// <summary>Initializes a new instance of the <see cref="RemoteOmpProvisioner"/> class.</summary>
    /// <param name="environments">Reads the saved environments, to find the ssh profile of each.</param>
    /// <param name="releases">Reads the latest omp release.</param>
    /// <param name="openExec">Opens the ssh exec of an environment. The caller disposes it.</param>
    /// <param name="clock">The clock of the confirmations and of the probe's timestamp.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public RemoteOmpProvisioner(IRemoteEnvironmentsHost environments, IOmpReleaseSource releases, Func<SshProfile, IRemoteExec> openExec, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(openExec);
        ArgumentNullException.ThrowIfNull(clock);
        _environments = environments;
        _openExec = openExec;
        _clock = clock;
        _confirmations = new InMemoryProvisionConfirmationStore();
        _service = new RemoteProvisionService(releases, _confirmations, clock);
    }

    /// <summary>
    /// Creates the provisioner of a head whose omp is managed. The latest release is read through an installer over the managed options, and each
    /// environment is reached through the same ssh options as its connection test and its sessions.
    /// </summary>
    /// <param name="environments">The saved environments.</param>
    /// <param name="options">The managed omp options of the head.</param>
    /// <param name="sshOptions">How ssh is run for the environments.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The provisioner.</returns>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public static RemoteOmpProvisioner ForManagedInstall(IRemoteEnvironmentsHost environments, OmpManagedOptions options, SshClientOptions sshOptions, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(sshOptions);
        return new RemoteOmpProvisioner(
            environments,
            new OmpManagedInstallerReleaseSource(new OmpManagedInstaller(options)),
            profile => new RemoteExec(profile, sshOptions, clock),
            clock);
    }

    /// <inheritdoc />
    public async Task<RemoteOmpReport> CheckAsync(string environmentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentId);
        var profile = await FindSshProfileAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            Forget(environmentId);
            return RemoteOmpReports.Unavailable(RemoteOmpReports.NotSaved);
        }

        if (!IsOmpManaged(profile))
        {
            Forget(environmentId);
            return RemoteOmpReports.Refused(RemoteOmpReports.CustomOmpPath(profile.RemoteOmpPath!));
        }

        using var exec = _openExec(profile);
        var result = await _service.CheckAsync(Request(environmentId, profile), exec, cancellationToken).ConfigureAwait(false);
        var report = RemoteOmpReports.From(result);
        Remember(environmentId, report);
        return report;
    }

    /// <inheritdoc />
    public async Task<RemoteOmpReport> InstallAsync(string environmentId, IReadOnlyList<string> checkedActions, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentId);
        ArgumentNullException.ThrowIfNull(checkedActions);

        // The check is consumed here, whatever happens next: a second install needs a second check.
        var planned = TakeChecked(environmentId);
        if (planned is null || !planned.SequenceEqual(checkedActions, StringComparer.Ordinal))
        {
            return RemoteOmpReports.Stale();
        }

        var profile = await FindSshProfileAsync(environmentId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return RemoteOmpReports.Unavailable(RemoteOmpReports.NotSaved);
        }

        if (!IsOmpManaged(profile))
        {
            return RemoteOmpReports.Refused(RemoteOmpReports.CustomOmpPath(profile.RemoteOmpPath!));
        }

        // The service writes only when the confirmation equals the plan it makes now, so the lines applied are exactly the lines checked.
        await _confirmations.RecordAsync(new ProvisionConfirmation(environmentId, planned, _clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        using var exec = _openExec(profile);
        var result = await _service.ProvisionAsync(Request(environmentId, profile), exec, cancellationToken).ConfigureAwait(false);
        return RemoteOmpReports.From(result);
    }

    private static bool IsOmpManaged(SshProfile profile) =>
        profile.RemoteOmpPath is null || string.Equals(profile.RemoteOmpPath, DefaultOmpCommand, StringComparison.Ordinal);

    private static RemoteProvisionRequest Request(string environmentId, SshProfile profile) => new(environmentId, profile, [], null);

    private async Task<SshProfile?> FindSshProfileAsync(string environmentId, CancellationToken cancellationToken)
    {
        var list = await _environments.LoadProfilesAsync(cancellationToken).ConfigureAwait(false);
        return list.Profiles
            .FirstOrDefault(entry => entry.Profile.Kind == EnvironmentKind.Ssh && string.Equals(entry.Profile.Id, environmentId, StringComparison.Ordinal))
            ?.Profile.Ssh;
    }

    private void Remember(string environmentId, RemoteOmpReport report)
    {
        lock (_gate)
        {
            if (report.Outcome == RemoteOmpOutcome.Planned)
            {
                _checkedLines[environmentId] = report.PlannedActions;
            }
            else
            {
                _ = _checkedLines.Remove(environmentId);
            }
        }
    }

    private List<string>? TakeChecked(string environmentId)
    {
        lock (_gate)
        {
            return _checkedLines.Remove(environmentId, out var lines) ? lines.ToList() : null;
        }
    }

    private void Forget(string environmentId)
    {
        lock (_gate)
        {
            _ = _checkedLines.Remove(environmentId);
        }
    }
}
