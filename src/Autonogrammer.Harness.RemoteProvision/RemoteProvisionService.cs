using System.Text;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.Remote;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// Provisions a remote environment with the pinned Bun, the latest omp and the bundled plugins. A check reads the machine with one read-only command
/// and reports the drift. A provision does the same, then installs only the drifted items, and only after the user has confirmed that exact action
/// list for that environment. A machine that matches the desired state is never written to, so a second provision of it makes no write.
/// </summary>
public sealed class RemoteProvisionService
{
    private const string BashStdin = "bash -s";
    private const string OmpCommand = "omp";

    private static readonly Uri DefaultGitHubBase = new("https://github.com/");

    private readonly IOmpReleaseSource _releases;
    private readonly IProvisionConfirmationStore _confirmations;
    private readonly TimeProvider _clock;
    private readonly OmpBunPin _pin;
    private readonly Uri _gitHubBase;

    /// <summary>Initializes a new instance of the <see cref="RemoteProvisionService"/> class.</summary>
    /// <param name="releases">Reads the latest omp release from the registry.</param>
    /// <param name="confirmations">Keeps the user's confirmations per environment.</param>
    /// <param name="clock">The clock the probe's timestamp uses, or null for the system clock.</param>
    /// <param name="pin">The pinned Bun, or null for <see cref="OmpBunPin.Default"/>.</param>
    /// <param name="gitHubBase">The GitHub base address the Bun archive is read from, with a trailing slash, or null for <c>https://github.com/</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="releases"/> or <paramref name="confirmations"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="gitHubBase"/> is not an absolute address ending with a slash.</exception>
    public RemoteProvisionService(IOmpReleaseSource releases, IProvisionConfirmationStore confirmations, TimeProvider? clock = null, OmpBunPin? pin = null, Uri? gitHubBase = null)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(confirmations);
        var github = gitHubBase ?? DefaultGitHubBase;
        if (!github.IsAbsoluteUri || !github.AbsolutePath.EndsWith('/'))
        {
            throw new ArgumentException("The GitHub base address must be absolute and end with a slash.", nameof(gitHubBase));
        }

