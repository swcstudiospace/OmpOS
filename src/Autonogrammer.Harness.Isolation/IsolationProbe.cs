namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Runs <c>bwrap --version</c> and one <c>bwrap</c> self-test at session start, through <see cref="IIsolationProcessRunner"/>.
/// Failures map to named degraded states. A non-Linux runtime identifier does not start a process.
/// </summary>
public sealed class IsolationProbe
{
    private static readonly string[] VersionArguments = ["--version"];
    private static readonly string[] SelfTestArguments = ["--die-with-parent", "--unshare-user", "--unshare-pid", "--ro-bind", "/", "/", "/bin/true"];

    private readonly IIsolationProcessRunner _runner;
    private readonly string _runtimeIdentifier;
    private readonly TimeSpan _timeout;

    /// <summary>Initializes a new instance of the <see cref="IsolationProbe"/> class.</summary>
    /// <param name="runner">The process runner. A test supplies a fake.</param>
    /// <param name="runtimeIdentifier">The runtime identifier, for example <c>linux-x64</c> or <c>win-x64</c>.</param>
    /// <param name="timeout">How long each probe process may run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runner"/> or <paramref name="runtimeIdentifier"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is not positive.</exception>
    public IsolationProbe(IIsolationProcessRunner runner, string runtimeIdentifier, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(runtimeIdentifier);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        _runner = runner;
        _runtimeIdentifier = runtimeIdentifier;
        _timeout = timeout;
    }

    /// <summary>Probes bubblewrap and returns the level actually achieved.</summary>
    /// <param name="cancellationToken">Cancels the probe. A timeout of the child is <see cref="IsolationProbeOutcome.ProbeTimeout"/>, not this token.</param>
    /// <returns>The outcome. A non-Linux runtime identifier is <see cref="IsolationProbeOutcome.PlatformUnsupported"/>.</returns>
    public async Task<IsolationProbeOutcome> ProbeAsync(CancellationToken cancellationToken)
    {
        if (!_runtimeIdentifier.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
        {
            return new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.PlatformUnsupported);
        }

        var version = await RunAsync(VersionArguments, cancellationToken).ConfigureAwait(false);
        if (version.TimedOut)
        {
            return TimedOut();
        }

        if (version.ExecutableMissing || version.ExitCode != 0)
        {
            return new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing);
        }

        var selfTest = await RunAsync(SelfTestArguments, cancellationToken).ConfigureAwait(false);
        if (selfTest.TimedOut)
        {
            return TimedOut();
        }

        if (selfTest.ExecutableMissing)
        {
            return new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.BwrapMissing);
        }

        if (selfTest.ExitCode != 0)
        {
            return new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.UsernsDisabled);
        }

        return IsolationProbeOutcome.Succeeded;
    }

    private async Task<IsolationProcessRun> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            var run = await _runner.RunAsync(BubblewrapIsolator.Executable, arguments, _timeout, timeout.Token).ConfigureAwait(false);
            return run.TimedOut ? new IsolationProcessRun(run.ExitCode, run.Output, true, run.ExecutableMissing) : run;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new IsolationProcessRun(-1, string.Empty, true, false);
        }
    }

    private static IsolationProbeOutcome TimedOut() =>
        new(IsolationLevel.None, IsolationProbeOutcome.ProbeTimeout);
}
