using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Wraps an intended launch in <c>sandbox-exec -p &lt;profile&gt;</c> by argument vector. The profile is one argument, never a shell string.
/// It starts nothing.
/// </summary>
public sealed class SeatbeltIsolator : IProcessLaunchWrapper
{
    /// <summary>The Seatbelt executable name.</summary>
    public const string Executable = "sandbox-exec";

    private readonly IsolationPolicy _policy;

    /// <summary>Initializes a new instance of the <see cref="SeatbeltIsolator"/> class.</summary>
    /// <param name="policy">The session policy the profile is built from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="policy"/> is null.</exception>
    public SeatbeltIsolator(IsolationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    /// <inheritdoc />
    public ProcessLaunch Wrap(ProcessLaunch intended)
    {
        ArgumentNullException.ThrowIfNull(intended);
        var arguments = new List<string> { "-p", SeatbeltProfileBuilder.Build(_policy), intended.FileName };
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
