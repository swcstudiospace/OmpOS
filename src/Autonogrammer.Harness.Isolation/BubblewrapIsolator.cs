using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Wraps an intended launch in <c>bwrap</c> by argument vector. It starts nothing; the probe is the only caller that starts <c>bwrap</c>.
/// </summary>
public sealed class BubblewrapIsolator : IProcessLaunchWrapper
{
    /// <summary>The bubblewrap executable name. It is resolved from <c>PATH</c>.</summary>
    public const string Executable = "bwrap";

    private readonly IsolationPolicy _policy;

    /// <summary>Initializes a new instance of the <see cref="BubblewrapIsolator"/> class.</summary>
    /// <param name="policy">The session policy the argument vector is built from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public BubblewrapIsolator(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <inheritdoc />
    public ProcessLaunch Wrap(ProcessLaunch intended)
    {
        ArgumentNullException.ThrowIfNull(intended);
        var arguments = new List<string>(
            BubblewrapArgvBuilder.AdaptToHost(_policy, BubblewrapArgvBuilder.Build(_policy)));
        if (!string.IsNullOrEmpty(intended.WorkingDirectory))
        {
            arguments.Add("--chdir");
            arguments.Add(intended.WorkingDirectory);
        }

        arguments.Add(intended.FileName);
        if (intended.Arguments is not null)
        {
            arguments.AddRange(intended.Arguments);
        }

        return new ProcessLaunch(
            Executable,
            arguments,
            intended.WorkingDirectory,
            BubblewrapArgvBuilder.Scrub(intended.Environment));
    }
}
