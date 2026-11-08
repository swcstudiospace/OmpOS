using Autonogrammer.Harness.RemoteProvision;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// Turns each provisioning result into the Omp report the Remote page shows: what a check found, what an install applied, and why nothing could be done.
/// It is pure: it reads results and writes nothing.
/// </summary>
internal static class RemoteOmpReports
{
    /// <summary>The reason shown for an environment that is no longer saved.</summary>
    public const string NotSaved = "The environment is no longer saved.";

    /// <summary>The reason shown for a provisioning result this head does not know.</summary>
    public const string UnknownResult = "The provisioning result has no known meaning.";

    /// <summary>The platforms omp is verified on, in the words the unsupported-platform reason names them.</summary>
    private const string VerifiedPlatforms = "Linux x86_64 and Darwin arm64";

    /// <summary>Maps a provisioning result to the report of an Omp check or install.</summary>
    /// <param name="result">The result the provisioning service returned.</param>
    /// <returns>The report.</returns>
    public static RemoteOmpReport From(RemoteProvisionResult result) => result switch
    {
        ProvisionCheck check => FromCheck(check),
        ProvisionNeedsConfirmation needs => new RemoteOmpReport(RemoteOmpOutcome.NeedsCheck, Drift(needs.Drift), needs.Actions, null, false),
        ProvisionCompleted completed => FromCompleted(completed),
        ProvisionUnsupportedPlatform platform => Refused(
            "This machine reports " + platform.KernelName + " " + platform.Machine + ". Autonogrammer builds omp only on " + VerifiedPlatforms + "."),
        ProvisionUnavailable unavailable => Unavailable(unavailable.Reason),
        ProvisionProbeFailed probe => Unavailable(probe.Reason),
        ProvisionDesiredStateUnavailable desired => Unavailable(desired.Reason),
        _ => Unavailable(UnknownResult),
    };

    /// <summary>The report of an install that was not run, because the machine was not checked or changed since the check.</summary>
    /// <returns>The report, with no lines.</returns>
    public static RemoteOmpReport Stale() => new(RemoteOmpOutcome.NeedsCheck, [], [], null, false);

    /// <summary>The report of an environment Omp cannot be installed on.</summary>
    /// <param name="reason">Why, in one plain sentence.</param>
    /// <returns>The report.</returns>
    public static RemoteOmpReport Refused(string reason) => new(RemoteOmpOutcome.Refused, [], [], reason, false);

    /// <summary>The report of a check that could not reach or read the environment.</summary>
    /// <param name="reason">Why, in one plain sentence.</param>
    /// <returns>The report.</returns>
    public static RemoteOmpReport Unavailable(string reason) => new(RemoteOmpOutcome.Unavailable, [], [], reason, false);

    /// <summary>The reason an environment with a custom remote omp path is refused: Autonogrammer manages only the default omp.</summary>
    /// <param name="path">The path the environment sets.</param>
    /// <returns>The sentence.</returns>
    public static string CustomOmpPath(string path) =>
        "This environment sets its remote omp path to " + path + ". Autonogrammer installs omp only at its default place, so it does not manage this one. "
        + "Clear the remote omp path to let Autonogrammer install omp here.";

    private static RemoteOmpReport FromCheck(ProvisionCheck check)
    {
        var drift = Drift(check.Drift);
        if (drift.Count == 0)
        {
            return new RemoteOmpReport(RemoteOmpOutcome.UpToDate, drift, [], null, false);
        }

        if (check.Actions.Count > 0)
        {
            return new RemoteOmpReport(RemoteOmpOutcome.Planned, drift, check.Actions, null, false);
        }

        // Every drift item is blocked, so no install can run. The blocking reasons say why.
        var reasons = check.Drift.Select(static item => item.BlockedBecause).OfType<string>();
        return new RemoteOmpReport(RemoteOmpOutcome.Refused, drift, [], string.Join(" ", reasons), false);
    }

    private static RemoteOmpReport FromCompleted(ProvisionCompleted completed)
    {
        if (completed.Succeeded)
        {
            return new RemoteOmpReport(RemoteOmpOutcome.Installed, [], [], null, true);
        }

        var changed = completed.Outcomes.Any(static outcome => outcome.Status == ProvisionActionStatus.Succeeded);
        var failure = completed.Outcomes.FirstOrDefault(static outcome => outcome.Status == ProvisionActionStatus.Failed);
        return new RemoteOmpReport(RemoteOmpOutcome.Failed, [], [], failure?.Message, changed);
    }

    private static List<RemoteOmpDriftLine> Drift(IReadOnlyList<ProvisionDriftItem> items) =>
        items.Select(static item => new RemoteOmpDriftLine(item.Subject, item.Reason, item.BlockedBecause)).ToList();
}
