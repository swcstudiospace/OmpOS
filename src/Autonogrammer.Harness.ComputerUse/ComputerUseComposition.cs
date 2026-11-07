using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Executor;
using Autonogrammer.Harness.ComputerUse.Policy;

namespace Autonogrammer.Harness.ComputerUse;

/// <summary>
/// Single composition root. The desktop head has no DI container.
/// 73-03 appends Tools, Frames and Audit composition only through a separate ComputerUseToolsComposition file.
/// </summary>
public static class ComputerUseComposition
{
    /// <summary>Builds the core. Invalid options throw before any service is used. This method never constructs an OS backend.</summary>
    /// <param name="options">Head-supplied dependencies.</param>
    /// <param name="cancellationToken">Token that cancels settings and catalogue loads.</param>
    /// <returns>The core.</returns>
    /// <exception cref="InvalidOperationException">Validation failed. The message lists option keys and never values.</exception>
    public static async ValueTask<ComputerUseCore> CreateAsync(ComputerUseCompositionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ComputerUseOptions merged = await MergeAsync(options, cancellationToken).ConfigureAwait(false);
        return await WireAsync(options, merged, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<ComputerUseOptions> MergeAsync(ComputerUseCompositionOptions options, CancellationToken cancellationToken)
    {
        ComputerUseOptionsReadResult read = ComputerUseOptionsReader.Read(options.GetVariable);
        ComputerUseUserSettings settings = await options.Settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        ComputerUseOptions merged = read.Options;
        if (!string.IsNullOrWhiteSpace(settings.KillChord))
        {
            merged.KillChord = settings.KillChord;
        }

        if (settings.AuditThumbnails is bool thumbnails)
        {
            merged.AuditThumbnails = thumbnails;
        }

        List<string> failures = MergeFailures(read.Failures, ComputerUseOptionsValidator.Validate(merged));
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", failures));
        }

        return merged;
    }

    private static List<string> MergeFailures(IReadOnlyList<string> readFailures, IReadOnlyList<string> validation)
    {
        List<string> failures = [.. readFailures];
        foreach (string failure in validation)
        {
            if (!failures.Contains(failure, StringComparer.Ordinal))
            {
                failures.Add(failure);
            }
        }

        return failures;
    }

    private static async ValueTask<ComputerUseCore> WireAsync(
        ComputerUseCompositionOptions options,
        ComputerUseOptions merged,
        CancellationToken cancellationToken)
    {
        AppCatalogue catalogue = await AppCatalogue.LoadAsync(options.Settings, cancellationToken).ConfigureAwait(false);
        GrantStore grants = new(options.Time);
        RateLimiter limiter = new(options.Time, merged.RatePerMinute, merged.RateBurst, merged.StepLimit, merged.StepContinueGrant, merged.FailurePause);
        KillSwitchCoordinator killSwitch = new(options.Hotkey, options.Backend, new NoopKillSwitchAudit());
        IComputerObservation observation = options.Backend as IComputerObservation ?? new BackendObservation(options.Backend);
        ComputerActionGate gate = new(merged, options.Access, observation, grants, catalogue, limiter, killSwitch, options.Prompt, new AcceptingAudit(), options.Time);
        ComputerActionExecutor executor = new(options.Backend, gate, killSwitch, options.Time, merged.TakeoverPx);
        return new ComputerUseCore
        {
            Gate = gate,
            ActionGate = gate,
            Executor = executor,
            Grants = grants,
            GrantStore = grants,
            Denylist = catalogue,
            KillSwitch = killSwitch,
            KillSwitchCoordinator = killSwitch,
            RateLimiter = limiter,
            Catalogue = catalogue,
            Options = merged,
            StateDirectory = options.StateDirectory,
        };
    }

    private sealed class NoopKillSwitchAudit : IKillSwitchAudit
    {
        public void WriteTrip()
        {
        }
    }

    private sealed class AcceptingAudit : IComputerUseAuditSink
    {
        public ValueTask<bool> TryWriteAheadAsync(ComputerActionRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class BackendObservation(IComputerUseBackend backend) : IComputerObservation
    {
        public ValueTask<ComputerAvailability> GetAvailabilityAsync(CancellationToken cancellationToken) =>
            backend.GetAvailabilityAsync(cancellationToken);

        public ValueTask<IReadOnlyList<ComputerPermissionStatus>> GetPermissionStatusAsync(CancellationToken cancellationToken) =>
            backend.GetPermissionStatusAsync(cancellationToken);

        public ValueTask<IReadOnlyList<WindowInfo>> ListWindowsAsync(CancellationToken cancellationToken) =>
            backend.ListWindowsAsync(cancellationToken);
    }
}
