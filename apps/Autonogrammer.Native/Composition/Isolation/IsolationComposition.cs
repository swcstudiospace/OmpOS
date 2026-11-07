using Autonogrammer.Cowork.Sessions;
using Autonogrammer.Harness.Isolation;
using Autonogrammer.Native.Composition.Permissions;
using Autonogrammer.Presentation.Isolation;

namespace Autonogrammer.Native.Composition.Isolation;

/// <summary>
/// Selects the process wrapper by surface: Cowork on, Code off. A failed probe asks and starts nothing until
/// Continue without isolation is answered. NotificationRequest and INotificationSink live in Harness.Notifications.
/// Presentation never references them: the bridge sits in Native and Native.Views.
/// </summary>
internal sealed class IsolationComposition
{
    private readonly IIsolationProcessRunner _runner;
    private readonly string _runtimeIdentifier;
    private readonly TimeSpan _timeout;

    /// <summary>Creates the composition.</summary>
    /// <param name="runner">The probe runner. Tests supply a fake.</param>
    /// <param name="runtimeIdentifier">The runtime identifier, for example <c>linux-x64</c>.</param>
    /// <param name="timeout">How long each probe process may run.</param>
    public IsolationComposition(IIsolationProcessRunner runner, string runtimeIdentifier, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        _runner = runner;
        _runtimeIdentifier = runtimeIdentifier;
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Prepares one session. Code skips the probe and gets no wrapper.</summary>
    /// <param name="cowork">True for a Cowork session.</param>
    /// <param name="homeDirectory">The user's home directory.</param>
    /// <param name="scratchDirectory">The session scratch directory.</param>
    /// <param name="grants">The folder grants. Read-write grants only get a bind.</param>
    /// <param name="continueWithoutIsolation">True when the user already answered the question.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>The decision, the chip, and whether the session may start.</returns>
    public async Task<IsolationSessionStart> PrepareAsync(
        bool cowork,
        string homeDirectory,
        string scratchDirectory,
        IReadOnlyList<CoworkFolderGrant>? grants,
        bool continueWithoutIsolation,
        CancellationToken cancellationToken)
    {
        var source = new CoworkFolderGrantSource(grants);
        var policy = cowork
            ? IsolationPolicy.ForCowork(homeDirectory, scratchDirectory, source.IsolationGrants(), platform: PlatformOf(_runtimeIdentifier))
            : IsolationPolicy.ForCode();
        IsolationProbeOutcome probe = policy.Enabled
            ? await new IsolationProbe(_runner, _runtimeIdentifier, _timeout).ProbeAsync(cancellationToken).ConfigureAwait(false)
            : IsolationProbeOutcome.Succeeded;
        var decision = IsolationPlanner.Decide(policy, probe, continueWithoutIsolation);
        var chip = new IsolationChipModel(decision.Level.ToString(), decision.DegradedCode, decision.NeedsConsent);
        var mayStart = !decision.NeedsConsent;
        return new IsolationSessionStart(decision, chip, mayStart ? decision.Wrapper : null, decision.Level == IsolationLevel.FilesystemIsolated, mayStart, source.IsolationGrants());
    }

    private static IsolationPlatform PlatformOf(string runtimeIdentifier)
    {
        if (runtimeIdentifier.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            return IsolationPlatform.Windows;
        }

        if (runtimeIdentifier.StartsWith("osx", StringComparison.OrdinalIgnoreCase) || runtimeIdentifier.StartsWith("mac", StringComparison.OrdinalIgnoreCase))
        {
            return IsolationPlatform.MacOs;
        }

        return IsolationPlatform.Linux;
    }
}