        _releases = releases;
        _confirmations = confirmations;
        _clock = clock ?? TimeProvider.System;
        _pin = pin ?? OmpBunPin.Default;
        _gitHubBase = github;
    }

    /// <summary>Reads the remote machine and reports what differs from the desired state. It writes nothing.</summary>
    /// <param name="request">The environment, its profile, the desktop's plugin bundle and the last snapshot.</param>
    /// <param name="exec">The exec layer of the environment's ssh host.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The drift, or the reason the check could not be made.</returns>
    public async Task<RemoteProvisionResult> CheckAsync(RemoteProvisionRequest request, IRemoteExec exec, CancellationToken cancellationToken)
    {
        ValidateArguments(request, exec);
        var step = await PlanAsync(request, exec, cancellationToken).ConfigureAwait(false);
        return step switch
        {
            Ready ready => new ProvisionCheck(ready.State, ready.Plan.Drift, Summaries(ready.Plan)),
            Stopped stopped => stopped.Result,
            _ => throw new InvalidOperationException("The planning step has no known outcome."),
        };
    }

    /// <summary>
    /// Brings the remote machine to the desired state. A machine with no actionable drift is returned as a check with no write. Otherwise the
    /// action list is compared with the user's confirmation for this environment: without an exact match nothing is written and the list is returned.
    /// With a match, each install runs in order, and the first failure stops the rest.
    /// </summary>
    /// <param name="request">The environment, its profile, the desktop's plugin bundle and the last snapshot.</param>
    /// <param name="exec">The exec layer of the environment's ssh host.</param>
    /// <param name="cancellationToken">Cancels the provision. An install that was cancelled leaves its temporary directory removed by its script.</param>
    /// <returns>The outcome: unchanged, needs confirmation, the per-action results, or why the machine could not be reached or read.</returns>
    public async Task<RemoteProvisionResult> ProvisionAsync(RemoteProvisionRequest request, IRemoteExec exec, CancellationToken cancellationToken)
    {
        ValidateArguments(request, exec);
        var step = await PlanAsync(request, exec, cancellationToken).ConfigureAwait(false);
        switch (step)
        {
            case Stopped stopped:
                return stopped.Result;
            case Ready ready when ready.Plan.Actions.Count == 0:
                return new ProvisionCheck(ready.State, ready.Plan.Drift, []);
            case Ready ready:
                return await ProvisionReadyAsync(ready, request, exec, cancellationToken).ConfigureAwait(false);
            default:
                throw new InvalidOperationException("The planning step has no known outcome.");
        }
    }

    private static void ValidateArguments(RemoteProvisionRequest request, IRemoteExec exec)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(exec);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EnvironmentId, nameof(request));
        ArgumentNullException.ThrowIfNull(request.Profile, nameof(request));
        ArgumentNullException.ThrowIfNull(request.PluginBundle, nameof(request));
    }

    private static List<string> Summaries(ProvisionPlan plan) => plan.Actions.Select(static action => action.Summary).ToList();

    private static bool IsOmpManaged(SshProfile profile) => profile.RemoteOmpPath is null || string.Equals(profile.RemoteOmpPath, OmpCommand, StringComparison.Ordinal);

    private async Task<RemoteProvisionResult> ProvisionReadyAsync(Ready ready, RemoteProvisionRequest request, IRemoteExec exec, CancellationToken cancellationToken)
    {
        var summaries = Summaries(ready.Plan);
        var confirmation = await _confirmations.FindAsync(request.EnvironmentId, cancellationToken).ConfigureAwait(false);
        if (confirmation is null || !confirmation.Actions.SequenceEqual(summaries, StringComparer.Ordinal))
        {
            return new ProvisionNeedsConfirmation(ready.State, ready.Plan.Drift, summaries);
        }

        return await RunActionsAsync(ready, exec, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteProvisionResult> RunActionsAsync(Ready ready, IRemoteExec exec, CancellationToken cancellationToken)
    {
        var outcomes = new List<ProvisionActionOutcome>(ready.Plan.Actions.Count);
        var entries = ready.State.Plugins;
        var failed = false;
        foreach (var action in ready.Plan.Actions)
        {
            if (failed)
            {
                outcomes.Add(new ProvisionActionOutcome(action.Summary, ProvisionActionStatus.NotRun, RemoteProvisionMessages.NotRunReason));
                continue;
            }

            var merged = action is PluginInstallAction plugin ? MergeEntries(entries, plugin.Record) : entries;
            var script = ScriptFor(action, merged);
            var result = await exec.RunAsync(BashStdin, Encoding.UTF8.GetBytes(script), RemoteProvisionScripts.MaxOutputBytes, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                outcomes.Add(new ProvisionActionOutcome(action.Summary, ProvisionActionStatus.Succeeded, null));
                entries = merged;
            }
            else
            {
                failed = true;
                outcomes.Add(new ProvisionActionOutcome(action.Summary, ProvisionActionStatus.Failed, FailureMessage(result)));
            }
        }

        return new ProvisionCompleted(ready.State, outcomes);
    }

    private string ScriptFor(ProvisionAction action, IReadOnlyList<RemotePluginEntry> entries) => action switch
    {
        BunInstallAction bun => RemoteProvisionScripts.Bun(bun, _gitHubBase),
        OmpInstallAction omp => RemoteProvisionScripts.Omp(omp),
        PluginInstallAction plugin => RemoteProvisionScripts.Plugin(plugin, BundleJson(entries)),
        _ => throw new InvalidOperationException("The action has no known script."),
    };

    private static List<RemotePluginEntry> MergeEntries(IReadOnlyList<RemotePluginEntry> entries, PluginBundleRecord record) =>
        entries
            .Where(entry => !string.Equals(entry.Dir, record.Dir, StringComparison.Ordinal))
            .Append(new RemotePluginEntry(record.Dir, record.Commit, record.ToJson()))
            .ToList();

    private static string BundleJson(IReadOnlyList<RemotePluginEntry> entries)
    {
        var array = new JsonArray(entries.Select(static entry => (JsonNode?)entry.Entry.DeepClone()).ToArray());
        return array.ToJsonString();
    }

    private static string FailureMessage(RemoteExecResult result)
    {
        if (result.TimedOut)
        {
            return RemoteProvisionMessages.InstallTimedOut;
        }

        return result.IsConnectionFailure
            ? RemoteOutputExcerpt.Of(result.DescribeConnectionFailure())
            : RemoteOutputExcerpt.Of(RemoteProvisionMessages.InstallExited(result.ExitCode) + " " + result.Error);
    }

    private async Task<Step> PlanAsync(RemoteProvisionRequest request, IRemoteExec exec, CancellationToken cancellationToken)
    {
        var probe = await exec.RunAsync(RemoteProbe.Script, ReadOnlyMemory<byte>.Empty, RemoteProbe.MaxOutputBytes, cancellationToken).ConfigureAwait(false);
        if (probe.IsConnectionFailure)
        {
            return new Stopped(new ProvisionUnavailable(request.LastKnown, RemoteOutputExcerpt.Of(probe.DescribeConnectionFailure())));
        }

        if (!probe.Succeeded)
        {
            return new Stopped(new ProvisionProbeFailed(RemoteProvisionMessages.ProbeExited(probe.ExitCode), RemoteOutputExcerpt.Of(probe.Error)));
        }

        if (probe.OutputTruncated)
        {
            return new Stopped(new ProvisionProbeFailed("The probe answer is longer than the probe may write.", null));
        }

        var output = probe.OutputText();
        var parsed = RemoteProbeParser.Parse(output, _clock.GetUtcNow());
        if (parsed.State is not { } state)
        {
            return new Stopped(new ProvisionProbeFailed(RemoteProvisionMessages.ProbeUnreadable(parsed.Failure ?? "unknown"), RemoteOutputExcerpt.Of(output)));
        }

        var platformKey = RemotePlatform.PinKeyFor(state.KernelName, state.Machine);
        if (platformKey is null)
        {
            return new Stopped(new ProvisionUnsupportedPlatform(state.KernelName, state.Machine));
        }

        OmpRelease? latest = null;
        if (IsOmpManaged(request.Profile))
        {
            var release = await ReadReleaseAsync(state, cancellationToken).ConfigureAwait(false);
            if (release.Stop is { } stop)
            {
                return new Stopped(stop);
            }

            latest = release.Release;
        }

        return new Ready(state, RemoteDriftPlanner.Plan(state, platformKey, _pin, latest, request.PluginBundle));
    }

    private async Task<ReleaseStep> ReadReleaseAsync(RemoteState state, CancellationToken cancellationToken)
    {
        OmpRelease release;
        try
        {
            release = await _releases.ReadLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            return ReleaseStep.Refused(new ProvisionDesiredStateUnavailable(state, RemoteProvisionMessages.RegistryUnavailable(RemoteOutputExcerpt.Of(exception.Message))));
        }
        catch (InvalidDataException exception)
        {
            return ReleaseStep.Refused(new ProvisionDesiredStateUnavailable(state, RemoteProvisionMessages.RegistryUnavailable(RemoteOutputExcerpt.Of(exception.Message))));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ReleaseStep.Refused(new ProvisionDesiredStateUnavailable(state, RemoteProvisionMessages.RegistryTimedOut));
        }

        return EnginesProblem(release.EnginesBun) is { } problem
            ? ReleaseStep.Refused(new ProvisionDesiredStateUnavailable(state, problem))
            : ReleaseStep.Accepted(release);
    }

    private string? EnginesProblem(string? range)
    {
        if (range is null)
        {
            return null;
        }

        if (!range.StartsWith(">=", StringComparison.Ordinal) || !Version.TryParse(range[2..], out var minimum))
        {
            return RemoteProvisionMessages.EnginesUnsupported(RemoteOutputExcerpt.Of(range), _pin.Version);
        }

        return Version.TryParse(_pin.Version, out var pinned) && pinned >= minimum
            ? null
            : RemoteProvisionMessages.EnginesNotMet(RemoteOutputExcerpt.Of(range), _pin.Version);
    }

    private abstract record Step;

    private sealed record Ready(RemoteState State, ProvisionPlan Plan) : Step;

    private sealed record Stopped(RemoteProvisionResult Result) : Step;

    private sealed record ReleaseStep(OmpRelease? Release, RemoteProvisionResult? Stop)
    {
        public static ReleaseStep Accepted(OmpRelease release) => new(release, null);

        public static ReleaseStep Refused(RemoteProvisionResult stop) => new(null, stop);
    }
}
