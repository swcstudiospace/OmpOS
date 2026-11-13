using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Runs <c>bwrap --version</c> and one <c>bwrap</c> namespace self-test at session start, then, for a session policy, the
/// session canary: the exact argument vector the session launches, run with a shell script that checks the policy from
/// inside the sandbox. Only a passing canary reports filesystem isolation. Failures map to named degraded states. A
/// non-Linux runtime identifier does not start a process.
/// </summary>
public sealed class IsolationProbe
{
    private static readonly string[] VersionArguments = ["--version"];
    private static readonly string[] SelfTestArguments = ["--die-with-parent", "--unshare-user", "--unshare-pid", "--ro-bind", "/", "/", "/bin/true"];

    /// <summary>
    /// The session canary script. Its first argument is the scratch directory and the rest are the deny targets. Each
    /// failing check exits with a distinct code (21 to 26), so a failure names its control.
    /// </summary>
    private static readonly string[] CanaryLines =
    [
        "SCRATCH=\"$1\"",
        "shift",
        "(echo x > /etc/autonogrammer-canary) 2>/dev/null && exit 21",
        "for D in \"$@\"; do",
        "  if [ -d \"$D\" ]; then [ -z \"$(ls -A \"$D\" 2>/dev/null)\" ] || exit 22",
        "  elif [ -e \"$D\" ]; then [ ! -s \"$D\" ] || exit 22",
        "  fi",
        "done",
        "[ ! -S \"/run/user/$(id -u)/bus\" ] || exit 23",
        "[ -z \"${SSH_AUTH_SOCK-}\" ] || [ ! -S \"$SSH_AUTH_SOCK\" ] || exit 24",
        "echo ok > \"$SCRATCH/.canary\" || exit 25",
        "rm -f \"$SCRATCH/.canary\"",
        "n=0; for p in /proc/[0-9]*; do n=$((n+1)); done",
        "[ \"$n\" -le 8 ] || exit 26",
        "exit 0",
    ];

    private static readonly string CanaryScript = string.Join('\n', CanaryLines);

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

    /// <summary>Probes bubblewrap's namespaces. A passing self-test is <see cref="IsolationProbeOutcome.Succeeded"/>, which names no filesystem level.</summary>
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

    /// <summary>
    /// Probes bubblewrap for one session policy: the namespace probe, then the session canary over the exact argument
    /// vector that session launches. Filesystem isolation is reported only when the canary passes.
    /// </summary>
    /// <param name="policy">The session policy whose launched vector the canary runs.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>
    /// The namespace probe's degraded outcome when it failed. Otherwise <see cref="IsolationProbeOutcome.SessionCanaryPassed"/>,
    /// <see cref="IsolationProbeOutcome.SessionCanaryFailed"/>, or <see cref="IsolationProbeOutcome.PolicyUnverified"/> when the
    /// policy cannot be built into a launchable vector on this host.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public async Task<IsolationProbeOutcome> ProbeSessionAsync(IsolationPolicy policy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var namespaces = await ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (namespaces.DegradedCode is not null || !policy.Enabled)
        {
            return namespaces;
        }

        return await RunSessionCanaryAsync(policy, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IsolationProbeOutcome> RunSessionCanaryAsync(IsolationPolicy policy, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> arguments;
        try
        {
            arguments = new BubblewrapIsolator(policy).Wrap(CanaryLaunch(policy)).Arguments;
        }
        catch (InvalidOperationException)
        {
            return new IsolationProbeOutcome(IsolationLevel.None, IsolationProbeOutcome.PolicyUnverified);
        }

        var run = await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
        if (run.TimedOut)
        {
            return TimedOut();
        }

        return run.ExitCode == 0 && !run.ExecutableMissing
            ? IsolationProbeOutcome.SessionCanaryPassed
            : IsolationProbeOutcome.SessionCanaryFailed;
    }

    private static ProcessLaunch CanaryLaunch(IsolationPolicy policy)
    {
        var arguments = new List<string> { "-c", CanaryScript, "sh", policy.ScratchDirectory ?? string.Empty };
        arguments.AddRange(BubblewrapArgvBuilder.MaskTargets(policy));
        return new ProcessLaunch(
            "/bin/sh",
            arguments,
            policy.ScratchDirectory,
            new Dictionary<string, string?>(StringComparer.Ordinal));
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
